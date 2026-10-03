using AspectN.Syntax;
using AspectN.Text;

namespace AspectN.Tests.Parsing;

public sealed class TypePatternParserTests
{
    [Fact]
    public void ParseTypePattern_NamespaceWildcards_PreservesSegmentsAndSeparators()
    {
        const string SOURCE = "MyCompany..Services.*Service";

        var tree = AspectNParser.ParseTypePattern(SOURCE);

        var named = Assert.IsType<NamedTypePatternSyntax>(tree.Root);
        Assert.Equal(3, named.Segments.Count);
        Assert.Collection(
            named.Separators,
            token => Assert.Equal(SyntaxKind.DotDotToken, token.Kind),
            token => Assert.Equal(SyntaxKind.DotToken, token.Kind));
        Assert.Collection(
            named.Segments[2].NameParts,
            token => Assert.Equal("*", token.Text),
            token => Assert.Equal("Service", token.Text));
        Assert.Equal(new TextSpan(0, SOURCE.Length), named.FullSpan);
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void ParseTypePattern_NestedGenericType_PreservesSlashAndArguments()
    {
        var tree = AspectNParser.ParseTypePattern("Outer<T>/Inner<string>");

        var named = Assert.IsType<NamedTypePatternSyntax>(tree.Root);
        Assert.Equal(2, named.Segments.Count);
        Assert.Equal(SyntaxKind.SlashToken, Assert.Single(named.Separators).Kind);
        Assert.Equal(
            GenericPatternForm.Arguments,
            named.Segments[0].GenericArguments?.Form);
        Assert.Equal(
            GenericPatternForm.Arguments,
            named.Segments[1].GenericArguments?.Form);
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void ParseTypePattern_AssignableSuffix_WrapsNamedPattern()
    {
        var tree = AspectNParser.ParseTypePattern("System.IDisposable+");

        var assignable = Assert.IsType<AssignableTypePatternSyntax>(tree.Root);
        Assert.IsType<NamedTypePatternSyntax>(assignable.TargetType);
        Assert.Equal("+", assignable.PlusToken.Text);
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void ParseTypePattern_NullableThenArray_PreservesSuffixOrderAndRank()
    {
        var tree = AspectNParser.ParseTypePattern("int?[]");

        var array = Assert.IsType<ArrayTypePatternSyntax>(tree.Root);
        Assert.Equal(1, array.Rank);
        Assert.IsType<NullableTypePatternSyntax>(array.ElementType);
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void ParseTypePattern_Tuple_CapturesElements()
    {
        var tree = AspectNParser.ParseTypePattern("(int, string)");

        var tuple = Assert.IsType<TupleTypePatternSyntax>(tree.Root);
        Assert.Equal(2, tuple.Elements.Count);
        Assert.Single(tuple.Separators);
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void ParseTypePattern_TaskLike_CapturesResultType()
    {
        var tree = AspectNParser.ParseTypePattern("tasklike<string>");

        var taskLike = Assert.IsType<TaskLikeTypePatternSyntax>(tree.Root);
        Assert.IsType<NamedTypePatternSyntax>(taskLike.ResultType);
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void ParseTypePattern_BareTaskLike_HasNoResultType()
    {
        var tree = AspectNParser.ParseTypePattern("tasklike");

        var taskLike = Assert.IsType<TaskLikeTypePatternSyntax>(tree.Root);
        Assert.Null(taskLike.ResultType);
        Assert.Null(taskLike.LessThanToken);
        Assert.Null(taskLike.GreaterThanToken);
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void ParseTypePattern_Variable_CapturesExplicitIdentifier()
    {
        var tree = AspectNParser.ParseTypePattern("$T");

        var variable = Assert.IsType<VariableTypePatternSyntax>(tree.Root);
        Assert.Equal("T", variable.IdentifierToken.Text);
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void ParseTypePattern_RefReadonly_CreatesTypeUse()
    {
        var tree = AspectNParser.ParseTypePattern("ref readonly int");

        var typeUse = Assert.IsType<TypeUseSyntax>(tree.Root);
        Assert.Collection(
            typeUse.RefKindTokens,
            token => Assert.Equal("ref", token.Text),
            token => Assert.Equal("readonly", token.Text));
        Assert.IsType<NamedTypePatternSyntax>(typeUse.TypePattern);
        Assert.Empty(tree.Diagnostics);
    }

    [Theory]
    [InlineData("Name<>", GenericPatternForm.FixedArity, 1)]
    [InlineData("Name<,>", GenericPatternForm.FixedArity, 2)]
    [InlineData("Name<..>", GenericPatternForm.AnyPositiveArity, 0)]
    [InlineData("Name<!>", GenericPatternForm.FixedArity, 0)]
    [InlineData("Name<?>", GenericPatternForm.AnyArity, 0)]
    public void ParseTypePattern_GenericWildcardForm_StoresExplicitArity(
        string source,
        GenericPatternForm expectedForm,
        int expectedArity)
    {
        var tree = AspectNParser.ParseTypePattern(source);

        var named = Assert.IsType<NamedTypePatternSyntax>(tree.Root);
        var generic = Assert.Single(named.Segments).GenericArguments;
        Assert.NotNull(generic);
        Assert.Equal(expectedForm, generic!.Form);
        Assert.Equal(expectedArity, generic!.Arity);
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void ParseTypePattern_SingleParenthesizedType_ReportsDiagnostic()
    {
        // 括号类型只保留 Boolean 分组含义，单元素括号按缺 tuple 元素诊断。
        var tree = AspectNParser.ParseTypePattern("(int)");

        Assert.NotEmpty(tree.Diagnostics);
    }

    [Theory]
    [InlineData("Name<!>")]
    [InlineData("Name<?>")]
    [InlineData("Name<T,>")]
    [InlineData("Name<,T>")]
    [InlineData("Name<T,,U>")]
    [InlineData("Name<int,>")]
    public void ParseTypePattern_GenericSlotForms_Accepted(string source)
    {
        var tree = AspectNParser.ParseTypePattern(source);

        Assert.IsType<NamedTypePatternSyntax>(tree.Root);
        Assert.Empty(tree.Diagnostics);
    }

    [Theory]
    [InlineData("out int")]
    [InlineData("tasklike<>")]
    [InlineData("()")]
    [InlineData("(int)")]
    public void ParseTypePattern_InvalidStandardForm_ReportsDiagnostic(string source)
    {
        var tree = AspectNParser.ParseTypePattern(source);

        Assert.NotNull(tree.Root);
        Assert.NotEmpty(tree.Diagnostics);
        Assert.All(
            tree.Diagnostics,
            diagnostic =>
            {
                Assert.NotNull(diagnostic.Span);
                Assert.InRange(diagnostic.Span.Value.Start, 0, source.Length);
                Assert.InRange(diagnostic.Span.Value.End, 0, source.Length);
            });
    }
}
