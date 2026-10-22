using AspectN.Execution;
using AspectN.AdapterInternals;
using System.Reflection;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Reflection.Internal;
using AspectN.TypeSystem;

namespace AspectN.Reflection;

internal sealed class ReflectionQueryOperation
{
    private readonly TypeSystemQueryContext _query;
    private readonly MappingContext _resources;
    private readonly ReflectionMappingOperation _mapper;
    internal ReflectionQueryOperation(ReflectionTypeSystem owner, TypeSystemQueryContext query)
    {
        _query = query;
        _resources = new MappingContext(owner.Mapper.Options, query.CancellationToken, query);
        _mapper = owner.Mapper.CreateOperation(_resources);
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

        if (!_mapper.TryGetType(candidate, out var candidateType, out var candidateContext) ||
            candidateType is null)
        {
            return unresolved ?? MissingNativeReference(candidate);
        }

        try
        {
            // + 只匹配声明的继承/实现关系；IsAssignableFrom 额外包含数组协变与泛型变型，
            // 与模式路径的合成目标语义不一致，统一按名义闭包遍历（GetInterfaces 含运行时合成的数组接口）。
            var pending = new Queue<(Type Type, Type? Context)>();
            var visited = new HashSet<(Type Type, Type? Context)>();
            pending.Enqueue((candidateType, candidateContext));
            while (pending.Count > 0)
            {
                _query.ThrowIfExhausted();
                var (current, context) = pending.Dequeue();
                try
                {
                    if (!visited.Add((current, context))) continue;
                    if (!_query.TryConsumeRelationNodes(1)) _query.ThrowIfExhausted();
                    var relation = CompareUnqualified(
                        _mapper.MapType(current, context),
                        target);
                    if (relation.Status == MatchStatus.Matched) return relation;
                    if (relation.Status == MatchStatus.Unknown) unresolved ??= relation;

                    EnqueueNominalAncestors(current, pending, context);
                }
                catch (Exception exception) when (IsMetadataFailure(exception))
                {
                    unresolved ??= ReflectionFailure(current, exception);
                }
            }

            return unresolved ?? TypeRelationResult.NotMatched;
        }
        catch (Exception exception) when (IsMetadataFailure(exception))
        {
            return ReflectionFailure(candidateType, exception);
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
        if (!_mapper.TryGetType(candidate, out var candidateType, out var candidateContext) ||
            candidateType is null)
        {
            return new InheritanceClosure(
                Array.Empty<TypeSignature>(),
                candidateFailure is null ? MissingNativeReference(candidate) : TypeRelationResult.Unknown(candidateFailure));
        }

        var closure = new List<TypeSignature>();
        var pending = new Queue<(Type Type, Type? Context)>();
        pending.Enqueue((candidateType, candidateContext));
        var visited = new HashSet<(Type Type, Type? Context)>();
        TypeRelationResult? failure = candidateFailure is null ? null : TypeRelationResult.Unknown(candidateFailure);
        while (pending.Count > 0)
        {
            _query.ThrowIfExhausted();
            var (current, context) = pending.Dequeue();
            if (!visited.Add((current, context))) continue;
            if (!_query.TryConsumeRelationNodes(1)) _query.ThrowIfExhausted();
            try
            {
                if (current != candidateType) AddAncestor(closure, _mapper.MapType(current, context), ref failure);
                EnqueueNominalAncestors(current, pending, context);
            }
            catch (Exception exception) when (IsMetadataFailure(exception))
            {
                // 已排入队列的已知分支仍须继续；单个读取失败只标记闭包不完整。
                failure ??= ReflectionFailure(current, exception);
            }
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
        catch (Exception exception) when (IsMetadataFailure(exception))
        {
            return ReflectionFailure(target.Kind.ToString(), exception);
        }

        if (attributes is null)
        {
            return TypeRelationResult.Unknown(
                MissingCapability(
                    "No Reflection object is available for the attribute target."));
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
                if (!_mapper.TryGetType(target.Type!, out var type) ||
                    type is null)
                {
                    return null;
                }

                return _mapper.MapTypeAttributes(type);
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

    private void EnqueueNominalAncestors(Type type, Queue<(Type Type, Type? Context)> pending, Type? context)
    {
        if (type.IsGenericParameter)
        {
            context ??= type.DeclaringMethod?.DeclaringType ?? type.DeclaringType;
            Enqueue(pending, (typeof(object), null));
            if ((type.GenericParameterAttributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
                Enqueue(pending, (typeof(ValueType), null));
            foreach (var constraint in type.GetGenericParameterConstraints())
                Enqueue(pending, (SubstituteConstraint(constraint, context), context));
            return;
        }
        if (type.BaseType is not null) Enqueue(pending, (type.BaseType, null));
        foreach (var implementation in type.GetInterfaces()) Enqueue(pending, (implementation, null));
    }

    private Type SubstituteConstraint(Type type, Type? context) => WorkStack.Run(SubstituteConstraintWork(type, context), _resources.CancellationToken);

    private async StackWork<Type> SubstituteConstraintWork(Type type, Type? context)
    {
        _resources.SpendWork();
        if (context is null || !context.IsConstructedGenericType) return type;
        if (type.IsGenericParameter && type.DeclaringMethod is null && ReflectionGenericContext.AppliesTo(type, context, _resources))
            return context.GetGenericArguments()[type.GenericParameterPosition];
        if (type.IsArray)
        {
            var element = await SubstituteConstraintWork(type.GetElementType()!, context);
            return RuntimeTypeFacts.IsVector(type) ? element.MakeArrayType() : element.MakeArrayType(type.GetArrayRank());
        }
        if (type.IsConstructedGenericType)
            return type.GetGenericTypeDefinition().MakeGenericType((await WorkSequence.Select(type.GetGenericArguments(), argument => SubstituteConstraintWork(argument, context))));
        return type;
    }

    private void Enqueue(Queue<(Type Type, Type? Context)> pending, (Type Type, Type? Context) item)
    {
        if (!_query.TryConsumeRelationEdges(1)) _query.ThrowIfExhausted();
        pending.Enqueue(item);
    }

    private TypeRelationResult CompareUnqualified(TypeSignature actual, TypeSignature expected)
    {
        return StructuralTypeComparer.AreEqual(actual, expected, _query, unqualified: true);
    }

    private static bool IsMetadataFailure(Exception exception)
    {
        return exception is TypeLoadException or
            FileNotFoundException or
            FileLoadException or
            BadImageFormatException or
            NotSupportedException or
            InvalidOperationException or
            ArgumentException or AspectNLimitException;
    }

    private static TypeRelationResult MissingNativeReference(
        TypeSignature candidate)
    {
        return TypeRelationResult.Unknown(
            MissingCapability(
                $"No Reflection type is available for " +
                $"'{candidate.GetType().Name}'."));
    }

    private static AspectNDiagnostic MissingCapability(string message)
    {
        return new AspectNDiagnostic(
            DiagnosticCodes.MissingBackendCapability,
            DiagnosticSeverity.Warning,
            message);
    }

    private TypeRelationResult ReflectionFailure(
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
                DiagnosticSeverity.Warning,
                $"Reflection metadata for '{subject}' could not be read. " +
                $"{exceptionType}: {detail}"));
    }
}
