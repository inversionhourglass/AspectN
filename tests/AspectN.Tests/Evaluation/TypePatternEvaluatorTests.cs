using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;
using AspectN.TypeSystem;

namespace AspectN.Tests.Evaluation;

public sealed class TypePatternEvaluatorTests
{
    [Fact]
    public void Match_OmittedNamespace_MatchesNamedTypeInAnyNamespace()
    {
        var result = Match("Service", Named("Example.Services", "Service"));

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_NamespaceAndNameWildcards_MatchExpectedType()
    {
        var candidate = Named(
            "Example.Features.Internal",
            "OrderService");

        var result = Match("Example..Internal.*Service", candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_NestedSlash_MatchesContainingTypeChain()
    {
        var outer = Identity("Example", "Outer");
        var inner = new NamedTypeSignature(
            Identity(string.Empty, "Inner", containingType: outer));

        var result = Match("Outer/Inner", inner);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_StandardNameComparison_IsCaseSensitive()
    {
        var result = Match("Service", Named("Example", "service"));

        Assert.Equal(MatchStatus.NotMatched, result.Status);
    }

    [Fact]
    public void Match_PrimitiveAlias_MatchesSystemType()
    {
        var result = Match(
            "int",
            Named("System", "Int32", TypeKind.Struct));

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_ArrayRankAndNesting_MustMatchExactly()
    {
        var element = Named("System", "String");
        var vector = new ArrayTypeSignature(element, 1);
        var matrix = new ArrayTypeSignature(element, 2);
        var jagged = new ArrayTypeSignature(vector, 1);

        Assert.Equal(MatchStatus.Matched, Match("string[]", vector).Status);
        Assert.Equal(MatchStatus.NotMatched, Match("string[]", matrix).Status);
        Assert.Equal(MatchStatus.Matched, Match("string[][]", jagged).Status);
    }

    [Fact]
    public void Match_MixedArrayRanks_UsesTextualOuterToInnerOrder()
    {
        var candidate = new ArrayTypeSignature(
            new ArrayTypeSignature(
                new ArrayTypeSignature(
                    Named("System", "Int32", TypeKind.Struct),
                    3),
                1),
            2);

        var result = Match("int[,][][,,]", candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }
    [Fact]
    public void Match_GroupingAndBooleanOperators_UseThreeStateLogic()
    {
        var candidate = Named("System", "Int32", TypeKind.Struct);

        Assert.Equal(MatchStatus.Matched, Match("((string || int))", candidate).Status);
        Assert.Equal(
            MatchStatus.Matched,
            Match("string || int", candidate).Status);
        Assert.Equal(
            MatchStatus.Matched,
            Match("int && *", candidate).Status);
        Assert.Equal(
            MatchStatus.Matched,
            Match("!string", candidate).Status);
    }

    [Theory]
    [InlineData(MatchStatus.Matched)]
    [InlineData(MatchStatus.NotMatched)]
    [InlineData(MatchStatus.Unknown)]
    public void Match_AssignablePattern_PropagatesTypeSystemResult(
        MatchStatus relationshipStatus)
    {
        TypeSignature? observedTarget = null;
        var relation = Relation(relationshipStatus);
        var typeSystem = new StubTypeSystem
        {
            Assignability = (_, target, _) =>
            {
                observedTarget = target;
                return relation;
            }
        };

        var result = Match(
            "System.IDisposable+",
            Named("Example", "Resource"),
            typeSystem);

        Assert.Equal(relationshipStatus, result.Status);
        var namedTarget = Assert.IsType<NamedTypeSignature>(observedTarget);
        Assert.Equal("System.IDisposable", namedTarget.Identity.FullMetadataName);
        Assert.Equal(relation.Diagnostics.Count, result.Diagnostics.Count);
    }

    [Fact]
    public void Match_UnsupportedCandidate_ReturnsUnknownWithDiagnostic()
    {
        var candidate = new UnsupportedTypeSignature(
            "delegate*<void>",
            "Function pointers are unavailable.");

        var result = Match("System.Object", candidate);

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void Match_CanceledAssignability_ThrowsCancellation()
    {
        var pattern = Compile("System.IDisposable+");
        var cancellation = new CancellationToken(canceled: true);

        Assert.Throws<OperationCanceledException>(
            () => pattern.Match(
                Named("Example", "Resource"),
                new StubTypeSystem(),
                cancellation));
    }

    private static MatchResult Match(
        string source,
        TypeSignature candidate,
        StubTypeSystem? typeSystem = null)
    {
        return Compile(source).Match(
            candidate,
            typeSystem ?? new StubTypeSystem());
    }

    private static CompiledTypePattern Compile(string source)
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseTypePattern(source));
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        return pattern!;
    }

    private static TypeRelationResult Relation(MatchStatus status)
    {
        return status switch
        {
            MatchStatus.Matched => TypeRelationResult.Matched,
            MatchStatus.NotMatched => TypeRelationResult.NotMatched,
            _ => TypeRelationResult.Unknown(
                new AspectNDiagnostic(
                    DiagnosticCodes.MissingBackendCapability,
                    DiagnosticSeverity.Warning,
                    "Assignability metadata is unavailable."))
        };
    }

    private static NamedTypeSignature Named(
        string @namespace,
        string name,
        TypeKind kind = TypeKind.Class)
    {
        return new NamedTypeSignature(Identity(@namespace, name, kind: kind));
    }

    private static TypeIdentity Identity(
        string @namespace,
        string name,
        int genericArity = 0,
        TypeIdentity? containingType = null,
        TypeKind kind = TypeKind.Class)
    {
        return new TypeIdentity(
            new AssemblyIdentity("Example.Assembly"),
            @namespace,
            name,
            genericArity,
            kind,
            containingType);
    }
}
