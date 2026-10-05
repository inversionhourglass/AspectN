using System.Runtime.CompilerServices;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.AdapterInternals;

internal static class UnqualifiedStructuralComparer
{
    public static TypeRelationResult AreEqual(TypeSignature actual, TypeSignature expected)
    {
        var pending = new Stack<(TypeSignature Actual, TypeSignature Expected)>();
        var visited = new HashSet<(TypeSignature Actual, TypeSignature Expected)>(SignaturePairComparer.Instance);
        pending.Push((actual, expected));
        List<AspectNDiagnostic>? diagnostics = null;
        while (pending.Count != 0)
        {
            var pair = pending.Pop();
            if (!visited.Add(pair)) continue;
            var (left, right) = pair;
            if (left.Kind != TypeSignatureKind.Unsupported && right.Kind != TypeSignatureKind.Unsupported)
            {
                if (left.Kind != right.Kind) return TypeRelationResult.NotMatched;
                if (left is NamedTypeSignature a && right is NamedTypeSignature b)
                {
                    if (!IdentityMatches(a.Identity, b.Identity) || a.TypeArguments.Count != b.TypeArguments.Count)
                        return TypeRelationResult.NotMatched;
                    for (var index = a.TypeArguments.Count - 1; index >= 0; index--)
                        pending.Push((a.TypeArguments[index], b.TypeArguments[index]));
                    continue;
                }
                if (left is ArrayTypeSignature arrayA && right is ArrayTypeSignature arrayB)
                {
                    if (arrayA.Rank != arrayB.Rank || arrayA.Shape != arrayB.Shape)
                        return TypeRelationResult.NotMatched;
                    pending.Push((arrayA.ElementType, arrayB.ElementType));
                    continue;
                }
            }
            var relation = StructuralTypeComparer.AreEqual(left, right);
            if (relation.Status == MatchStatus.NotMatched) return relation;
            if (relation.Status != MatchStatus.Unknown) continue;
            diagnostics ??= [];
            diagnostics.AddRange(relation.Diagnostics);
        }
        return diagnostics is null ? TypeRelationResult.Matched : TypeRelationResult.Unknown(diagnostics.ToArray());
    }

    private sealed class SignaturePairComparer : IEqualityComparer<(TypeSignature Actual, TypeSignature Expected)>
    {
        public static readonly SignaturePairComparer Instance = new();

        public bool Equals((TypeSignature Actual, TypeSignature Expected) x, (TypeSignature Actual, TypeSignature Expected) y)
            => ReferenceEquals(x.Actual, y.Actual) && ReferenceEquals(x.Expected, y.Expected);

        public int GetHashCode((TypeSignature Actual, TypeSignature Expected) pair)
            => unchecked(RuntimeHelpers.GetHashCode(pair.Actual) * 397 ^ RuntimeHelpers.GetHashCode(pair.Expected));
    }

    private static bool IdentityMatches(TypeIdentity actual, TypeIdentity expected)
    {
        TypeIdentity? left = actual;
        TypeIdentity? right = expected;
        while (left is not null && right is not null)
        {
            if ((!string.IsNullOrEmpty(right.Assembly.Name) && left.Assembly != right.Assembly) ||
                (!string.IsNullOrEmpty(right.Namespace) && !StringComparer.Ordinal.Equals(left.Namespace, right.Namespace)) ||
                !StringComparer.Ordinal.Equals(left.MetadataName, right.MetadataName) ||
                left.GenericArity != right.GenericArity || (right.Kind != TypeKind.Unknown && left.Kind != right.Kind))
                return false;
            left = left.ContainingType;
            right = right.ContainingType;
        }
        return left is null && right is null;
    }
}
