using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

public sealed class GenericPatternTests
{
    [Theory]
    [InlineData("Box<>", 1, MatchStatus.Matched)]
    [InlineData("Box<>", 2, MatchStatus.NotMatched)]
    [InlineData("Pair<,>", 2, MatchStatus.Matched)]
    [InlineData("Pair<,>", 1, MatchStatus.NotMatched)]
    [InlineData("Many<..>", 3, MatchStatus.Matched)]
    [InlineData("Many<..>", 0, MatchStatus.NotMatched)]
    public void Match_ExplicitArityPattern_ReturnsExpected(
        string source,
        int arity,
        MatchStatus expected)
    {
        var candidate = Definition("Example", NameFrom(source), arity);

        var result = Match(source, candidate);

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void Match_ExactConstructedArguments_MustMatchInOrder()
    {
        var definition = Definition("Example", "Pair", 2);
        var matching = Construct(
            definition,
            Named("System", "String"),
            Named("System", "Int32", TypeKind.Struct));
        var reversed = Construct(
            definition,
            Named("System", "Int32", TypeKind.Struct),
            Named("System", "String"));

        Assert.Equal(
            MatchStatus.Matched,
            Match("Pair<string, int>", matching).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            Match("Pair<string, int>", reversed).Status);
    }

    [Fact]
    public void Match_NestedGenericArguments_UseOuterThenInnerOrder()
    {
        var outerIdentity = Identity("Example", "Outer", 1);
        var innerIdentity = Identity(
            string.Empty,
            "Inner",
            1,
            containingType: outerIdentity);
        var definition = new NamedTypeSignature(innerIdentity);
        var candidate = new NamedTypeSignature(
            innerIdentity,
            [
                Named("System", "String"),
                Named("System", "Int32", TypeKind.Struct)
            ],
            definition);

        var result = Match("Outer<string>/Inner<int>", candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_ConstructedType_UsesEffectiveArgumentsNotDefinitionParameters()
    {
        var definitionIdentity = Identity("Example", "Box", 1);
        var definition = new NamedTypeSignature(
            definitionIdentity,
            [
                new GenericParameterTypeSignature(
                    new GenericParameterSignature(
                        "T",
                        GenericParameterOwnerKind.Type,
                        "Example.Box`1",
                        0))
            ]);
        var constructed = new NamedTypeSignature(
            definitionIdentity,
            [Named("System", "String")],
            definition);

        var result = Match("Box<string>", constructed);

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.Same(definition, constructed.OriginalDefinition);
    }

    [Fact]
    public void Match_MissingConstructedArguments_ReturnsUnknown()
    {
        var candidate = Definition("Example", "Box", 1);

        var result = Match("Box<string>", candidate);

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.NotEmpty(result.Diagnostics);
    }

    private static MatchResult Match(
        string source,
        NamedTypeSignature candidate)
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseTypePattern(source));
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        return pattern!.Match(candidate, new StubTypeSystem());
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

    private static NamedTypeSignature Definition(
        string @namespace,
        string name,
        int arity)
    {
        return new NamedTypeSignature(Identity(@namespace, name, arity));
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
        int arity = 0,
        TypeIdentity? containingType = null,
        TypeKind kind = TypeKind.Class)
    {
        return new TypeIdentity(
            new AssemblyIdentity("Example.Assembly"),
            @namespace,
            name,
            arity,
            kind,
            containingType);
    }

    private static string NameFrom(string source)
    {
        var genericStart = source.IndexOf('<', StringComparison.Ordinal);
        return genericStart < 0 ? source : source.Substring(0, genericStart);
    }
}
