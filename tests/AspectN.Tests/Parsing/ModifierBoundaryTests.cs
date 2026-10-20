using AspectN.Syntax;

namespace AspectN.Tests.Parsing;

public sealed class ModifierBoundaryTests
{
    public static TheoryData<string, string, int> Cases
    {
        get
        {
            var cases = new TheoryData<string, string, int>();
            foreach (var property in new[] { false, true })
            {
                foreach (var keyword in new[] { "static", "public", "private", "protected", "internal", "privateprotected", "protectedinternal", "async" })
                {
                    foreach (var suffix in new[] { "*", "*Tail", ".Widget", "/Widget", "<int>", "[]", "?", "+" })
                    {
                        var type = keyword + suffix;
                        cases.Add(Expression(property, type), type, 0);
                    }
                    cases.Add(Expression(property, "!" + keyword + "*"), "!" + keyword + "*", 0);
                }
                foreach (var (prefix, type) in new[]
                         {
                             ("private ", "protected*"), ("!private ", "protected*"),
                             ("protected ", "internal*"), ("!protected ", "internal*"),
                             ("public|internal ", "private*"), ("public|internal ", "static*"),
                             ("(private protected)|public ", "protected*"),
                             ("static ", "static*"), ("static ", "*"),
                             ("static\t", "*"), ("static\n", "*"),
                             ("private protected ", "*"), ("!private protected ", "*"),
                             ("public|internal ", "*")
                         })
                    cases.Add(Expression(property, prefix + type), type, 1);
            }
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ParseMemberExpression_KeywordBoundary_PreservesTypeSlot(string source, string expectedType, int modifierCount)
    {
        var tree = AspectNParser.ParseMemberExpression(source);
        Assert.Empty(tree.Diagnostics);
        var selector = Assert.IsAssignableFrom<SelectorSyntax>(tree.Root);
        var signature = Assert.IsAssignableFrom<SignatureSyntax>(selector.Body);
        Assert.Equal(modifierCount, signature.Parts.OfType<ModifierSyntax>().Count());
        var type = Assert.Single(signature.Parts.OfType<TypeUseSyntax>());
        Assert.Equal(expectedType, source.Substring(type.FullSpan.Start, type.FullSpan.Length));
        Assert.True(AspectNCompilation.Create(tree).TryGetMemberPattern(out _));
    }

    [Theory]
    [InlineData("method(public|internal* * Run())")]
    [InlineData("property(public|internal* * Value)")]
    [InlineData("method(privateprotected * Run())")]
    [InlineData("method(async * Run())")]
    public void CompileMemberExpression_InvalidModifierSyntax_RemainsInvalid(string source)
    {
        var compilation = new AspectNEngine().CompileMemberExpression(source);
        Assert.False(compilation.TryGetMemberPattern(out _));
        Assert.NotEmpty(compilation.Diagnostics);
    }

    private static string Expression(bool property, string typeAndModifiers) => property
        ? $"property({typeAndModifiers} Value)"
        : $"method({typeAndModifiers} Run())";
}
