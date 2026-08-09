using System.Reflection;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Reflection;

public sealed class ReflectionTypeSystem : ITypeSystem
{
    public ReflectionTypeSystem()
    {
        Mapper = new ReflectionSignatureMapper();
    }

    public TypeSystemCapabilities Capabilities => TypeSystemCapabilities.All;

    public ReflectionSignatureMapper Mapper { get; }

    public TypeRelationResult AreEqual(
        TypeSignature left,
        TypeSignature right)
    {
        return StructuralTypeComparer.AreEqual(left, right);
    }

    public TypeRelationResult IsAssignableTo(
        TypeSignature candidate,
        TypeSignature target,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var structural = CompareUnqualified(candidate, target);
        if (structural.Status != MatchStatus.NotMatched) return structural;

        if (!Mapper.TryGetType(candidate, out var candidateType) ||
            candidateType is null)
        {
            return MissingNativeReference(candidate);
        }

        try
        {
            if (Mapper.TryGetType(target, out var targetType) &&
                targetType is not null)
            {
                return targetType.IsAssignableFrom(candidateType)
                    ? TypeRelationResult.Matched
                    : TypeRelationResult.NotMatched;
            }

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
