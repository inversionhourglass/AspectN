using AspectN.Diagnostics;
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
            "execution(public async tasklike<string> Service.Get(..))",
            typeof(CallableSelectorSyntax)
        },
        {
            "property(public string Model.Name)",
            typeof(PropertySelectorSyntax)
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

    [Fact]
    public void ParseMemberExpression_RemovedAccessorKeyword_IsExtension()
    {
        var tree = AspectNParser.ParseMemberExpression("accessor()");

        Assert.IsType<ExtensionSelectorSyntax>(tree.Root);
        Assert.Empty(tree.Diagnostics);
    }

    [Theory]
    [InlineData("EXECUTION(* Service.Get(..))", typeof(CallableSelectorSyntax))]
    [InlineData("PROPERTY(* Model.Name)", typeof(PropertySelectorSyntax))]
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

    [Theory]
    [InlineData(
        "method(privateprotected * *.*(..))",
        "privateprotected",
        "private protected")]
    [InlineData(
        "method(protectedinternal * *.*(..))",
        "protectedinternal",
        "protected internal")]
    [InlineData(
        "method(static protectedinternal * *.*(..))",
        "protectedinternal",
        "protected internal")]
    [InlineData(
        "method(!privateprotected * *.*(..))",
        "privateprotected",
        "private protected")]
    [InlineData(
        "property(privateprotected string Model.Name)",
        "privateprotected",
        "private protected")]
    public void ParseMemberExpression_CompoundAccessibilityInStandard_ReportsMigration(
        string source,
        string misspelled,
        string replacement)
    {
        var tree = AspectNParser.ParseMemberExpression(source);

        var diagnostic = Assert.Single(tree.Diagnostics);
        Assert.Equal(
            DiagnosticCodes.CompositeAccessibilityMigration,
            diagnostic.Code);
        Assert.Contains(misspelled, diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains(replacement, diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("method(private protected * *.*(..))")]
    [InlineData("method(protected internal * *.*(..))")]
    [InlineData("method(!protected internal * *.*(..))")]
    public void ParseMemberExpression_SpacedCompoundAccessibility_HasNoDiagnostics(
        string source)
    {
        var tree = AspectNParser.ParseMemberExpression(source);

        Assert.Empty(tree.Diagnostics);
    }

    [Theory]
    [InlineData("method(private|internal * *.*(..))")]
    [InlineData("method(private | internal * *.*(..))")]
    [InlineData("method((private protected)|(protected internal)|public * *.*(..))")]
    [InlineData("method(private|internal static * *.*(..))")]
    [InlineData("property(private|internal string Model.Name)")]
    public void ParseMemberExpression_AccessibilityAlternation_HasNoDiagnostics(
        string source)
    {
        var tree = AspectNParser.ParseMemberExpression(source);

        Assert.Empty(tree.Diagnostics);
    }

    [Theory]
    [InlineData("method(private protected|public * *.*(..))", "parenthesized")]
    [InlineData("method(private|protected internal * *.*(..))", "parenthesized")]
    [InlineData("method(!private|internal * *.*(..))", "negated")]
    [InlineData("method(private|static * *.*(..))", "alternative")]
    public void ParseMemberExpression_InvalidAlternation_ReportsUnexpected(
        string source,
        string messagePart)
    {
        var tree = AspectNParser.ParseMemberExpression(source);

        var diagnostic = Assert.Single(tree.Diagnostics);
        Assert.Equal(DiagnosticCodes.UnexpectedToken, diagnostic.Code);
        Assert.Contains(messagePart, diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("method(void Fixtures.Foo Bar.M(..))")]
    [InlineData("method(void Service.Foo Bar(..))")]
    [InlineData("method(* *..* Foo(..))")]
    public void ParseMemberExpression_SpaceGluedMemberName_ReportsDiagnostic(
        string source)
    {
        var tree = AspectNParser.ParseMemberExpression(source);

        Assert.Contains(
            tree.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.UnexpectedToken);
    }

    [Theory]
    [InlineData("method(* A.(..))")]
    [InlineData("method(* A..(..))")]
    [InlineData("method(* A.B.(..))")]
    public void ParseMemberExpression_MissingNameAfterSeparator_ReportsDiagnostic(
        string source)
    {
        var tree = AspectNParser.ParseMemberExpression(source);

        Assert.Contains(
            tree.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.UnexpectedToken);
    }

    [Theory]
    [InlineData("method(void Fixtures.FooBar.M(..))")]
    [InlineData("method(* Service.Get*(..))")]
    [InlineData("method(* *..*.*Async(..))")]
    [InlineData("method($T Converter.Convert<$T>($T))")]
    [InlineData("method(* Service.Generic<T>.Run(..))")]
    [InlineData("method(* Service+.Get(..))")]
    [InlineData("method(* Service . Get(..))")]
    public void ParseMemberExpression_WellFormedMemberPath_HasNoDiagnostics(
        string source)
    {
        var tree = AspectNParser.ParseMemberExpression(source);

        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void ParseMemberExpression_GenericClauseThenAssignableSuffix_HasNoDiagnostics()
    {
        // §7 的成员路径类型段含泛型子句与 + 后缀（§12.4），两者组合必须放行。
        var tree = AspectNParser.ParseMemberExpression(
            "method(* Repo<int>+.Find(..))");

        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void ParseMemberExpression_GenericClauseThenAssignableSuffixLegacy_HasNoDiagnostics()
    {
        var tree = AspectNParser.ParseMemberExpression(
            "execution(* Dictionary<string,int>+.TryGetValue(..))",
            new AspectNParseOptions(AspectNLanguageVersion.Legacy));

        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void ParseMemberExpression_AssignableSuffixWithoutMemberSeparator_ReportsDiagnostic()
    {
        var tree = AspectNParser.ParseMemberExpression(
            "method(* Repo<int>+Find(..))");

        Assert.Contains(
            tree.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.UnexpectedToken);
    }

    [Theory]
    [InlineData("attr(parameter[-1] System.ObsoleteAttribute)")]
    [InlineData("attr(parameter[-0] System.ObsoleteAttribute)")]
    public void ParseMemberExpression_NegativeParameterIndex_ReportsDiagnostic(
        string source)
    {
        var tree = AspectNParser.ParseMemberExpression(source);

        Assert.Contains(
            tree.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.UnexpectedToken);
    }

    [Theory]
    [InlineData("attr(parameter[0] System.ObsoleteAttribute)")]
    [InlineData("attr(parameter[*] System.ObsoleteAttribute)")]
    public void ParseMemberExpression_NonNegativeParameterIndex_HasNoDiagnostics(
        string source)
    {
        var tree = AspectNParser.ParseMemberExpression(source);

        Assert.Empty(tree.Diagnostics);
    }
}
