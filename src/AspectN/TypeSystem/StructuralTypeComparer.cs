using System.Runtime.CompilerServices;
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

        var pending = new Stack<(TypeSignature Left, TypeSignature Right)>();
        var visited = new HashSet<(TypeSignature Left, TypeSignature Right)>(SignaturePairComparer.Instance);
        pending.Push((left, right));
        List<AspectNDiagnostic>? unknownDiagnostics = null;
        // 使用显式工作栈，保持 NotMatched 优先于其他分支的 Unknown。
        while (pending.Count != 0)
        {
            var pair = pending.Pop();
            // 共享子图只比较一次；引用相同的 Unsupported 节点仍须保留 Unknown。
            if (!visited.Add(pair)) continue;
            if (pair.Left.Kind == TypeSignatureKind.Unsupported || pair.Right.Kind == TypeSignatureKind.Unsupported)
            {
                unknownDiagnostics ??= [];
                unknownDiagnostics.AddRange(Unsupported(pair.Left, pair.Right).Diagnostics);
                continue;
            }
            if (pair.Left.Kind != pair.Right.Kind) return TypeRelationResult.NotMatched;
            switch (pair.Left)
            {
                case NamedTypeSignature a when pair.Right is NamedTypeSignature b:
                    if (a.Identity != b.Identity || a.TypeArguments.Count != b.TypeArguments.Count)
                        return TypeRelationResult.NotMatched;
                    for (var index = a.TypeArguments.Count - 1; index >= 0; index--)
                        pending.Push((a.TypeArguments[index], b.TypeArguments[index]));
                    break;
                case ArrayTypeSignature a when pair.Right is ArrayTypeSignature b:
                    if (a.Rank != b.Rank || a.Shape != b.Shape) return TypeRelationResult.NotMatched;
                    pending.Push((a.ElementType, b.ElementType));
                    break;
                case GenericParameterTypeSignature a when pair.Right is GenericParameterTypeSignature b:
                    if (CompareGenericParameter(a, b).Status == MatchStatus.NotMatched)
                        return TypeRelationResult.NotMatched;
                    break;
                default:
                    unknownDiagnostics ??= [];
                    unknownDiagnostics.AddRange(Unsupported(pair.Left, pair.Right).Diagnostics);
                    break;
            }
        }

        return unknownDiagnostics is null
            ? TypeRelationResult.Matched
            : TypeRelationResult.Unknown(unknownDiagnostics.ToArray());
    }

    private sealed class SignaturePairComparer : IEqualityComparer<(TypeSignature Left, TypeSignature Right)>
    {
        public static readonly SignaturePairComparer Instance = new();

        public bool Equals((TypeSignature Left, TypeSignature Right) x, (TypeSignature Left, TypeSignature Right) y)
            => ReferenceEquals(x.Left, y.Left) && ReferenceEquals(x.Right, y.Right);

        public int GetHashCode((TypeSignature Left, TypeSignature Right) pair)
            => unchecked(RuntimeHelpers.GetHashCode(pair.Left) * 397 ^ RuntimeHelpers.GetHashCode(pair.Right));
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
