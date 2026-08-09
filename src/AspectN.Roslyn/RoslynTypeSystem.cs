using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Microsoft.CodeAnalysis;
using CoreDiagnosticSeverity = AspectN.Diagnostics.DiagnosticSeverity;
using CoreTypeKind = AspectN.Metadata.TypeKind;

namespace AspectN.Roslyn;

public sealed class RoslynTypeSystem : ITypeSystem
{
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

    public Compilation Compilation { get; }

    public TypeSystemCapabilities Capabilities => TypeSystemCapabilities.All;

    public RoslynSignatureMapper Mapper { get; }

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

        if (!Mapper.TryGetTypeSymbol(candidate, out var candidateSymbol) ||
            candidateSymbol is null)
        {
            return MissingNativeSymbol(candidate);
        }

        try
        {
            if (Mapper.TryGetTypeSymbol(target, out var targetSymbol) &&
                targetSymbol is not null)
            {
                return IsNativeAssignable(
                    candidateSymbol,
                    targetSymbol,
                    cancellationToken)
                        ? TypeRelationResult.Matched
                        : TypeRelationResult.NotMatched;
            }

            var pending = new Queue<ITypeSymbol>();
            var visited = new HashSet<ITypeSymbol>(
                SymbolEqualityComparer.Default);
            pending.Enqueue(candidateSymbol);
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

                foreach (var implementation in current.Interfaces)
                {
                    pending.Enqueue(implementation);
                }
            }

            return TypeRelationResult.NotMatched;
        }
        catch (Exception exception) when (IsSymbolFailure(exception))
        {
            return SymbolFailure(candidateSymbol, exception);
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

    private bool IsNativeAssignable(
        ITypeSymbol candidate,
        ITypeSymbol target,
        CancellationToken cancellationToken)
    {
        var conversion = Compilation.ClassifyCommonConversion(
            candidate,
            target);
        if (conversion.Exists && conversion.IsImplicit &&
            (conversion.IsIdentity || conversion.IsReference))
        {
            return true;
        }

        if (target is INamedTypeSymbol nullable &&
            nullable.OriginalDefinition.SpecialType ==
                SpecialType.System_Nullable_T &&
            nullable.TypeArguments.Length == 1 &&
            SymbolEqualityComparer.Default.Equals(
                candidate,
                nullable.TypeArguments[0]))
        {
            return true;
        }

        var pending = new Queue<ITypeSymbol>();
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        pending.Enqueue(candidate);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Dequeue();
            if (!visited.Add(current)) continue;
            if (SymbolEqualityComparer.Default.Equals(current, target))
            {
                return true;
            }

            if (current.BaseType is not null)
            {
                pending.Enqueue(current.BaseType);
            }

            foreach (var implementation in current.Interfaces)
            {
                pending.Enqueue(implementation);
            }
        }

        return false;
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

                return typeSymbol.GetAttributes()
                    .Where(attribute => attribute.AttributeClass is not null)
                    .Select(attribute => new AttributeSignature(
                        Mapper.MapType(attribute.AttributeClass!),
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
            (expected.Kind != CoreTypeKind.Unknown &&
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

    private static bool IsSymbolFailure(Exception exception)
    {
        return exception is InvalidOperationException or ArgumentException;
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
        return TypeRelationResult.Unknown(
            new AspectNDiagnostic(
                DiagnosticCodes.MissingBackendCapability,
                CoreDiagnosticSeverity.Warning,
                $"Roslyn symbol data for '{source}' could not be read. " +
                $"{exception.GetType().Name}: {exception.Message}"));
    }
}
