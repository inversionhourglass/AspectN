using AspectN.Diagnostics;
using AspectN.Parsing;
using AspectN.Syntax;
using AspectN.Text;

namespace AspectN.Tests.Syntax;

public sealed class SyntaxTreeTests
{
    [Fact]
    public void FullSpan_CompositeExpression_CoversChildrenInSourceOrder()
    {
        var left = Extension("a", 0);
        var right = Extension("b", 7);
        var expression = new BinaryExpressionSyntax(
            left,
            Token(SyntaxKind.AmpersandAmpersandToken, 4, "&&"),
            right);

        Assert.Equal(new TextSpan(0, 10), expression.FullSpan);
        Assert.Equal(
            new SyntaxNode[] { left, expression.OperatorToken, right },
            expression.Children);
    }

    [Fact]
    public void TupleTypePattern_Constructor_CopiesChildCollections()
    {
        var elements = new List<TypePatternSyntax>
        {
            new WildcardTypePatternSyntax(Token(SyntaxKind.StarToken, 1, "*")),
            new WildcardTypePatternSyntax(Token(SyntaxKind.StarToken, 4, "*"))
        };
        var separators = new List<SyntaxToken>
        {
            Token(SyntaxKind.CommaToken, 2, ",")
        };
        var tuple = new TupleTypePatternSyntax(
            Token(SyntaxKind.OpenParenToken, 0, "("),
            elements,
            separators,
            Token(SyntaxKind.CloseParenToken, 5, ")"));

        elements.Clear();
        separators.Clear();

        Assert.Equal(2, tuple.Elements.Count);
        Assert.Single(tuple.Separators);
        Assert.Equal(new TextSpan(0, 6), tuple.FullSpan);
    }

    [Fact]
    public void MissingToken_Create_HasZeroWidth()
    {
        var token = SyntaxToken.Missing(SyntaxKind.CloseParenToken, 8);

        Assert.True(token.IsMissing);
        Assert.Equal(new TextSpan(8, 0), token.Span);
        Assert.Equal(string.Empty, token.Text);
        Assert.Equal(token.Span, token.FullSpan);
    }

    [Fact]
    public void SyntaxTree_MalformedInput_RetainsRootAndDiagnosticSnapshot()
    {
        var diagnostics = new List<AspectNDiagnostic>
        {
            new(
                DiagnosticCodes.UnexpectedToken,
                DiagnosticSeverity.Error,
                "Expected ')'.",
                new TextSpan(2, 0))
        };
        var tree = new AspectNSyntaxTree(
            SourceText.From("a("),
            AspectNParseKind.MemberExpression,
            Extension("a", 0, closeParenMissing: true),
            diagnostics);

        diagnostics.Clear();

        Assert.NotNull(tree.Root);
        Assert.Single(tree.Diagnostics);
        Assert.Equal(new TextSpan(0, 2), tree.Root.FullSpan);
    }

    [Fact]
    public void SyntaxModel_ConcreteNodeFamilies_AreAvailable()
    {
        Type[] requiredTypes =
        {
            typeof(UnaryExpressionSyntax),
            typeof(BinaryExpressionSyntax),
            typeof(ParenthesizedExpressionSyntax),
            typeof(CallableSelectorSyntax),
            typeof(MethodSelectorSyntax),
            typeof(PropertySelectorSyntax),
            typeof(GetterSelectorSyntax),
            typeof(SetterSelectorSyntax),
            typeof(ConstructorSelectorSyntax),
            typeof(StaticConstructorSelectorSyntax),
            typeof(AttributeSelectorSyntax),
            typeof(RegexSelectorSyntax),
            typeof(ExtensionSelectorSyntax),
            typeof(CallableSignatureSyntax),
            typeof(PropertySignatureSyntax),
            typeof(ConstructorSignatureSyntax),
            typeof(ParameterSyntax),
            typeof(ModifierSyntax),
            typeof(GenericPatternListSyntax),
            typeof(TypeUseSyntax),
            typeof(NamedTypePatternSyntax),
            typeof(WildcardTypePatternSyntax),
            typeof(TupleTypePatternSyntax),
            typeof(TaskLikeTypePatternSyntax),
            typeof(VariableTypePatternSyntax),
            typeof(ArrayTypePatternSyntax),
            typeof(NullableTypePatternSyntax),
            typeof(AssignableTypePatternSyntax),
            typeof(ParenthesizedTypePatternSyntax)
        };

        Assert.All(requiredTypes, type => Assert.True(type.IsSealed));
    }

    [Fact]
    public void SyntaxModel_PublicSurface_DoesNotReferenceBackendAssemblies()
    {
        var forbiddenPrefixes = new[]
        {
            "Mono.Cecil",
            "Microsoft.CodeAnalysis",
            "System.Reflection"
        };
        var syntaxTypes = typeof(SyntaxNode).Assembly
            .GetExportedTypes()
            .Where(type => type.Namespace == "AspectN.Syntax");

        foreach (var type in syntaxTypes)
        {
            foreach (var property in type.GetProperties())
            {
                Assert.DoesNotContain(
                    forbiddenPrefixes,
                    prefix => property.PropertyType.FullName?.StartsWith(
                        prefix,
                        StringComparison.Ordinal) == true);
            }
        }
    }

    private static ExtensionSelectorSyntax Extension(
        string name,
        int start,
        bool closeParenMissing = false)
    {
        return new ExtensionSelectorSyntax(
            Token(SyntaxKind.IdentifierToken, start, name),
            Token(SyntaxKind.OpenParenToken, start + 1, "("),
            closeParenMissing
                ? SyntaxToken.Missing(SyntaxKind.CloseParenToken, start + 2)
                : Token(SyntaxKind.CloseParenToken, start + 2, ")"));
    }

    private static SyntaxToken Token(SyntaxKind kind, int start, string text)
    {
        return new SyntaxToken(kind, new TextSpan(start, text.Length), text);
    }
}
