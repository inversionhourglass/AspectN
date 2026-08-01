using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

public sealed class PatternVariableTests
{
    [Fact]
    public void Match_RepeatedVariable_RequiresTypeEquality()
    {
        var definition = Definition("Pair", 2);
        var equal = Construct(
            definition,
            Named("String"),
            Named("String"));
        var unequal = Construct(
            definition,
            Named("String"),
            Named("Int32", TypeKind.Struct));

        Assert.Equal(
            MatchStatus.Matched,
            Match("Pair<$T, $T>", equal).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            Match("Pair<$T, $T>", unequal).Status);
    }

    [Fact]
    public void Match_And_SharesBindingEnvironment()
    {
        var candidate = Construct(
            Definition("Pair", 2),
            Named("String"),
            Named("String"));

        var result = Match(
            "Pair<$T, *> && Pair<*, $T>",
            candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_Or_UsesIsolatedCopiesOfExistingBindings()
    {
        var candidate = Construct(
            Definition("Pair", 2),
            Named("String"),
            Named("Int32", TypeKind.Struct));

        var result = Match(
            "Pair<$T, *> && (Pair<*, $T> || Pair<$T, *>)",
            candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_Negation_DoesNotMutateOuterBindings()
    {
        var candidate = Construct(
            Definition("Pair", 2),
            Named("String"),
            Named("Int32", TypeKind.Struct));

        var result = Match(
            "Pair<$T, *> && !Pair<*, $T> && Pair<$T, *>",
            candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_RepeatedUnsupportedVariable_PropagatesUnknown()
    {
        var unsupported = new UnsupportedTypeSignature(
            "delegate*<void>",
            "Function pointers are unavailable.");
        var candidate = Construct(
            Definition("Pair", 2),
            unsupported,
            unsupported);

        var result = Match("Pair<$T, $T>", candidate);

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Single(result.Diagnostics);
    }

    [Fact]
    public async Task Match_ConcurrentEvaluations_IsolateBindings()
    {
        var pattern = Compile("Pair<$T, $T>");
        var definition = Definition("Pair", 2);
        var first = Construct(definition, Named("String"), Named("String"));
        var second = Construct(
            definition,
            Named("Int32", TypeKind.Struct),
            Named("Int32", TypeKind.Struct));
        var typeSystem = new StubTypeSystem();
        var tasks = Enumerable.Range(0, 100)
            .Select(index => Task.Run(() => pattern.Match(
                index % 2 == 0 ? first : second,
                typeSystem)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(
            results,
            result => Assert.Equal(MatchStatus.Matched, result.Status));
    }

    private static MatchResult Match(
        string source,
        NamedTypeSignature candidate)
    {
        return Compile(source).Match(candidate, new StubTypeSystem());
    }

    private static CompiledTypePattern Compile(string source)
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseTypePattern(source));
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        return pattern!;
    }

    private static NamedTypeSignature Construct(
        NamedTypeSignature definition,
        params TypeSignature[] arguments)
    {
        return new NamedTypeSignature(
            definition.Identity,
            arguments,
            definition);
    }

    private static NamedTypeSignature Definition(string name, int arity)
    {
        return new NamedTypeSignature(
            new TypeIdentity(
                new AssemblyIdentity("Example.Assembly"),
                "Example",
                name,
                arity,
                TypeKind.Class));
    }

    private static NamedTypeSignature Named(
        string name,
        TypeKind kind = TypeKind.Class)
    {
        return new NamedTypeSignature(
            new TypeIdentity(
                new AssemblyIdentity("Example.Assembly"),
                "System",
                name,
                0,
                kind));
    }
}
