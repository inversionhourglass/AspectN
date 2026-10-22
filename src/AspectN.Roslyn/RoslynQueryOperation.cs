using AspectN.AdapterInternals;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Microsoft.CodeAnalysis;
using CoreDiagnosticSeverity = AspectN.Diagnostics.DiagnosticSeverity;
using CoreTypeKind = AspectN.Metadata.TypeKind;

namespace AspectN.Roslyn;

internal sealed class RoslynQueryOperation
{
    private readonly TypeSystemQueryContext _query;
    private readonly RoslynMappingOperation _mapper;
    private readonly Compilation _compilation;
    internal RoslynQueryOperation(RoslynTypeSystem owner, TypeSystemQueryContext query)
    {
        _query = query;
        _mapper = owner.Mapper.CreateOperation(new MappingContext(owner.Mapper.Options, query.CancellationToken, query));
        _compilation = owner.Compilation;
    }

    public TypeRelationResult AreEqual(
        TypeSignature left,
        TypeSignature right, CancellationToken cancellationToken = default)
    {
        return StructuralTypeComparer.AreEqual(left, right, _query);
    }

    public TypeRelationResult IsAssignableTo(
        TypeSignature candidate,
        TypeSignature target,
        CancellationToken cancellationToken = default)
    {
        _query.ThrowIfExhausted();
        var arrayRelation = ArrayRelations.Compare(candidate, target, CompareUnqualified, _query);
        if (arrayRelation is not null) return arrayRelation;
        var structural = CompareUnqualified(candidate, target);
        if (structural.Status == MatchStatus.Matched) return structural;
        TypeRelationResult? unresolved = structural.Status == MatchStatus.Unknown ? structural : null;

        if (!_mapper.TryGetTypeSymbol(candidate, out var candidateSymbol) ||
            candidateSymbol is null)
        {
            return unresolved ?? MissingNativeSymbol(candidate);
        }

        try
        {
            // + 只匹配声明的继承/实现关系；ClassifyCommonConversion 额外包含数组协变与泛型变型，
            // 与模式路径的合成目标语义不一致，统一按名义闭包遍历（Interfaces 含运行时合成的数组接口）。
            var pending = new Queue<ITypeSymbol>();
            var visited = new HashSet<ITypeSymbol>(
                SymbolEqualityComparer.Default);
            pending.Enqueue(candidateSymbol);
            while (pending.Count > 0)
            {
                _query.ThrowIfExhausted();
                var current = pending.Dequeue();
                try
                {
                    if (!visited.Add(current)) continue;
                    if (!_query.TryConsumeRelationNodes(1)) _query.ThrowIfExhausted();
                    var relation = CompareUnqualified(_mapper.MapType(current), target);
                    if (relation.Status == MatchStatus.Matched) return relation;
                    // 不可读的祖先只影响该分支；后续已知接口仍能给出确定匹配。
                    if (relation.Status == MatchStatus.Unknown) unresolved ??= relation;
                    EnqueueNominalAncestors(current, pending);
                }
                catch (Exception exception) when (IsSymbolFailure(exception))
                {
                    unresolved ??= SymbolFailure(current, exception);
                }
            }

            return unresolved ?? TypeRelationResult.NotMatched;
        }
        catch (Exception exception) when (IsSymbolFailure(exception))
        {
            return SymbolFailure(candidateSymbol, exception);
        }
    }

    private void AddAncestor(List<TypeSignature> ancestors, TypeSignature signature, ref TypeRelationResult? failure)
    {
        var diagnostic = MetadataLimitGuard.GetFailure(signature, _query);
        if (diagnostic is not null) failure ??= TypeRelationResult.Unknown(diagnostic);
        else { _query.SpendWork(); ancestors.Add(signature); }
    }

    public InheritanceClosure GetInheritanceClosure(
        TypeSignature candidate,
        CancellationToken cancellationToken = default)
    {
        _query.ThrowIfExhausted();
        var candidateFailure = MetadataLimitGuard.GetFailure(candidate, _query);
        if (!_mapper.TryGetTypeSymbol(candidate, out var candidateSymbol) ||
            candidateSymbol is null)
        {
            return new InheritanceClosure(
                Array.Empty<TypeSignature>(),
                candidateFailure is null ? MissingNativeSymbol(candidate) : TypeRelationResult.Unknown(candidateFailure));
        }

        var closure = new List<TypeSignature>();
        TypeRelationResult? failure = candidateFailure is null ? null : TypeRelationResult.Unknown(candidateFailure);
        try
        {
            var pending = new Queue<ITypeSymbol>();
            if (!_query.TryConsumeRelationNodes(1)) _query.ThrowIfExhausted();
            EnqueueNominalAncestors(candidateSymbol, pending);

            var visited = new HashSet<ITypeSymbol>(
                SymbolEqualityComparer.Default) { candidateSymbol };
            while (pending.Count > 0)
            {
                _query.ThrowIfExhausted();
                var current = pending.Dequeue();
                try
                {
                    if (!visited.Add(current)) continue;
                    if (!_query.TryConsumeRelationNodes(1)) _query.ThrowIfExhausted();
                    AddAncestor(closure, _mapper.MapType(current), ref failure);
                    EnqueueNominalAncestors(current, pending);
                }
                catch (Exception exception) when (IsSymbolFailure(exception))
                {
                    failure ??= SymbolFailure(current, exception);
                }
            }
        }
        catch (Exception exception) when (IsSymbolFailure(exception))
        {
            // 符号读取失败：已产出的祖先仍有效，闭包标记为部分并携带失败诊断。
            return new InheritanceClosure(
                closure,
                SymbolFailure(candidateSymbol, exception));
        }

        return new InheritanceClosure(closure, failure);
    }

    public TypeRelationResult HasDirectAttribute(
        AttributeTarget target,
        TypeSignature attributeType,
        CancellationToken cancellationToken = default)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(attributeType);
#else
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (attributeType is null)
            throw new ArgumentNullException(nameof(attributeType));
#endif

        _query.ThrowIfExhausted();
        var structuralFailure = MetadataLimitGuard.GetFailure(attributeType, _query) ?? MetadataLimitGuard.GetFailure(target, _query);
        if (structuralFailure is not null) return TypeRelationResult.Unknown(structuralFailure);
        var metadataFailure = AttributeAvailability.GetFailure(target);
        if (metadataFailure is not null) return metadataFailure;
        IReadOnlyList<AttributeSignature>? attributes;
        try
        {
            attributes = GetAttributes(target);
        }
        catch (Exception exception) when (IsSymbolFailure(exception))
        {
            return SymbolFailure(target.Kind.ToString(), exception);
        }

        if (attributes is null)
        {
            return TypeRelationResult.Unknown(
                MissingCapability(
                    "No Roslyn symbol is available for the attribute target."));
        }

        TypeRelationResult? unknown = null;
        foreach (var attribute in attributes)
        {
            if (!_query.TryConsumeAttributeRecords(1)) _query.ThrowIfExhausted();
            var relation = CompareUnqualified(
                attribute.AttributeType,
                attributeType);
            if (relation.Status == MatchStatus.Matched) return relation;
            if (relation.Status == MatchStatus.Unknown) unknown = relation;
        }

        return unknown ?? TypeRelationResult.NotMatched;
    }

    private IReadOnlyList<AttributeSignature>? GetAttributes(
        AttributeTarget target)
    {
        switch (target.Kind)
        {
            case AttributeTargetKind.Type:
                if (!_mapper.TryGetTypeSymbol(
                        target.Type!,
                        out var typeSymbol) ||
                    typeSymbol is null)
                {
                    return null;
                }

                return _mapper.MapTypeAttributes(typeSymbol);
            case AttributeTargetKind.Method:
                return target.Method!.Attributes;
            case AttributeTargetKind.Property:
                return target.Property!.Attributes;
            case AttributeTargetKind.Parameter:
                return target.Parameter!.Attributes;
            case AttributeTargetKind.Return:
                return target.Method!.ReturnAttributes;
            default:
                return null;
        }
    }

    private void EnqueueNominalAncestors(ITypeSymbol symbol, Queue<ITypeSymbol> pending)
    {
        if (symbol is ITypeParameterSymbol parameter)
        {
            // Roslyn 的类型形参没有普通 BaseType/Interfaces；使用声明约束并补充隐含基类。
            Enqueue(pending, _compilation.GetSpecialType(SpecialType.System_Object));
            if (parameter.HasValueTypeConstraint || parameter.HasUnmanagedTypeConstraint)
                Enqueue(pending, _compilation.GetSpecialType(SpecialType.System_ValueType));
            foreach (var constraint in parameter.ConstraintTypes) Enqueue(pending, constraint);
            return;
        }
        if (symbol.BaseType is not null) Enqueue(pending, symbol.BaseType);
        foreach (var implementation in symbol.Interfaces) Enqueue(pending, implementation);
    }

    private void Enqueue(Queue<ITypeSymbol> pending, ITypeSymbol item)
    {
        if (!_query.TryConsumeRelationEdges(1)) _query.ThrowIfExhausted();
        pending.Enqueue(item);
    }

    private TypeRelationResult CompareUnqualified(TypeSignature actual, TypeSignature expected)
    {
        return StructuralTypeComparer.AreEqual(actual, expected, _query, unqualified: true);
    }

    private static bool IsSymbolFailure(Exception exception)
    {
        return exception is InvalidOperationException or ArgumentException or AspectNLimitException;
    }

    private static TypeRelationResult MissingNativeSymbol(
        TypeSignature candidate)
    {
        return TypeRelationResult.Unknown(
            MissingCapability(
                $"No Roslyn type symbol is available for " +
                $"'{candidate.GetType().Name}'."));
    }

    private static AspectNDiagnostic MissingCapability(string message)
    {
        return new AspectNDiagnostic(
            DiagnosticCodes.MissingBackendCapability,
            CoreDiagnosticSeverity.Warning,
            message);
    }

    private TypeRelationResult SymbolFailure(
        object source,
        Exception exception)
    {
        if (exception is AspectNLimitException limit) return TypeRelationResult.Unknown(limit.Diagnostic);
        var subject = source is string text ? text : source.GetType().Name;
        var detail = exception.Message;
        var exceptionType = exception.GetType().Name;
        _query.SpendWork(Math.Max(1L, subject.Length + (long)detail.Length + exceptionType.Length));
        return TypeRelationResult.Unknown(
            new AspectNDiagnostic(
                DiagnosticCodes.ResolutionFailure,
                CoreDiagnosticSeverity.Warning,
                $"Roslyn symbol data for '{subject}' could not be read. " +
                $"{exceptionType}: {detail}"));
    }
}
