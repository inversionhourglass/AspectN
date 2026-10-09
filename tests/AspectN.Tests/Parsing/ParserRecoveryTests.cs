using AspectN.Diagnostics;
using AspectN.Syntax;

namespace AspectN.Tests.Parsing;

public sealed class ParserRecoveryTests
{
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
