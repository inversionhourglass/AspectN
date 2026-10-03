using AspectN.Diagnostics;
using AspectN.Syntax;

namespace AspectN.Tests.Parsing;

public sealed class SelectorParserTests
{
    public static TheoryData<string, Type> StandardSelectorCases => new()
    {
        {
            "callable(public tasklike<string> Service.Get(..))",
            typeof(CallableSelectorSyntax)
        },
        {
            "method(ref readonly int Buffer.Get())",
            typeof(MethodSelectorSyntax)
        },
        {
            "execution(public tasklike<string> Service.Get(..))",
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
            "attr(para[0] RequiredAttribute)",
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
    [InlineData("method((private protected) * *.*(..))")]
    [InlineData("method((protected internal) * *.*(..))")]
    [InlineData("method((private protected) static * *.*(..))")]
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
    [InlineData("method((public) * *.*(..))", "compound accessibility")]
    [InlineData("method((internal public) * *.*(..))", "compound accessibility")]
    [InlineData("method(private| * *.*(..))", "accessibility alternative")]
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
    [InlineData("method(async string Service.Get(..))")]
    [InlineData("method(public async tasklike<string> Service.Get(..))")]
    [InlineData("execution(async * Service.Run(..))")]
    public void ParseMemberExpression_AsyncInStandard_ReportsUnexpected(
        string source)
    {
        var tree = AspectNParser.ParseMemberExpression(source);

        var diagnostic = Assert.Single(tree.Diagnostics);
        Assert.Equal(DiagnosticCodes.UnexpectedToken, diagnostic.Code);
        Assert.Contains("async", diagnostic.Message, StringComparison.Ordinal);
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
    public void ParseMemberExpression_WellFormedMemberPath_HasNoDiagnostics(
        string source)
    {
        var tree = AspectNParser.ParseMemberExpression(source);

        Assert.Empty(tree.Diagnostics);
    }

    [Theory]
    [InlineData("method(* Service .Get(..))", "must be adjacent")]
    [InlineData("method(* Service. Get(..))", "member path separator")]
    [InlineData("method(* IEnumerable<int>+ .Get(..))", "must be adjacent")]
    [InlineData("method(* System. String.Foo(..))", "member path separator")]
    public void ParseMemberExpression_WhitespaceAroundMemberSeparator_ReportsDiagnostic(
        string source,
        string messagePart)
    {
        // 分隔符与其两侧名字必须相邻（§3.4）：空格是槽位边界，不表达成员路径延续。
        var tree = AspectNParser.ParseMemberExpression(source);

        var diagnostic = Assert.Single(tree.Diagnostics);
        Assert.Equal(DiagnosticCodes.UnexpectedToken, diagnostic.Code);
        Assert.Contains(messagePart, diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("method(tasklike.Service())")]
    [InlineData("method(tasklike<T>.X())")]
    [InlineData("callable(tasklike .Foo())")]
    [InlineData("method(* .Get(..))")]
    [InlineData("method($T .Foo())")]
    [InlineData("method(int .Foo())")]
    public void ParseMemberExpression_LeadingMemberSeparator_ReportsDiagnostic(
        string source)
    {
        // 省略声明类型就是不写（§7）；成员路径必须以名字开头，前导分隔符没有语义。
        var tree = AspectNParser.ParseMemberExpression(source);

        var diagnostic = Assert.Single(tree.Diagnostics);
        Assert.Equal(DiagnosticCodes.UnexpectedToken, diagnostic.Code);
        Assert.Contains(
            "cannot start with a separator",
            diagnostic.Message,
            StringComparison.Ordinal);
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
    public void ParseMemberExpression_ExecutionGenericClauseThenAssignableSuffix_HasNoDiagnostics()
    {
        var tree = AspectNParser.ParseMemberExpression(
            "execution(* Dictionary<string,int>+.TryGetValue(..))");

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
    [InlineData("attr(para[-1] System.ObsoleteAttribute)")]
    [InlineData("attr(para[-0] System.ObsoleteAttribute)")]
    [InlineData("attr(para -1 System.ObsoleteAttribute)")]
    [InlineData("attr(para -0 System.ObsoleteAttribute)")]
    public void ParseMemberExpression_NegativeParameterIndex_ReportsDiagnostic(
        string source)
    {
        var tree = AspectNParser.ParseMemberExpression(source);

        Assert.Contains(
            tree.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.UnexpectedToken);
    }

    [Theory]
    [InlineData("attr(para[0] System.ObsoleteAttribute)")]
    [InlineData("attr(para[*] System.ObsoleteAttribute)")]
    public void ParseMemberExpression_NonNegativeParameterIndex_HasNoDiagnostics(
        string source)
    {
        var tree = AspectNParser.ParseMemberExpression(source);

        Assert.Empty(tree.Diagnostics);
    }

    [Theory]
    [InlineData("attr(para 0 System.ObsoleteAttribute)")]
    [InlineData("attr(para * System.ObsoleteAttribute)")]
    [InlineData("attr(* System.ObsoleteAttribute)")]
    [InlineData("attr(exec System.ObsoleteAttribute)")]
    public void ParseMemberExpression_AttributePositionSpaceForm_HasNoDiagnostics(
        string source)
    {
        var tree = AspectNParser.ParseMemberExpression(source);

        Assert.Empty(tree.Diagnostics);
    }

    [Theory]
    [InlineData("attr(callable System.ObsoleteAttribute)", "callable", "call")]
    [InlineData("attr(property System.ObsoleteAttribute)", "property", "prop")]
    [InlineData("attr(parameter[0] System.ObsoleteAttribute)", "parameter", "para")]
    [InlineData("attr(return System.ObsoleteAttribute)", "return", "ret")]
    public void ParseMemberExpression_LongAttributePosition_ReportsMigration(
        string source,
        string position,
        string replacement)
    {
        var tree = AspectNParser.ParseMemberExpression(source);

        var diagnostic = Assert.Single(tree.Diagnostics);
        Assert.Equal(
            DiagnosticCodes.AttributePositionMigration,
            diagnostic.Code);
        Assert.Contains(position, diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains(replacement, diagnostic.Message, StringComparison.Ordinal);
    }
}
