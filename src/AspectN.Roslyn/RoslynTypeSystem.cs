using AspectN.AdapterInternals;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Microsoft.CodeAnalysis;
using CoreDiagnosticSeverity = AspectN.Diagnostics.DiagnosticSeverity;
using CoreTypeKind = AspectN.Metadata.TypeKind;

namespace AspectN.Roslyn;

/// <summary>Provides Roslyn-backed type and metadata operations for AspectN matching.</summary>
public sealed class RoslynTypeSystem : ITypeSystem
{
    /// <summary>Initializes a new instance of the RoslynTypeSystem class.</summary>
    /// <param name="compilation">Specifies the compilation.</param>
    public RoslynTypeSystem(Compilation compilation)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(compilation);
#else
        if (compilation is null)
            throw new ArgumentNullException(nameof(compilation));
#endif

        Compilation = compilation;
        Mapper = new RoslynSignatureMapper(compilation);
    }

    /// <summary>Gets the compilation.</summary>
    public Compilation Compilation { get; }

    /// <summary>Gets the capabilities.</summary>
    public TypeSystemCapabilities Capabilities => TypeSystemCapabilities.All;

    /// <summary>Gets the mapper.</summary>
    public RoslynSignatureMapper Mapper { get; }

    /// <summary>Compares two values for structural equality.</summary>
    /// <param name="left">Specifies the left.</param>
    /// <param name="right">Specifies the right.</param>
    /// <returns>Returns the result of the operation.</returns>
    public TypeRelationResult AreEqual(
        TypeSignature left,
        TypeSignature right)
    {
        return StructuralTypeComparer.AreEqual(left, right);
    }

    /// <summary>Determines whether is assignable to.</summary>
    /// <param name="candidate">Specifies the candidate.</param>
    /// <param name="target">Specifies the target.</param>
    /// <param name="cancellationToken">Specifies the cancellation token.</param>
    /// <returns>Returns the result of the operation.</returns>
    public TypeRelationResult IsAssignableTo(
        TypeSignature candidate,
        TypeSignature target,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var arrayRelation = ArrayRelations.Compare(candidate, target, CompareUnqualified);
        if (arrayRelation is not null) return arrayRelation;
        var structural = CompareUnqualified(candidate, target);
        if (structural.Status == MatchStatus.Matched) return structural;
        TypeRelationResult? unresolved = structural.Status == MatchStatus.Unknown ? structural : null;

        if (!Mapper.TryGetTypeSymbol(candidate, out var candidateSymbol) ||
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
                cancellationToken.ThrowIfCancellationRequested();
                var current = pending.Dequeue();
                if (!visited.Add(current)) continue;
                try
                {
                    var relation = CompareUnqualified(Mapper.MapType(current), target);
                    if (relation.Status == MatchStatus.Matched) return relation;
                    // 不可读的祖先只影响该分支；后续已知接口仍能给出确定匹配。
                    if (relation.Status == MatchStatus.Unknown) unresolved ??= relation;
                    EnqueueNominalAncestors(current, pending);
                }
                catch (AspectNLimitException exception)
                {
                    unresolved ??= TypeRelationResult.Unknown(exception.Diagnostic);
                }
            }

            return unresolved ?? TypeRelationResult.NotMatched;
        }
        catch (Exception exception) when (IsSymbolFailure(exception))
        {
            return SymbolFailure(candidateSymbol, exception);
        }
    }

    /// <summary>Gets the declared inheritance closure of a type, excluding the type itself.</summary>
    /// <param name="candidate">Specifies the candidate.</param>
    /// <param name="cancellationToken">Specifies the cancellation token.</param>
    /// <returns>Returns the known ancestors and the failure, if any, that stopped expansion.</returns>
    public InheritanceClosure GetInheritanceClosure(
        TypeSignature candidate,
        CancellationToken cancellationToken = default)
    {
        if (!Mapper.TryGetTypeSymbol(candidate, out var candidateSymbol) ||
            candidateSymbol is null)
        {
            return new InheritanceClosure(
                Array.Empty<TypeSignature>(),
                MissingNativeSymbol(candidate));
        }

        var closure = new List<TypeSignature>();
        TypeRelationResult? failure = null;
        try
        {
            var pending = new Queue<ITypeSymbol>();
            EnqueueNominalAncestors(candidateSymbol, pending);

            var visited = new HashSet<ITypeSymbol>(
                SymbolEqualityComparer.Default) { candidateSymbol };
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = pending.Dequeue();
                if (!visited.Add(current)) continue;
                try
                {
                    closure.Add(Mapper.MapType(current));
                    EnqueueNominalAncestors(current, pending);
                }
                catch (AspectNLimitException exception)
                {
                    failure ??= TypeRelationResult.Unknown(exception.Diagnostic);
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

    /// <summary>Determines whether has direct attribute.</summary>
    /// <param name="target">Specifies the target.</param>
    /// <param name="attributeType">Specifies the attribute type.</param>
    /// <param name="cancellationToken">Specifies the cancellation token.</param>
    /// <returns>Returns the result of the operation.</returns>
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

        cancellationToken.ThrowIfCancellationRequested();
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
                if (!Mapper.TryGetTypeSymbol(
                        target.Type!,
                        out var typeSymbol) ||
                    typeSymbol is null)
                {
                    return null;
                }

                return Mapper.MapTypeAttributes(typeSymbol);
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
            pending.Enqueue(Compilation.GetSpecialType(SpecialType.System_Object));
            if (parameter.HasValueTypeConstraint || parameter.HasUnmanagedTypeConstraint)
                pending.Enqueue(Compilation.GetSpecialType(SpecialType.System_ValueType));
            foreach (var constraint in parameter.ConstraintTypes) pending.Enqueue(constraint);
            return;
        }
        if (symbol.BaseType is not null) pending.Enqueue(symbol.BaseType);
        foreach (var implementation in symbol.Interfaces) pending.Enqueue(implementation);
    }

    private static TypeRelationResult CompareUnqualified(TypeSignature actual, TypeSignature expected)
    {
        return UnqualifiedStructuralComparer.AreEqual(actual, expected);
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

    private static TypeRelationResult SymbolFailure(
        object source,
        Exception exception)
    {
        if (exception is AspectNLimitException limit) return TypeRelationResult.Unknown(limit.Diagnostic);
        return TypeRelationResult.Unknown(
            new AspectNDiagnostic(
                DiagnosticCodes.MissingBackendCapability,
                CoreDiagnosticSeverity.Warning,
                $"Roslyn symbol data for '{source}' could not be read. " +
                $"{exception.GetType().Name}: {exception.Message}"));
    }
}
