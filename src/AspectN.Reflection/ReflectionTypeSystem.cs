using AspectN.AdapterInternals;
using System.Reflection;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Reflection.Internal;
using AspectN.TypeSystem;

namespace AspectN.Reflection;

/// <summary>Provides reflection-backed type and metadata operations for AspectN matching.</summary>
public sealed class ReflectionTypeSystem : ITypeSystem
{
    /// <summary>Initializes a new instance of the ReflectionTypeSystem class.</summary>
    public ReflectionTypeSystem()
    {
        Mapper = new ReflectionSignatureMapper();
    }

    /// <summary>Gets the capabilities.</summary>
    public TypeSystemCapabilities Capabilities => TypeSystemCapabilities.All;

    /// <summary>Gets the mapper.</summary>
    public ReflectionSignatureMapper Mapper { get; }

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

        if (!Mapper.TryGetType(candidate, out var candidateType, out var candidateContext) ||
            candidateType is null)
        {
            return MissingNativeReference(candidate);
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
                cancellationToken.ThrowIfCancellationRequested();
                var (current, context) = pending.Dequeue();
                try
                {
                    if (!visited.Add((current, context))) continue;
                    var relation = CompareUnqualified(
                        Mapper.MapType(current, context),
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

    /// <summary>Gets the declared inheritance closure of a type, excluding the type itself.</summary>
    /// <param name="candidate">Specifies the candidate.</param>
    /// <param name="cancellationToken">Specifies the cancellation token.</param>
    /// <returns>Returns the known ancestors and the failure, if any, that stopped expansion.</returns>
    public InheritanceClosure GetInheritanceClosure(
        TypeSignature candidate,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Mapper.TryGetType(candidate, out var candidateType, out var candidateContext) ||
            candidateType is null)
        {
            return new InheritanceClosure(
                Array.Empty<TypeSignature>(),
                MissingNativeReference(candidate));
        }

        var closure = new List<TypeSignature>();
        var pending = new Queue<(Type Type, Type? Context)>();
        pending.Enqueue((candidateType, candidateContext));
        var visited = new HashSet<(Type Type, Type? Context)>();
        TypeRelationResult? failure = null;
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (current, context) = pending.Dequeue();
            if (!visited.Add((current, context))) continue;
            try
            {
                if (current != candidateType) closure.Add(Mapper.MapType(current, context));
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
                if (!Mapper.TryGetType(target.Type!, out var type) ||
                    type is null)
                {
                    return null;
                }

                return Mapper.MapTypeAttributes(type);
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

    private static void EnqueueNominalAncestors(Type type, Queue<(Type Type, Type? Context)> pending, Type? context)
    {
        if (type.IsGenericParameter)
        {
            context ??= type.DeclaringMethod?.DeclaringType ?? type.DeclaringType;
            pending.Enqueue((typeof(object), null));
            if ((type.GenericParameterAttributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
                pending.Enqueue((typeof(ValueType), null));
            foreach (var constraint in type.GetGenericParameterConstraints())
                pending.Enqueue((SubstituteConstraint(constraint, context), context));
            return;
        }
        if (type.BaseType is not null) pending.Enqueue((type.BaseType, null));
        foreach (var implementation in type.GetInterfaces()) pending.Enqueue((implementation, null));
    }

    private static Type SubstituteConstraint(Type type, Type? context)
    {
        if (context is null || !context.IsConstructedGenericType) return type;
        if (type.IsGenericParameter && type.DeclaringMethod is null && ReflectionGenericContext.AppliesTo(type, context))
            return context.GetGenericArguments()[type.GenericParameterPosition];
        if (type.IsArray)
        {
            var element = SubstituteConstraint(type.GetElementType()!, context);
            return RuntimeTypeFacts.IsVector(type) ? element.MakeArrayType() : element.MakeArrayType(type.GetArrayRank());
        }
        if (type.IsConstructedGenericType)
            return type.GetGenericTypeDefinition().MakeGenericType(type.GetGenericArguments()
                .Select(argument => SubstituteConstraint(argument, context)).ToArray());
        return type;
    }

    private static TypeRelationResult CompareUnqualified(TypeSignature actual, TypeSignature expected)
    {
        return UnqualifiedStructuralComparer.AreEqual(actual, expected);
    }

    private static bool IsMetadataFailure(Exception exception)
    {
        return exception is TypeLoadException or
            FileNotFoundException or
            FileLoadException or
            BadImageFormatException or
            NotSupportedException or
            InvalidOperationException or
            ArgumentException;
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

    private static TypeRelationResult ReflectionFailure(
        object source,
        Exception exception)
    {
        return TypeRelationResult.Unknown(
            new AspectNDiagnostic(
                DiagnosticCodes.ResolutionFailure,
                DiagnosticSeverity.Warning,
                $"Reflection metadata for '{source}' could not be read. " +
                $"{exception.GetType().Name}: {exception.Message}"));
    }
}
