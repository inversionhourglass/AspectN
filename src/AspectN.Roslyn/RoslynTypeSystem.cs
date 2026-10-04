using AspectN.AdapterInternals;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Microsoft.CodeAnalysis;
using CoreDiagnosticSeverity = AspectN.Diagnostics.DiagnosticSeverity;
using CoreTypeKind = AspectN.Metadata.TypeKind;

namespace AspectN.Roslyn;

/// <summary>Provides Roslyn-backed type and metadata operations for AspectN matching.</summary>
public sealed class RoslynTypeSystem : ITypeSystem
{
    /// <summary>Initializes a new instance of the RoslynTypeSystem class.</summary>
    /// <param name="compilation">Specifies the compilation.</param>
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

    /// <summary>Gets the compilation.</summary>
    public Compilation Compilation { get; }

    /// <summary>Gets the capabilities.</summary>
    public TypeSystemCapabilities Capabilities => TypeSystemCapabilities.All;

    /// <summary>Gets the mapper.</summary>
    public RoslynSignatureMapper Mapper { get; }

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
        if (structural.Status != MatchStatus.NotMatched) return structural;

        if (!Mapper.TryGetTypeSymbol(candidate, out var candidateSymbol) ||
            candidateSymbol is null)
        {
            return MissingNativeSymbol(candidate);
        }

        try
        {
            // + 只匹配声明的继承/实现关系；ClassifyCommonConversion 额外包含数组协变与泛型变型，
            // 与模式路径的合成目标语义不一致，统一按名义闭包遍历（Interfaces 含运行时合成的数组接口）。
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

    /// <summary>Gets the declared inheritance closure of a type, excluding the type itself.</summary>
    /// <param name="candidate">Specifies the candidate.</param>
    /// <param name="cancellationToken">Specifies the cancellation token.</param>
    /// <returns>Returns the known ancestors and the failure, if any, that stopped expansion.</returns>
    public InheritanceClosure GetInheritanceClosure(
        TypeSignature candidate,
        CancellationToken cancellationToken = default)
    {
        if (!Mapper.TryGetTypeSymbol(candidate, out var candidateSymbol) ||
            candidateSymbol is null)
        {
            return new InheritanceClosure(
                Array.Empty<TypeSignature>(),
                MissingNativeSymbol(candidate));
        }

        var closure = new List<TypeSignature>();
        try
        {
            var pending = new Queue<ITypeSymbol>();
            if (candidateSymbol.BaseType is not null)
            {
                pending.Enqueue(candidateSymbol.BaseType);
            }

            foreach (var implementation in candidateSymbol.Interfaces)
            {
                pending.Enqueue(implementation);
            }

            var visited = new HashSet<ITypeSymbol>(
                SymbolEqualityComparer.Default);
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

                foreach (var implementation in current.Interfaces)
                {
                    pending.Enqueue(implementation);
                }
            }
        }
        catch (Exception exception) when (IsSymbolFailure(exception))
        {
            // 符号读取失败：已产出的祖先仍有效，闭包标记为部分并携带失败诊断。
            return new InheritanceClosure(
                closure,
                SymbolFailure(candidateSymbol, exception));
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
        var metadataFailure = AttributeAvailability.GetFailure(target);
        if (metadataFailure is not null) return metadataFailure;
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
                    actualArray.Rank == expectedArray.Rank && actualArray.Shape == expectedArray.Shape
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
