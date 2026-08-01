using AspectN.Syntax;

namespace AspectN.Tests.Parsing;

public sealed class SelectorParserTests
{
    public static TheoryData<string, Type> StandardSelectorCases => new()
    {
        {
            "callable(public async tasklike<string> Service.Get(..))",
            typeof(CallableSelectorSyntax)
        },
        {
            "method(ref readonly int Buffer.Get())",
            typeof(MethodSelectorSyntax)
        },
        {
            "accessor(public string Model.Name)",
            typeof(AccessorSelectorSyntax)
        },
        { "getter(* Model.Name)", typeof(GetterSelectorSyntax) },
        { "setter(* Model.Name)", typeof(SetterSelectorSyntax) },
        { "ctor(Service(string, ref int))", typeof(ConstructorSelectorSyntax) },
        { "cctor(Service)", typeof(StaticConstructorSelectorSyntax) },
        {
            "attr(parameter[0] RequiredAttribute)",
            typeof(AttributeSelectorSyntax)
        },
        {
            "vendor.feature(\"x\", -2, true, System.IDisposable+)",
            typeof(ExtensionSelectorSyntax)
        }
    };

    [Theory]
    [MemberData(nameof(StandardSelectorCases))]
    public void ParseMemberExpression_StandardSelector_CreatesExpectedSyntax(
        string source,
        Type expectedType)
    {
        var tree = AspectNParser.ParseMemberExpression(source);

        Assert.IsType(expectedType, tree.Root);
        Assert.Empty(tree.Diagnostics);
        Assert.Equal(source.Length, tree.Root.FullSpan.Length);
    }

    [Fact]
    public void ParseMemberExpression_ExtensionArguments_UseFixedArgumentGrammar()
    {
        var tree = AspectNParser.ParseMemberExpression(
            "vendor.feature(\"x\", -2, true, System.IDisposable+)");

        var extension = Assert.IsType<ExtensionSelectorSyntax>(tree.Root);
        Assert.Equal(4, extension.Arguments.Count);
        Assert.Equal(SyntaxKind.StringToken, extension.Arguments[0].Kind);
        Assert.Equal(SyntaxKind.IntegerToken, extension.Arguments[1].Kind);
        Assert.Equal(SyntaxKind.IdentifierToken, extension.Arguments[2].Kind);
        Assert.IsType<AssignableTypePatternSyntax>(extension.Arguments[3]);
        Assert.Equal(3, extension.Separators.Count);
    }

    [Theory]
    [InlineData("execution(* Service.Get(..))", "AN4001")]
    [InlineData("property(* Model.Name)", "AN4002")]
    public void ParseMemberExpression_LegacySelectorInStandard_ReportsMigration(
        string source,
        string expectedCode)
    {
        var tree = AspectNParser.ParseMemberExpression(source);

        Assert.Contains(
            tree.Diagnostics,
            diagnostic => diagnostic.Code == expectedCode);
    }

    [Theory]
    [InlineData("EXECUTION(* Service.Get(..))", typeof(CallableSelectorSyntax))]
    [InlineData("PROPERTY(* Model.Name)", typeof(AccessorSelectorSyntax))]
    [InlineData("ATTR(para 0 RequiredAttribute)", typeof(AttributeSelectorSyntax))]
    [InlineData("METHOD(* Service.Get<!>(..))", typeof(MethodSelectorSyntax))]
    [InlineData("METHOD(privateprotected * Service.Get(..))", typeof(MethodSelectorSyntax))]
    public void ParseMemberExpression_LegacyForm_IsAcceptedCaseInsensitively(
        string source,
        Type expectedType)
    {
        var options = new AspectNParseOptions(AspectNLanguageVersion.Legacy);

        var tree = AspectNParser.ParseMemberExpression(source, options);

        Assert.IsType(expectedType, tree.Root);
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void ParseMemberExpression_StandardKeywordWithWrongCase_IsExtension()
    {
        var tree = AspectNParser.ParseMemberExpression("METHOD()");

        Assert.IsType<ExtensionSelectorSyntax>(tree.Root);
        Assert.Empty(tree.Diagnostics);
    }
}
