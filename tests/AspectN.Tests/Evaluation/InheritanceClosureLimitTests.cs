using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;
using AspectN.TypeSystem;

namespace AspectN.Tests.Evaluation;

public sealed class InheritanceClosureLimitTests
{
    [Theory]
    [InlineData("argument-depth")]
    [InlineData("containing-depth")]
    [InlineData("declared-arity")]
    [InlineData("argument-arity")]
    [InlineData("containing-arity")]
    public void Match_OverLimitAncestor_AgreesWithDirectCandidate(string shape)
    {
        var item = CreateCase(shape, true);
        var backend = Backend(item.Ancestor);

        AssertResult(Match(item.Pattern, item.Ancestor, backend), MatchStatus.Unknown, item.Code);
        AssertResult(Match(item.Pattern, Named("Leaf"), backend), MatchStatus.Unknown, item.Code);
    }

    [Theory]
    [InlineData("argument-depth")]
    [InlineData("containing-depth")]
    [InlineData("declared-arity")]
    [InlineData("argument-arity")]
    [InlineData("containing-arity")]
    public void Match_OverLimitAndKnownMatchingAncestors_EitherOrderMatchesWithDiagnostic(string shape)
    {
        var item = CreateCase(shape, true);
        var known = shape == "containing-depth" ? Named("L64")
            : shape == "containing-arity" ? Nested(1) : Construct("Box", Named("String"));
        foreach (var ancestors in new[] { new[] { item.Ancestor, known }, new[] { known, item.Ancestor } })
        {
            AssertResult(Match(item.Pattern, Named("Leaf"), Backend(ancestors)), MatchStatus.Matched, item.Code);
        }
    }

    [Theory]
    [InlineData("argument-depth")]
    [InlineData("containing-depth")]
    [InlineData("declared-arity")]
    [InlineData("argument-arity")]
    [InlineData("containing-arity")]
    public void Match_AncestorAtLimit_RemainsUsable(string shape)
    {
        var item = CreateCase(shape, false);
        Assert.Equal(MatchStatus.Matched, Match("*", item.Ancestor, Backend()).Status);
        var result = Match(item.Pattern, Named("Leaf"), Backend(item.Ancestor));

        Assert.Equal(shape == "containing-depth" ? MatchStatus.NotMatched : MatchStatus.Matched, result.Status);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData(false, MatchStatus.Unknown)]
    [InlineData(true, MatchStatus.Matched)]
    public void Match_LaterVariableConstraint_PreservesUnknownAndKnownAlternatives(bool knownSurvives, MatchStatus expected)
    {
        var item = CreateCase("argument-depth", true);
        var candidate = Construct("Carrier", Named("String"), Named("Number"));
        var source = "Box<$T>+ && " + (knownSurvives ? "Carrier<$T,*>" : "Carrier<*,$T>");
        var backend = Backend(item.Ancestor, Construct("Box", Named("String")));

        AssertResult(Match(source, candidate, backend), expected, item.Code);
    }

    [Fact]
    public void Match_NegatedOverLimitAncestor_RemainsUnknown()
    {
        var item = CreateCase("argument-depth", true);
        AssertResult(Match("!Box<?>+", Named("Leaf"), Backend(item.Ancestor)), MatchStatus.Unknown, item.Code);
    }

    [Theory]
    [InlineData("argument-depth")]
    [InlineData("containing-depth")]
    [InlineData("declared-arity")]
    [InlineData("argument-arity")]
    [InlineData("containing-arity")]
    public void Match_MemberReturnRelation_RejectsOverLimitAncestor(string shape)
    {
        var item = CreateCase(shape, true);
        var candidate = new MethodSignature("M", MethodKind.OrdinaryMethod, Accessibility.Public, false, false,
            Named("Owner"), new TypeUseSignature(Named("Leaf"), RefKind.None, TypeUseKind.Return), [], [], [], [], []);
        var compilation = new AspectNEngine().CompileMemberExpression("method(" + item.Pattern.Replace("$T", "*") + " M())");
        Assert.True(compilation.TryGetMemberPattern(out var pattern));

        AssertResult(pattern.Match(candidate, Backend(item.Ancestor)), MatchStatus.Unknown, item.Code);
    }

    [Fact]
    public void Match_ClosureFailureAndOverLimitAncestor_PreservesBothDiagnostics()
    {
        var item = CreateCase("argument-depth", true);
        var original = new AspectNDiagnostic("AN3001", DiagnosticSeverity.Warning, "第三方闭包不完整。");
        var backend = new StubTypeSystem
        {
            Closure = (_, _) => new InheritanceClosure([item.Ancestor, Construct("Box", Named("String"))],
                TypeRelationResult.Unknown(original))
        };
        var result = Match(item.Pattern, Named("Leaf"), backend);

        AssertResult(result, MatchStatus.Matched, item.Code);
        Assert.Contains(original, result.Diagnostics);
    }

    [Fact]
    public void Match_RepeatedOverLimitAncestor_DeduplicatesLimitDiagnostic()
    {
        var item = CreateCase("argument-depth", true);
        var result = Match(item.Pattern, Named("Leaf"), Backend(item.Ancestor, item.Ancestor));

        AssertResult(result, MatchStatus.Unknown, item.Code);
        Assert.Single(result.Diagnostics);
    }

    [Fact]
    public void Match_CancelledAfterClosureQuery_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var item = CreateCase("argument-depth", true);
        var backend = new StubTypeSystem
        {
            Closure = (_, token) =>
            {
                Assert.Equal(cancellation.Token, token);
                cancellation.Cancel();
                return new InheritanceClosure([item.Ancestor], null);
            }
        };
        Assert.Throws<OperationCanceledException>(() => Match(item.Pattern, Named("Leaf"), backend, cancellation.Token));
    }

    [Fact]
    public void Match_ConcreteRelationshipTarget_UsesBackendRelation()
    {
        var backend = new StubTypeSystem
        {
            Assignability = (_, _, _) => TypeRelationResult.Matched,
            Closure = (_, _) => throw new InvalidOperationException("具体目标不应走闭包模式求值。")
        };
        var result = Match("Base+", Named("Leaf"), backend);

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.Empty(result.Diagnostics);
    }

    private static LimitCase CreateCase(string shape, bool exceeded)
    {
        switch (shape)
        {
            case "argument-depth":
                TypeSignature argument = Named("String");
                for (var index = 0; index < (exceeded ? 63 : 62); index++) argument = new ArrayTypeSignature(argument, 1);
                return new("Box<$T>+", Construct("Box", argument), "AN3005");
            case "containing-depth":
                TypeIdentity? owner = null;
                for (var index = 0; index < (exceeded ? 65 : 64); index++)
                    owner = new TypeIdentity(new AssemblyIdentity("Example"), index == 0 ? "Example" : "", "L" + index,
                        0, TypeKind.Class, owner);
                return new("L6*<?>+", new NamedTypeSignature(owner!), "AN3005");
            case "declared-arity":
                return new("Box<?>+", Named("Box", exceeded ? 65 : 64), "AN3006");
            case "argument-arity":
                return new("Box<?>+", Construct("Box", Enumerable.Repeat<TypeSignature>(Named("String"), exceeded ? 65 : 64).ToArray()), "AN3006");
            case "containing-arity":
                return new("Outer<?>/Inner<$T>+", Nested(exceeded ? 65 : 63), "AN3006");
            default:
                throw new ArgumentOutOfRangeException(nameof(shape));
        }
    }

    private static NamedTypeSignature Named(string name, int arity = 0) => new(
        new TypeIdentity(new AssemblyIdentity("Example"), "Example", name, arity, TypeKind.Class));

    private static NamedTypeSignature Construct(string name, params TypeSignature[] arguments) => new(
        Named(name, arguments.Length).Identity, arguments);

    private static NamedTypeSignature Nested(int outerArity) => new(
        new TypeIdentity(new AssemblyIdentity("Example"), "", "Inner", 1, TypeKind.Class, Named("Outer", outerArity).Identity));

    private static StubTypeSystem Backend(params TypeSignature[] ancestors) => new()
    {
        Closure = (_, _) => new InheritanceClosure(ancestors, null)
    };

    private static MatchResult Match(string source, TypeSignature candidate, ITypeSystem backend,
        CancellationToken cancellationToken = default)
    {
        Assert.True(new AspectNEngine().CompileTypePattern(source).TryGetTypePattern(out var pattern));
        return pattern.Match(candidate, backend, cancellationToken);
    }

    private static void AssertResult(MatchResult result, MatchStatus expected, string code)
    {
        Assert.Equal(expected, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    private sealed record LimitCase(string Pattern, NamedTypeSignature Ancestor, string Code);
}
