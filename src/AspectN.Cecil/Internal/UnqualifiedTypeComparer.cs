using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Cecil.Internal;

internal static class UnqualifiedTypeComparer
{
    public static TypeRelationResult AreEqual(
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
                    actualArray.Rank == expectedArray.Rank && actualArray.Shape == expectedArray.Shape
                        ? AreEqual(
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
            var relation = AreEqual(
                actual.TypeArguments[index],
                expected.TypeArguments[index]);
            if (relation.Status == MatchStatus.NotMatched)
            {
                return relation;
            }

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

        return IdentityMatches(
            actual.ContainingType,
            expected.ContainingType);
    }
}
