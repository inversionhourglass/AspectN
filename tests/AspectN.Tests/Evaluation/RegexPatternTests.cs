using System.Globalization;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

public sealed class RegexPatternTests
{
    [Fact]
    public void Format_ComplexConstructedCallable_IsCanonical()
    {
        var candidate = ComplexCandidate();
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            var signature = CanonicalSignatureFormatter.Format(
                candidate,
                AspectNLanguageVersion.Standard);

            Assert.Equal(
                "public static async ref readonly System.String " +
                "Example.Outer<System.String>/Inner<System.Int32>." +
                "Map<System.String>(ref System.Int32, out System.String[])",
                signature);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Match_Regex_AppliesToCanonicalSignature()
    {
        var source =
            @"regex(^public static async ref readonly System\.String Example\.Outer<System\.String>/Inner<System\.Int32>\.Map<System\.String>\(ref System\.Int32, out System\.String\[\]\)$)";

        var result = Match(source, ComplexCandidate());

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Format_Accessibility_IsVersioned()
    {
        var candidate = Callable(
            "Run",
            accessibility: Accessibility.PrivateProtected);

        var standard = CanonicalSignatureFormatter.Format(
            candidate,
            AspectNLanguageVersion.Standard);
        var legacy = CanonicalSignatureFormatter.Format(
            candidate,
            AspectNLanguageVersion.Legacy);

        Assert.StartsWith("private protected ", standard);
        Assert.StartsWith("privateprotected ", legacy);
    }
    [Fact]
    public void Match_CatastrophicRegexTimesOut_ReturnsUnknownDiagnostic()
    {
        var candidate = Callable(new string('a', 20_000));

        var result = Match(
            "regex(^(.+)+Z$)",
            candidate,
            new AspectNCompilationOptions(TimeSpan.FromMilliseconds(1)));

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.RegexTimeout);
    }

    private static MatchResult Match(
        string source,
        CallableSignature candidate,
        AspectNCompilationOptions? options = null)
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(source),
            options);
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        return pattern!.Match(candidate, new StubTypeSystem());
    }

    [Fact]
    public void Match_RegexCharacterClassLeadingBracket_IsLiteral()
    {
        // .NET 语义：字符类起始位置的 ] 是字面量（Regex.IsMatch(")", "[])]") 为 true），
        // 不能把它当作字符类结束符而吞掉选择器的右括号。
        var result = Match("regex([])])", Callable("Run"));

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_RegexCommentWithOpenParen_CompilesAndMatches()
    {
        // .NET 的 (?#...) 注释由第一个 ) 结束，注释内容里的 ( 不开启分组；
        // Regex.IsMatch("Load", "(?#()Load") 为 true。
        var result = Match("regex((?#()Load)", Callable("Load"));

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_RegexCommentWithoutParen_StillMatches()
    {
        var result = Match("regex((?#explanation)Load)", Callable("Load"));

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_RegexNestedGroups_StillMatch()
    {
        var result = Match("regex((Lo)(ad))", Callable("Load"));

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_RegexCommentFollowedByConjunction_CompilesAndMatches()
    {
        var result = Match(
            "regex((?#()Load) && method(public void Example.Service.Load())",
            Callable("Load"));

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_RegexCommentWithEscapedParen_CompilesAndMatches()
    {
        // .NET 语义：注释内容对转义不透明，(?#a\) 的 ) 结束注释；
        // Regex.IsMatch("Load", "(?#a\\)Load") 为 true。
        var result = Match("regex((?#a\\)Load)", Callable("Load"));

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_RegexCommentWithWindowsPath_CompilesAndMatches()
    {
        // 注释内容里的反斜杠不被转义分支吞掉注释终止符：
        // Regex.IsMatch("Load", "(?#C:\\path\\)Load") 为 true。
        var result = Match("regex((?#C:\\path\\)Load)", Callable("Load"));

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_RegexCommentWithBracketAndParen_CompilesAndMatches()
    {
        // 注释内容里的 [ 不开启字符类，注释由第一个 ) 结束；
        // 剩余的 a 命中规范签名中的字母（Regex.IsMatch(签名, "(?# [ )a") 为 true）。
        var result = Match("regex((?# [ )a)", Callable("Load"));

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_RegexEscapedCommentMarker_DoesNotEnterComment()
    {
        // (?# 的识别必须位于转义检查之后：\( 是转义的左括号，不能开启注释；
        // 模式 x\(?#z 是合法正则但不匹配签名（含字面量 #）。
        var result = Match("regex(x\\(?#z)", Callable("Load"));

        Assert.Equal(MatchStatus.NotMatched, result.Status);
    }

    [Fact]
    public void Match_UnterminatedRegexComment_ReportsUnterminated()
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression("regex((?#abc"));

        Assert.Contains(
            compilation.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.UnterminatedRegex);
    }

    [Theory]
    [InlineData("regex([\\d])")]
    [InlineData("regex([\\]])")]
    [InlineData("regex([^\\d])")]
    [InlineData("regex([^^])")]
    public void Match_RegexCharacterClassFirstElement_ConsumesClassStart(
        string source)
    {
        // 转义元素与起始 ^ 之后的字面量 ^ 都会消耗字符类起始位置，
        // 其后的 ] 必须是结束符（.NET 接受 [\d]、[\]]、[^\d]、[^^]）。
        // 复杂候选的规范签名含 System.Int32 与 System.String[]，四类字符都能命中其中字符。
        var result = Match(source, ComplexCandidate());

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    private static CallableSignature ComplexCandidate()
    {
        var assembly = new AssemblyIdentity("Example.Assembly");
        var outerIdentity = new TypeIdentity(
            assembly,
            "Example",
            "Outer",
            1,
            TypeKind.Class);
        var innerIdentity = new TypeIdentity(
            assembly,
            string.Empty,
            "Inner",
            1,
            TypeKind.Class,
            outerIdentity);
        var definition = new NamedTypeSignature(innerIdentity);
        var declaringType = new NamedTypeSignature(
            innerIdentity,
            new TypeSignature[]
            {
                Named("System", "String"),
                Named("System", "Int32", TypeKind.Struct)
            },
            definition);
        return new CallableSignature(
            "Map",
            CallableKind.OrdinaryMethod,
            Accessibility.Public,
            isStatic: true,
            isAsync: true,
            declaringType,
            new TypeUseSignature(
                Named("System", "String"),
                RefKind.RefReadOnly,
                TypeUseKind.Return),
            new[]
            {
                new ParameterSignature(
                    "value",
                    0,
                    new TypeUseSignature(
                        Named("System", "Int32", TypeKind.Struct),
                        RefKind.Ref,
                        TypeUseKind.Parameter)),
                new ParameterSignature(
                    "values",
                    1,
                    new TypeUseSignature(
                        new ArrayTypeSignature(
                            Named("System", "String"),
                            1),
                        RefKind.Out,
                        TypeUseKind.Parameter))
            },
            Array.Empty<GenericParameterSignature>(),
            new[] { Named("System", "String") },
            Array.Empty<AttributeSignature>(),
            Array.Empty<AttributeSignature>());
    }

    private static CallableSignature Callable(
        string name,
        Accessibility accessibility = Accessibility.Public)
    {
        return new CallableSignature(
            name,
            CallableKind.OrdinaryMethod,
            accessibility,
            isStatic: false,
            isAsync: false,
            Named("Example", "Service"),
            new TypeUseSignature(
                Named("System", "Void", TypeKind.Struct),
                RefKind.None,
                TypeUseKind.Return),
            Array.Empty<ParameterSignature>(),
            Array.Empty<GenericParameterSignature>(),
            Array.Empty<TypeSignature>(),
            Array.Empty<AttributeSignature>(),
            Array.Empty<AttributeSignature>());
    }

    private static NamedTypeSignature Named(
        string @namespace,
        string name,
        TypeKind kind = TypeKind.Class)
    {
        return new NamedTypeSignature(
            new TypeIdentity(
                new AssemblyIdentity("Example.Assembly"),
                @namespace,
                name,
                0,
                kind));
    }
}
