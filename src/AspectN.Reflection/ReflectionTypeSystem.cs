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
        var structural = CompareUnqualified(candidate, target);
        if (structural.Status != MatchStatus.NotMatched)
        {
            // 结构相等不足以证明可赋值：同元素的一维非向量数组不能赋给向量数组。
            return IsArrayIdentityAdmissible(candidate, target)
                ? structural
                : TypeRelationResult.NotMatched;
        }

        if (!Mapper.TryGetType(candidate, out var candidateType) ||
            candidateType is null)
        {
            return MissingNativeReference(candidate);
        }

        try
        {
            // + 只匹配声明的继承/实现关系；IsAssignableFrom 额外包含数组协变与泛型变型，
            // 与模式路径的合成目标语义不一致，统一按名义闭包遍历（GetInterfaces 含运行时合成的数组接口）。
            var pending = new Queue<Type>();
            var visited = new HashSet<Type>();
            pending.Enqueue(candidateType);
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = pending.Dequeue();
                if (!visited.Add(current)) continue;
                var relation = CompareUnqualified(
                    Mapper.MapType(current),
                    target);
                if (relation.Status != MatchStatus.NotMatched)
                {
                    return relation;
                }

                if (current.BaseType is not null)
                {
                    pending.Enqueue(current.BaseType);
                }

                foreach (var implementation in current.GetInterfaces())
                {
                    pending.Enqueue(implementation);
                }
            }

            return TypeRelationResult.NotMatched;
        }
        catch (Exception exception) when (IsMetadataFailure(exception))
        {
            return ReflectionFailure(candidateType, exception);
        }
    }

    // 同元素数组的恒等还要求形状方向兼容：非向量（非零下界）一维数组不能赋给向量数组。
    private bool IsArrayIdentityAdmissible(
        TypeSignature candidate,
        TypeSignature target)
    {
        if (candidate is not ArrayTypeSignature ||
            target is not ArrayTypeSignature ||
            !Mapper.TryGetType(candidate, out var candidateType) ||
            candidateType is not { IsArray: true } array)
        {
            return true;
        }

        return RuntimeTypeFacts.IsVector(array) || !IsVectorTarget(target);
    }

    // 判定数组目标是否为向量：模式构造的秩 1 目标无法表达下界，按向量处理。
    private bool IsVectorTarget(TypeSignature target)
    {
        return !Mapper.TryGetType(target, out var targetType) ||
               targetType is not { IsArray: true } array ||
               RuntimeTypeFacts.IsVector(array);
    }

    /// <summary>Gets the declared inheritance closure of a type, excluding the type itself.</summary>
    /// <param name="candidate">Specifies the candidate.</param>
    /// <param name="cancellationToken">Specifies the cancellation token.</param>
    /// <returns>Returns the known ancestors and the failure, if any, that stopped expansion.</returns>
    public InheritanceClosure GetInheritanceClosure(
        TypeSignature candidate,
        CancellationToken cancellationToken = default)
    {
        if (!Mapper.TryGetType(candidate, out var candidateType) ||
            candidateType is null)
        {
            return new InheritanceClosure(
                Array.Empty<TypeSignature>(),
                MissingNativeReference(candidate));
        }

        var closure = new List<TypeSignature>();
        try
        {
            var pending = new Queue<Type>();
            if (candidateType.BaseType is not null)
            {
                pending.Enqueue(candidateType.BaseType);
            }

            foreach (var implementation in candidateType.GetInterfaces())
            {
                pending.Enqueue(implementation);
            }

            var visited = new HashSet<Type>();
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = pending.Dequeue();
                if (!visited.Add(current)) continue;
                closure.Add(Mapper.MapType(current));
                if (current.BaseType is not null)
                {
                    pending.Enqueue(current.BaseType);
                }

                foreach (var implementation in current.GetInterfaces())
                {
                    pending.Enqueue(implementation);
                }
            }
        }
        catch (Exception exception) when (IsMetadataFailure(exception))
        {
            // 元数据读取失败：已产出的祖先仍有效，闭包标记为部分并携带失败诊断。
            return new InheritanceClosure(
                closure,
                ReflectionFailure(candidateType, exception));
        }

        return new InheritanceClosure(closure, null);
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

                return CustomAttributeData.GetCustomAttributes(type)
                    .Select(attribute => new AttributeSignature(
                        Mapper.MapType(attribute.AttributeType),
                        AttributeTargetKind.Type))
                    .ToArray();
            case AttributeTargetKind.Callable:
                return target.Callable!.Attributes;
            case AttributeTargetKind.Property:
                return target.Property!.Attributes;
            case AttributeTargetKind.Parameter:
                return target.Parameter!.Attributes;
            case AttributeTargetKind.Return:
                return target.Callable!.ReturnAttributes;
            default:
                return null;
        }
    }

    private static TypeRelationResult CompareUnqualified(
        TypeSignature actual,
        TypeSignature expected)
    {
        if (actual.Kind == TypeSignatureKind.Unsupported ||
            expected.Kind == TypeSignatureKind.Unsupported)
        {
            return StructuralTypeComparer.AreEqual(actual, expected);
        }

        if (actual.Kind != expected.Kind)
        {
            return TypeRelationResult.NotMatched;
        }

        return actual switch
        {
            NamedTypeSignature actualNamed
                when expected is NamedTypeSignature expectedNamed =>
                    CompareNamed(actualNamed, expectedNamed),
            ArrayTypeSignature actualArray
                when expected is ArrayTypeSignature expectedArray =>
                    actualArray.Rank == expectedArray.Rank
                        ? CompareUnqualified(
                            actualArray.ElementType,
                            expectedArray.ElementType)
                        : TypeRelationResult.NotMatched,
            _ => StructuralTypeComparer.AreEqual(actual, expected)
        };
    }

    private static TypeRelationResult CompareNamed(
        NamedTypeSignature actual,
        NamedTypeSignature expected)
    {
        if (!IdentityMatches(actual.Identity, expected.Identity) ||
            actual.TypeArguments.Count != expected.TypeArguments.Count)
        {
            return TypeRelationResult.NotMatched;
        }

        List<AspectNDiagnostic>? unknownDiagnostics = null;
        for (var index = 0; index < actual.TypeArguments.Count; index++)
        {
            var relation = CompareUnqualified(
                actual.TypeArguments[index],
                expected.TypeArguments[index]);
            if (relation.Status == MatchStatus.NotMatched) return relation;
            if (relation.Status == MatchStatus.Unknown)
            {
                unknownDiagnostics ??= [];
                unknownDiagnostics.AddRange(relation.Diagnostics);
            }
        }

        return unknownDiagnostics is null
            ? TypeRelationResult.Matched
            : TypeRelationResult.Unknown(unknownDiagnostics.ToArray());
    }

    private static bool IdentityMatches(
        TypeIdentity actual,
        TypeIdentity expected)
    {
        if ((!string.IsNullOrEmpty(expected.Assembly.Name) &&
             actual.Assembly != expected.Assembly) ||
            (!string.IsNullOrEmpty(expected.Namespace) &&
             !StringComparer.Ordinal.Equals(
                 actual.Namespace,
                 expected.Namespace)) ||
            !StringComparer.Ordinal.Equals(
                actual.MetadataName,
                expected.MetadataName) ||
            actual.GenericArity != expected.GenericArity ||
            (expected.Kind != TypeKind.Unknown &&
             actual.Kind != expected.Kind))
        {
            return false;
        }

        if (actual.ContainingType is null || expected.ContainingType is null)
        {
            return actual.ContainingType is null &&
                   expected.ContainingType is null;
        }

        return IdentityMatches(actual.ContainingType, expected.ContainingType);
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
                DiagnosticCodes.MissingBackendCapability,
                DiagnosticSeverity.Warning,
                $"Reflection metadata for '{source}' could not be read. " +
                $"{exception.GetType().Name}: {exception.Message}"));
    }
}
