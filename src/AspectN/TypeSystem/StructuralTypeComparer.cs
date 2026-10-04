using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;

namespace AspectN.TypeSystem;

/// <summary>Represents structural type comparer.</summary>
public static class StructuralTypeComparer
{
    /// <summary>Compares two values for structural equality.</summary>
    /// <param name="left">Specifies the left.</param>
    /// <param name="right">Specifies the right.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static TypeRelationResult AreEqual(
        TypeSignature left,
        TypeSignature right)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
#else
        if (left is null) throw new ArgumentNullException(nameof(left));
        if (right is null) throw new ArgumentNullException(nameof(right));
#endif

        if (left.Kind == TypeSignatureKind.Unsupported ||
            right.Kind == TypeSignatureKind.Unsupported)
        {
            return Unsupported(left, right);
        }

        if (left.Kind != right.Kind)
        {
            return TypeRelationResult.NotMatched;
        }

        return left switch
        {
            NamedTypeSignature leftNamed when right is NamedTypeSignature rightNamed =>
                CompareNamed(leftNamed, rightNamed),
            ArrayTypeSignature leftArray when right is ArrayTypeSignature rightArray =>
                CompareArray(leftArray, rightArray),
            GenericParameterTypeSignature leftParameter
                when right is GenericParameterTypeSignature rightParameter =>
                CompareGenericParameter(leftParameter, rightParameter),
            _ => Unsupported(left, right)
        };
    }

    private static TypeRelationResult CompareNamed(
        NamedTypeSignature left,
        NamedTypeSignature right)
    {
        if (left.Identity != right.Identity ||
            left.TypeArguments.Count != right.TypeArguments.Count)
        {
            return TypeRelationResult.NotMatched;
        }

        List<AspectNDiagnostic>? unknownDiagnostics = null;
        for (var index = 0; index < left.TypeArguments.Count; index++)
        {
            var result = AreEqual(
                left.TypeArguments[index],
                right.TypeArguments[index]);
            if (result.Status == MatchStatus.NotMatched)
            {
                return result;
            }

            if (result.Status == MatchStatus.Unknown)
            {
                unknownDiagnostics ??= [];
                unknownDiagnostics.AddRange(result.Diagnostics);
            }
        }

        return unknownDiagnostics is null
            ? TypeRelationResult.Matched
            : TypeRelationResult.Unknown(unknownDiagnostics.ToArray());
    }

    private static TypeRelationResult CompareArray(
        ArrayTypeSignature left,
        ArrayTypeSignature right)
    {
        return left.Rank == right.Rank && left.Shape == right.Shape
            ? AreEqual(left.ElementType, right.ElementType)
            : TypeRelationResult.NotMatched;
    }

    private static TypeRelationResult CompareGenericParameter(
        GenericParameterTypeSignature left,
        GenericParameterTypeSignature right)
    {
        var leftParameter = left.Parameter;
        var rightParameter = right.Parameter;
        return leftParameter.OwnerKind == rightParameter.OwnerKind &&
               StringComparer.Ordinal.Equals(
                   leftParameter.OwnerId,
                   rightParameter.OwnerId) &&
               leftParameter.OwnerAssembly == rightParameter.OwnerAssembly &&
               leftParameter.Position == rightParameter.Position
            ? TypeRelationResult.Matched
            : TypeRelationResult.NotMatched;
    }

    private static TypeRelationResult Unsupported(
        TypeSignature left,
        TypeSignature right)
    {
        var unsupported = left.Kind == TypeSignatureKind.Unsupported
            ? left
            : right;
        var displayName = unsupported is UnsupportedTypeSignature signature
            ? signature.DisplayName
            : unsupported.GetType().Name;
        return TypeRelationResult.Unknown(
            new AspectNDiagnostic(
                DiagnosticCodes.MissingBackendCapability,
                DiagnosticSeverity.Warning,
                $"Structural identity is unavailable for type '{displayName}'."));
    }
}
