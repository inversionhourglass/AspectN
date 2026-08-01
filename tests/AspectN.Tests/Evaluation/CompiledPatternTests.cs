using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

public sealed class CompiledPatternTests
{
    [Fact]
    public void TryGetTypePattern_ValidCompilation_ReturnsImmutablePlan()
    {
        var tree = AspectNParser.ParseTypePattern("$T && $T");
        var compilation = AspectNCompilation.Create(tree);

        var success = compilation.TryGetTypePattern(out var pattern);

        Assert.True(success);
        Assert.NotNull(pattern);
        Assert.Equal(AspectNLanguageVersion.Standard, pattern!.LanguageVersion);
        Assert.Same(compilation.Options, pattern.Options);
    }

    [Fact]
    public void TryGetMemberPattern_ValidCompilation_ReturnsMemberPlanOnly()
    {
        var tree = AspectNParser.ParseMemberExpression(
            "method(* Service.Get(..))");
        var compilation = AspectNCompilation.Create(tree);

        Assert.True(compilation.TryGetMemberPattern(out var memberPattern));
        Assert.NotNull(memberPattern);
        Assert.False(compilation.TryGetTypePattern(out var typePattern));
        Assert.Null(typePattern);
    }

    [Fact]
    public void TryGetPattern_InvalidCompilation_ReturnsFalse()
    {
        var tree = AspectNParser.ParseTypePattern("!$T");
        var compilation = AspectNCompilation.Create(tree);

        Assert.False(compilation.TryGetTypePattern(out var pattern));
        Assert.Null(pattern);
    }

    [Fact]
    public async Task Match_ConcurrentCandidates_DoNotShareVariableBindings()
    {
        var pattern = CompileTypePattern("$T && $T");
        var typeSystem = new StubTypeSystem();
        var first = Named("First");
        var second = Named("Second");
        var tasks = Enumerable.Range(0, 100)
            .Select(index => Task.Run(() =>
                pattern.Match(
                    index % 2 == 0 ? first : second,
                    typeSystem)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(
            results,
            result => Assert.Equal(MatchStatus.Matched, result.Status));
    }

    [Fact]
    public async Task Match_ConcurrentUnknowns_DoNotShareDiagnostics()
    {
        var pattern = CompileTypePattern("$T && $T");
        var typeSystem = new StubTypeSystem();
        var unsupported = new UnsupportedTypeSignature(
            "delegate*<void>",
            "Function pointers are unavailable.");
        var tasks = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => pattern.Match(unsupported, typeSystem)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(
            results,
            result =>
            {
                Assert.Equal(MatchStatus.Unknown, result.Status);
                Assert.Single(result.Diagnostics);
            });
    }

    private static CompiledTypePattern CompileTypePattern(string source)
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseTypePattern(source));
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        return pattern!;
    }

    private static NamedTypeSignature Named(string name)
    {
        return new NamedTypeSignature(
            new TypeIdentity(
                new AssemblyIdentity("Example.Assembly"),
                "Example",
                name,
                0,
                TypeKind.Class));
    }
}
