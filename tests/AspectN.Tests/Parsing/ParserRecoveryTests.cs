using AspectN.Diagnostics;
using AspectN.Syntax;
using AspectN.Text;

namespace AspectN.Tests.Parsing;

public sealed class ParserRecoveryTests
{
    [Theory]
    [InlineData(" a", "a")]
    [InlineData(" a b c d", "a b c d")]
    [InlineData(") ] ,", ") ] ,")]
    [InlineData(" \t a \r\n b \t", "a \r\n b")]
    [InlineData(" a \"😀\"", "a \"😀\"")]
    public void ParseMemberExpression_TrailingInput_ReportsOneDiagnosticCoveringAllTokens(string suffix, string expectedText)
    {
        const string expression = "method(* Run(..))";
        var source = expression + suffix;

        var tree = AspectNParser.ParseMemberExpression(source);

        Assert.IsType<MethodSelectorSyntax>(tree.Root);
        var diagnostic = Assert.Single(tree.Diagnostics);
        Assert.Equal(DiagnosticCodes.UnexpectedToken, diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(new TextSpan(source.IndexOf(expectedText, expression.Length, StringComparison.Ordinal), expectedText.Length), diagnostic.Span);
        Assert.False(AspectNCompilation.Create(tree).TryGetMemberPattern(out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \r\n\t")]
    public void ParseMemberExpression_OnlyTrailingWhitespace_HasNoDiagnostics(string suffix)
    {
        var tree = AspectNParser.ParseMemberExpression("method(* Run(..))" + suffix);

        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void ParseMemberExpression_IncompleteSelector_PreservesDistinctMissingComponents()
    {
        var tree = AspectNParser.ParseMemberExpression("method(");

        Assert.Equal(4, tree.Diagnostics.Count);
        Assert.All(tree.Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticCodes.UnexpectedToken, diagnostic.Code);
            Assert.Equal(new TextSpan(7, 0), diagnostic.Span);
        });
        Assert.Equal(4, tree.Diagnostics.Select(diagnostic => diagnostic.Message).Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData(" a # b @", "AN0001")]
    [InlineData(" a \"unterminated", "AN0002")]
    public void ParseMemberExpression_TrailingLexicalError_PreservesLexicalDiagnostics(string suffix, string code)
    {
        const string expression = "method(* Run(..))";
        var tree = AspectNParser.ParseMemberExpression(expression + suffix);

        var trailing = Assert.Single(tree.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.UnexpectedToken);
        Assert.Equal(new TextSpan(expression.Length + 1, suffix.Length - 1), trailing.Span);
        Assert.Contains(tree.Diagnostics, diagnostic => diagnostic.Code == code);
        if (code == DiagnosticCodes.BadCharacter)
            Assert.Equal(2, tree.Diagnostics.Count(diagnostic => diagnostic.Code == code));
    }

    [Fact]
    public void ParseMemberExpression_MissingClosingParenthesisBeforeTail_PreservesBothDiagnostics()
    {
        const string source = "(method(* Run(..)) a b";
        var tree = AspectNParser.ParseMemberExpression(source);

        Assert.Equal(2, tree.Diagnostics.Count);
        Assert.Contains(tree.Diagnostics, diagnostic => diagnostic.Message.Contains("Expected CloseParenToken", StringComparison.Ordinal));
        Assert.Contains(tree.Diagnostics, diagnostic => diagnostic.Span == new TextSpan(source.Length - 3, 3));
    }

    [Fact]
    public void ParseMemberExpression_BooleanExpressionWithTail_PreservesExpressionTree()
    {
        var tree = AspectNParser.ParseMemberExpression("method(* A()) || method(* B()) a b");

        var root = Assert.IsType<BinaryExpressionSyntax>(tree.Root);
        Assert.IsType<MethodSelectorSyntax>(root.Left);
        Assert.IsType<MethodSelectorSyntax>(root.Right);
        Assert.Equal(DiagnosticCodes.UnexpectedToken, Assert.Single(tree.Diagnostics).Code);
    }

    [Theory]
    [InlineData(32, true)]
    [InlineData(128, false)]
    public void ParseMemberExpression_TrailingInput_StillConsumesTokenBudget(int tokens, bool limited)
    {
        var source = "method(* Run(..))" + string.Concat(Enumerable.Repeat(" x", 64));
        var options = new AspectNParseOptions(maximumTokens: ResourceLimit.Finite(tokens));

        var tree = AspectNParser.ParseMemberExpression(source, options);

        Assert.Equal(limited, tree.Diagnostics.Any(diagnostic => diagnostic.Code == DiagnosticCodes.ParseResourceLimitExceeded));
        Assert.Single(tree.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.UnexpectedToken);
    }

    [Theory]
    [InlineData(32, true)]
    [InlineData(256, false)]
    public void ParseMemberExpression_TrailingInput_StillConsumesSyntaxNodeBudget(int nodes, bool limited)
    {
        var source = "method(* Run(..))" + string.Concat(Enumerable.Repeat(" x", 64));
        var options = new AspectNParseOptions(maximumSyntaxNodes: ResourceLimit.Finite(nodes));

        var tree = AspectNParser.ParseMemberExpression(source, options);

        Assert.Equal(limited, tree.Diagnostics.Any(diagnostic => diagnostic.Code == DiagnosticCodes.ParseResourceLimitExceeded));
        Assert.Single(tree.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.UnexpectedToken);
    }

    [Fact]
    public void ParseMemberExpression_TrailingInputWithCanceledToken_ThrowsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => AspectNParser.ParseMemberExpression(
            "method(* Run(..)) a b", cancellationToken: cancellation.Token));
    }

    public static TheoryData<string> MalformedCases => new()
    {
        { "method(" },
        { "a() && && b()" },
        { "((a())" }
    };

    [Theory]
    [MemberData(nameof(MalformedCases))]
    public void ParseMemberExpression_MalformedInput_ReturnsRootAndStableDiagnostics(
        string source)
    {
        var tree = AspectNParser.ParseMemberExpression(source);

        Assert.NotNull(tree.Root);
        Assert.NotEmpty(tree.Diagnostics);
        Assert.All(
            tree.Diagnostics,
            diagnostic =>
            {
                Assert.StartsWith("AN", diagnostic.Code, StringComparison.Ordinal);
                Assert.NotNull(diagnostic.Span);
                Assert.InRange(diagnostic.Span.Value.Start, 0, source.Length);
                Assert.InRange(diagnostic.Span.Value.End, 0, source.Length);
            });
    }

    [Fact]
    public void ParseMemberExpression_DuplicateBinaryOperator_ReportsBothRecoveryPoints()
    {
        var tree = AspectNParser.ParseMemberExpression("method(* A()) && && method(* B())");

        Assert.True(tree.Diagnostics.Count >= 2);
        Assert.All(
            tree.Diagnostics,
            diagnostic => Assert.Equal(DiagnosticCodes.UnexpectedToken, diagnostic.Code));
    }

    [Fact]
    public void ParseMemberExpression_BadCharacter_PreservesLexicalDiagnostic()
    {
        var tree = AspectNParser.ParseMemberExpression("a() && #");

        Assert.Contains(
            tree.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.BadCharacter);
    }

    [Theory]
    [InlineData("method(int <T> X.Y())")]
    [InlineData("method(Service <int> Service.Run())")]
    [InlineData("property(Service <int> Service.X)")]
    public void ParseMemberExpression_MalformedSignature_ReturnsDiagnostics(string source)
    {
        var compilation = AspectNCompilation.Create(AspectNParser.ParseMemberExpression(source));
        Assert.Contains(compilation.Diagnostics, diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Span is not null);
        Assert.False(compilation.TryGetMemberPattern(out _));
    }

    [Fact]
    public void ParseMemberExpression_EmptyTaskLike_PreservesBoundaryAndDiagnostic()
    {
        var standalone = Assert.Single(AspectNParser.ParseTypePattern("tasklike<>").Diagnostics);
        var tree = AspectNParser.ParseMemberExpression("method(tasklike<> M()) || method(* N())");
        var embedded = Assert.Single(tree.Diagnostics);
        Assert.Equal(standalone.Code, embedded.Code);
        Assert.Equal(standalone.Message, embedded.Message);
        Assert.Equal(standalone.Span!.Value.Start + 7, embedded.Span!.Value.Start);
        var root = Assert.IsType<BinaryExpressionSyntax>(tree.Root);
        Assert.IsType<MethodSelectorSyntax>(root.Right);
    }

    [Theory]
    [InlineData("method(* System..*(..)) || method(* N())")]
    [InlineData("getter(* System..*) || method(* N())")]
    public void ParseMemberExpression_NamespaceEllipsisWithoutMemberSeparator_ReportsOneError(string source)
    {
        var tree = AspectNParser.ParseMemberExpression(source);
        var diagnostic = Assert.Single(tree.Diagnostics);
        Assert.Equal(DiagnosticCodes.UnexpectedToken, diagnostic.Code);
        Assert.Contains("member", diagnostic.Message);
        var root = Assert.IsType<BinaryExpressionSyntax>(tree.Root);
        Assert.IsType<MethodSelectorSyntax>(root.Right);
    }
}
