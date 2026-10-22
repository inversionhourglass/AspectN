using System.Runtime.CompilerServices;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;

namespace AspectN.TypeSystem;

/// <summary>Compares backend-neutral type structures without resolving native metadata.</summary>
public static class StructuralTypeComparer
{
    /// <summary>Compares named identities, generic slots, and array elements and shapes for strict equality.</summary>
    /// <param name="left">The first type signature.</param>
    /// <param name="right">The second type signature.</param>
    /// <param name="options">Immutable limits for this independent comparison; null uses match defaults.</param>
    /// <param name="cancellationToken">Cancels this independent comparison.</param>
    /// <returns>Matched for equal structures, NotMatched for a definite difference, or Unknown with diagnostics for unsupported facts or exhausted comparison limits.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="left"/> or <paramref name="right"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    /// <remarks>Empty identity qualifiers and TypeKind.Unknown are values, not directional query wildcards. This operation does not test inheritance or CLR conversions.</remarks>
    public static TypeRelationResult AreEqual(
        TypeSignature left,
        TypeSignature right, AspectNMatchOptions? options = null, CancellationToken cancellationToken = default)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
#else
        if (left is null) throw new ArgumentNullException(nameof(left));
        if (right is null) throw new ArgumentNullException(nameof(right));
#endif

        var context = new TypeSystemQueryContext(options ?? new AspectNMatchOptions(), cancellationToken);
        try
        {
            return AreEqual(left, right, context);
        }
        catch (AspectNLimitException exhausted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return TypeRelationResult.Unknown(exhausted.Diagnostic);
        }
    }

    internal static TypeRelationResult AreEqual(TypeSignature left, TypeSignature right, TypeSystemQueryContext context, bool unqualified = false)
    {
        context.ThrowIfExhausted();
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
#else
        if (left is null) throw new ArgumentNullException(nameof(left));
        if (right is null) throw new ArgumentNullException(nameof(right));
#endif
        var failure = MetadataLimitGuard.GetFailure(left, context) ?? MetadataLimitGuard.GetFailure(right, context);
        if (failure is not null) return TypeRelationResult.Unknown(failure);
        var pending = new Stack<(TypeSignature Left, TypeSignature Right, int NextIndex)>();
        var visited = new HashSet<(TypeSignature Left, TypeSignature Right)>(SignaturePairComparer.Instance);
        pending.Push((left, right, -1));
        List<AspectNDiagnostic>? unknownDiagnostics = null;
        // 使用显式工作栈，保持 NotMatched 优先于其他分支的 Unknown。
        while (pending.Count != 0)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var pair = pending.Pop();
            if (pair.NextIndex >= 0)
            {
                var a = (NamedTypeSignature)pair.Left;
                var b = (NamedTypeSignature)pair.Right;
                if (pair.NextIndex + 1 < a.TypeArguments.Count) pending.Push((a, b, pair.NextIndex + 1));
                pending.Push((a.TypeArguments[pair.NextIndex], b.TypeArguments[pair.NextIndex], -1));
                continue;
            }
            context.SpendWork();
            // 共享子图只比较一次；引用相同的 Unsupported 节点仍须保留 Unknown。
            if (!visited.Add((pair.Left, pair.Right))) continue;
            if (pair.Left.Kind == TypeSignatureKind.Unsupported || pair.Right.Kind == TypeSignatureKind.Unsupported)
            {
                unknownDiagnostics ??= [];
                unknownDiagnostics.AddRange(Unsupported(pair.Left, pair.Right, context).Diagnostics);
                continue;
            }
            if (pair.Left.Kind != pair.Right.Kind) return TypeRelationResult.NotMatched;
            switch (pair.Left)
            {
                case NamedTypeSignature a when pair.Right is NamedTypeSignature b:
                    if (!(unqualified ? IdentityMatches(a.Identity, b.Identity, context) : context.IdentitiesEqual(a.Identity, b.Identity)) || a.TypeArguments.Count != b.TypeArguments.Count)
                        return TypeRelationResult.NotMatched;
                    if (a.TypeArguments.Count > 0) pending.Push((a, b, 0));
                    break;
                case ArrayTypeSignature a when pair.Right is ArrayTypeSignature b:
                    if (a.Rank != b.Rank || a.Shape != b.Shape) return TypeRelationResult.NotMatched;
                    pending.Push((a.ElementType, b.ElementType, -1));
                    break;
                case GenericParameterTypeSignature a when pair.Right is GenericParameterTypeSignature b:
                    if (CompareGenericParameter(a, b, context).Status == MatchStatus.NotMatched)
                        return TypeRelationResult.NotMatched;
                    break;
                default:
                    unknownDiagnostics ??= [];
                    unknownDiagnostics.AddRange(Unsupported(pair.Left, pair.Right, context).Diagnostics);
                    break;
            }
        }

        return unknownDiagnostics is null
            ? TypeRelationResult.Matched
            : TypeRelationResult.Unknown(unknownDiagnostics.ToArray());
    }

    private static bool IdentityMatches(TypeIdentity? actual, TypeIdentity? expected, TypeSystemQueryContext context)
    {
        while (actual is not null && expected is not null)
        {
            context.SpendWork();
            if ((expected.Assembly.Name.Length > 0 && !context.AssembliesEqual(actual.Assembly, expected.Assembly)) ||
                (expected.Namespace.Length > 0 && !context.StringsEqual(actual.Namespace, expected.Namespace)) ||
                !context.StringsEqual(actual.MetadataName, expected.MetadataName) || actual.GenericArity != expected.GenericArity ||
                (expected.Kind != TypeKind.Unknown && actual.Kind != expected.Kind)) return false;
            actual = actual.ContainingType;
            expected = expected.ContainingType;
        }
        return actual is null && expected is null;
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
        GenericParameterTypeSignature right, TypeSystemQueryContext context)
    {
        var leftParameter = left.Parameter;
        var rightParameter = right.Parameter;
        return leftParameter.OwnerKind == rightParameter.OwnerKind &&
               context.StringsEqual(
                   leftParameter.OwnerId,
                   rightParameter.OwnerId) &&
               context.AssembliesEqual(leftParameter.OwnerAssembly, rightParameter.OwnerAssembly) &&
               leftParameter.Position == rightParameter.Position
            ? TypeRelationResult.Matched
            : TypeRelationResult.NotMatched;
    }

    private static TypeRelationResult Unsupported(
        TypeSignature left,
        TypeSignature right, TypeSystemQueryContext context)
    {
        var unsupported = left.Kind == TypeSignatureKind.Unsupported
            ? left
            : right;
        var displayName = unsupported is UnsupportedTypeSignature signature
            ? signature.DisplayName
            : unsupported.GetType().Name;
        context.SpendWork(Math.Max(1, displayName.Length));
        return TypeRelationResult.Unknown(
            new AspectNDiagnostic(
                DiagnosticCodes.MissingBackendCapability,
                DiagnosticSeverity.Warning,
                $"Structural identity is unavailable for type '{displayName}'."));
    }
}
