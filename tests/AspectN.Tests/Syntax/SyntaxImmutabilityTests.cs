using System.Collections;
using AspectN.Syntax;
using AspectN.Text;

namespace AspectN.Tests.Syntax;

public sealed class SyntaxImmutabilityTests
{
    [Fact]
    public void CompileTypePattern_CachedNameParts_CannotBeModified()
    {
        var engine = new AspectNEngine();
        var compilation = engine.CompileTypePattern("int");
        var root = Assert.IsType<NamedTypePatternSyntax>(compilation.SyntaxTree.Root);
        var name = root.Segments[0];
        var collection = Assert.IsAssignableFrom<IList<SyntaxToken>>(name.NameParts);

        Assert.Throws<NotSupportedException>(() => collection[0] =
            new SyntaxToken(SyntaxKind.IdentifierToken, new TextSpan(0, 3), "string"));

        Assert.Same(compilation, engine.CompileTypePattern("int"));
        Assert.Equal("int", name.NameParts[0].Text);
        Assert.Same(name.NameParts[0], name.Children[0]);
    }

    [Theory]
    [InlineData("method(public ref int Example.Box<string>.M<int[]>(ref (int,string)))")]
    [InlineData("method(!ref int Example.Box<int>.M(!ref int))")]
    public void ParseMemberExpression_NonemptyPublicCollections_RejectMutation(string source)
    {
        var tree = AspectNParser.ParseMemberExpression(source);
        Assert.Empty(tree.Diagnostics);
        var pending = new Stack<SyntaxNode>();
        pending.Push(tree.Root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            foreach (var property in node.GetType().GetProperties())
            {
                if (property.GetValue(node) is IList { Count: > 0 } collection)
                    Assert.Throws<NotSupportedException>(() => collection[0] = collection[0]);
            }

            foreach (var child in node.Children) pending.Push(child);
        }
    }

    [Fact]
    public void TypeUse_Constructor_SnapshotsNegatedRefTokens()
    {
        var tokens = new List<SyntaxToken>
        {
            new(SyntaxKind.BangToken, new TextSpan(0, 1), "!"),
            new(SyntaxKind.IdentifierToken, new TextSpan(1, 3), "ref")
        };
        var use = new TypeUseSyntax([], new WildcardTypePatternSyntax(
            new SyntaxToken(SyntaxKind.StarToken, new TextSpan(5, 1), "*")), tokens);

        tokens.Clear();

        Assert.Equal(2, use.NegatedRefTokens.Count);
        Assert.Equal(3, use.Children.Count);
        Assert.Same(use.NegatedRefTokens[0], use.Children[0]);
    }
}
