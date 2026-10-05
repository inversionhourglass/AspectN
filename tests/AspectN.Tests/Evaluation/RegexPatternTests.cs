using System.Globalization;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

public sealed class RegexPatternTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Match_OpenGenericArityExceedsFormattingBudget_ReturnsUnknown(bool declaringType)
    {
        var assembly = new AssemblyIdentity("Example");
        var outer = new TypeIdentity(assembly, "Example", "Outer", int.MaxValue, TypeKind.Class);
        var huge = new NamedTypeSignature(new TypeIdentity(assembly, "", "Inner", 1, TypeKind.Class, outer));
        var candidate = new MethodSignature("M", MethodKind.OrdinaryMethod, Accessibility.Public, false, false,
            declaringType ? huge : Named("Example", "Service"),
            new TypeUseSignature(declaringType ? Named("System", "Void", TypeKind.Struct) : huge,
                RefKind.None, TypeUseKind.Return), [], [], [], [], []);
        var result = Match("regex(.*)", candidate);
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.MissingBackendCapability);
    }

    [Fact]
    public async Task Match_SharedCanonicalTypeGraph_ReturnsUnknownWithoutExponentialOutput()
    {
        TypeSignature node = Named("System", "Int32", TypeKind.Struct);
        var identity = new TypeIdentity(new AssemblyIdentity("Example"), "Example", "Box", 2, TypeKind.Class);
        for (var index = 0; index < 40; index++) node = new NamedTypeSignature(identity, [node, node]);
        var candidate = new MethodSignature("M", MethodKind.OrdinaryMethod, Accessibility.Public, false, false,
            Named("Example", "Service"), new TypeUseSignature(node, RefKind.None, TypeUseKind.Return), [], [], [], [], []);
        var result = await Task.Run(() => Match("regex(.*)", candidate)).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.MissingBackendCapability
            && diagnostic.Message.Contains("formatting limit", StringComparison.Ordinal));
    }

    [Fact]
    public void Match_CanonicalSignatureExceedsOutputLimit_ReturnsUnknown()
    {
        var result = Match("regex(.*)", Callable(new string('M', 100_000)));
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.MissingBackendCapability);
    }

    [Fact]
    public void Match_UnknownGenericDeclaration_ReturnsUnknownInsteadOfSourceName()
    {
        var missing = new GenericParameterSignature("SourceName", GenericParameterOwnerKind.Method,
            "Other.M", new AssemblyIdentity("Other"), 0);
        var candidate = new MethodSignature("M", MethodKind.OrdinaryMethod, Accessibility.Public, false, false,
            Named("Example", "Service"),
            new TypeUseSignature(new GenericParameterTypeSignature(missing), RefKind.None, TypeUseKind.Return),
            [], [], [], [], []);
        var result = Match("regex(SourceName)", candidate);
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.MissingBackendCapability);
    }

    [Fact]
    public void GenericParameterKey_SameDeclarationTextInDifferentAssemblies_IsDistinct()
    {
        var first = new GenericParameterKey(new GenericParameterSignature("T", GenericParameterOwnerKind.Type,
            "Example.Owner", new AssemblyIdentity("First"), 0));
        var second = new GenericParameterKey(new GenericParameterSignature("T", GenericParameterOwnerKind.Type,
            "Example.Owner", new AssemblyIdentity("Second"), 0));
        Assert.NotEqual(first, second);
        Assert.Equal(2, new HashSet<GenericParameterKey> { first, second }.Count);
    }

    [Fact]
    public void Format_ComplexConstructedCallable_IsCanonical()
    {
        var candidate = ComplexCandidate();
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            var signature = CanonicalSignatureFormatter.Format(
                candidate);

            Assert.Equal(
                "public static ref readonly System.String " +
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
            @"regex(^public static ref readonly System\.String Example\.Outer<System\.String>/Inner<System\.Int32>\.Map<System\.String>\(ref System\.Int32, out System\.String\[\]\)$)";

        var result = Match(source, ComplexCandidate());

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Format_Accessibility_UsesSpacedCompoundName()
    {
        var candidate = Callable(
            "Run",
            accessibility: Accessibility.PrivateProtected);

        var standard = CanonicalSignatureFormatter.Format(
            candidate);

        Assert.StartsWith("private protected ", standard);
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

    [Fact]
    public void Format_OpenGenericDefinition_RenamesParametersPositionally()
    {
        // class Cls<TM> { public TA M<TA, TB>(TM m, TB b) => default; }
        var candidate = OpenGenericCandidate();

        var standard = CanonicalSignatureFormatter.Format(
            candidate);

        Assert.Equal("public T2 X.Y.Z.Cls<T1>.M<T2, T3>(T1, T3)", standard);
    }

    [Fact]
    public void Format_NestedOpenGeneric_NumbersFlatSlotsAcrossLevels()
    {
        // class Outer<TA> { class Inner<TB, TC> { public TD M<TD>(TB b, TC c, TA a); } }
        var candidate = NestedOpenGenericCandidate();

        var standard = CanonicalSignatureFormatter.Format(
            candidate);

        Assert.Equal(
            "public T4 X.Y.Z.Outer<T1>/Inner<T2, T3>.M<T4>(T2, T3, T1)",
            standard);
    }

    [Fact]
    public void Format_SingleGenericParameter_UsesBareT()
    {
        // class Cls<A> { public void M(A a); } —— 总数为 1 时渲染裸 T。
        var candidate = SingleGenericCandidate();

        var standard = CanonicalSignatureFormatter.Format(
            candidate);

        Assert.Equal("public System.Void X.Y.Z.Cls<T>.M(T)", standard);
    }

    [Fact]
    public void Format_MethodOnlySingleGenericParameter_UsesBareT()
    {
        // class Cls { public void M<A>(A a); } —— 仅方法泛型、总数为 1，同样渲染裸 T。
        var candidate = MethodOnlyGenericCandidate();

        var standard = CanonicalSignatureFormatter.Format(
            candidate);

        Assert.Equal("public System.Void X.Y.Z.Cls.M<T>(T)", standard);
    }

    [Fact]
    public void Format_ShadowedMethodGenericParameter_BindsToMethodSlot()
    {
        // class Cls<TM> { public void M<TM>(TM x); } —— 方法级 TM 遮蔽类型级 TM。
        var candidate = ShadowedGenericCandidate();

        var standard = CanonicalSignatureFormatter.Format(
            candidate);

        Assert.Equal("public System.Void X.Y.Z.Cls<T1>.M<T2>(T2)", standard);
    }

    [Fact]
    public void Format_ConstructedGenericUse_RenamesNestedArgument()
    {
        // class Cls<TM> { public void M(List<TM> items); } —— 总数为 1，渲染裸 T。
        var candidate = ConstructedUseCandidate();

        var standard = CanonicalSignatureFormatter.Format(
            candidate);

        Assert.Equal(
            "public System.Void X.Y.Z.Cls<T>.M(" +
            "System.Collections.Generic.List<T>)",
            standard);
    }

    [Fact]
    public void Match_Regex_AppliesToPositionalGenericNames()
    {
        var result = Match(
            "regex(^public T2 X\\.Y\\.Z\\.Cls<T1>\\.M<T2, T3>\\(T1, T3\\)$)",
            OpenGenericCandidate());

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_Regex_SingleGenericParameter_MatchesBareT()
    {
        var result = Match(
            "regex(^public System\\.Void X\\.Y\\.Z\\.Cls<T>\\.M\\(T\\)$)",
            SingleGenericCandidate());

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    private static MatchResult Match(
        string source,
        MethodSignature candidate,
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

    [Theory]
    [InlineData("[a-[])]]", MatchStatus.Matched)]
    [InlineData("[a-[b-[])]]]", MatchStatus.Matched)]
    [InlineData("[a-[^])]]", MatchStatus.NotMatched)]
    public void Match_RegexSubtractionWithLeadingBracket_PreservesNestedClass(
        string body,
        MatchStatus expected)
    {
        var result = Match($"regex({body})", Callable("Load"));

        Assert.Equal(expected, result.Status);
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

    private static MethodSignature ComplexCandidate()
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
        return new MethodSignature(
            "Map",
            MethodKind.OrdinaryMethod,
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

    private static MethodSignature Callable(
        string name,
        Accessibility accessibility = Accessibility.Public)
    {
        return new MethodSignature(
            name,
            MethodKind.OrdinaryMethod,
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

    private static MethodSignature OpenGenericCandidate()
    {
        var assembly = new AssemblyIdentity("Example.Assembly");
        var clsIdentity = new TypeIdentity(
            assembly,
            "X.Y.Z",
            "Cls",
            1,
            TypeKind.Class);
        var methodOwner = "X.Y.Z.Cls`1.M`2";
        var typeParameter = new GenericParameterSignature(
            "TM",
            GenericParameterOwnerKind.Type,
            clsIdentity.FullMetadataName,
            assembly,
            0);
        var firstMethodParameter = new GenericParameterSignature(
            "TA",
            GenericParameterOwnerKind.Method,
            methodOwner,
            assembly,
            0);
        var secondMethodParameter = new GenericParameterSignature(
            "TB",
            GenericParameterOwnerKind.Method,
            methodOwner,
            assembly,
            1);
        return new MethodSignature(
            "M",
            MethodKind.OrdinaryMethod,
            Accessibility.Public,
            isStatic: false,
            isAsync: false,
            new NamedTypeSignature(clsIdentity),
            new TypeUseSignature(
                new GenericParameterTypeSignature(firstMethodParameter),
                RefKind.None,
                TypeUseKind.Return),
            new[]
            {
                new ParameterSignature(
                    "m",
                    0,
                    new TypeUseSignature(
                        new GenericParameterTypeSignature(typeParameter),
                        RefKind.None,
                        TypeUseKind.Parameter)),
                new ParameterSignature(
                    "b",
                    1,
                    new TypeUseSignature(
                        new GenericParameterTypeSignature(secondMethodParameter),
                        RefKind.None,
                        TypeUseKind.Parameter))
            },
            new[] { firstMethodParameter, secondMethodParameter },
            Array.Empty<TypeSignature>(),
            Array.Empty<AttributeSignature>(),
            Array.Empty<AttributeSignature>());
    }

    private static MethodSignature NestedOpenGenericCandidate()
    {
        var assembly = new AssemblyIdentity("Example.Assembly");
        var outerIdentity = new TypeIdentity(
            assembly,
            "X.Y.Z",
            "Outer",
            1,
            TypeKind.Class);
        var innerIdentity = new TypeIdentity(
            assembly,
            string.Empty,
            "Inner",
            2,
            TypeKind.Class,
            outerIdentity);
        // 类型形参的 Position 使用包含链扁平 ECMA 槽位，OwnerId 使用声明层
        // 的 FullMetadataName，与三个适配器的真实映射一致。
        var outerParameter = new GenericParameterSignature(
            "TA",
            GenericParameterOwnerKind.Type,
            outerIdentity.FullMetadataName,
            assembly,
            0);
        var firstInnerParameter = new GenericParameterSignature(
            "TB",
            GenericParameterOwnerKind.Type,
            innerIdentity.FullMetadataName,
            assembly,
            1);
        var secondInnerParameter = new GenericParameterSignature(
            "TC",
            GenericParameterOwnerKind.Type,
            innerIdentity.FullMetadataName,
            assembly,
            2);
        var methodParameter = new GenericParameterSignature(
            "TD",
            GenericParameterOwnerKind.Method,
            "X.Y.Z.Outer`1/Inner`2.M`1",
            assembly,
            0);
        return new MethodSignature(
            "M",
            MethodKind.OrdinaryMethod,
            Accessibility.Public,
            isStatic: false,
            isAsync: false,
            new NamedTypeSignature(innerIdentity),
            new TypeUseSignature(
                new GenericParameterTypeSignature(methodParameter),
                RefKind.None,
                TypeUseKind.Return),
            new[]
            {
                new ParameterSignature(
                    "b",
                    0,
                    new TypeUseSignature(
                        new GenericParameterTypeSignature(firstInnerParameter),
                        RefKind.None,
                        TypeUseKind.Parameter)),
                new ParameterSignature(
                    "c",
                    1,
                    new TypeUseSignature(
                        new GenericParameterTypeSignature(secondInnerParameter),
                        RefKind.None,
                        TypeUseKind.Parameter)),
                new ParameterSignature(
                    "a",
                    2,
                    new TypeUseSignature(
                        new GenericParameterTypeSignature(outerParameter),
                        RefKind.None,
                        TypeUseKind.Parameter))
            },
            new[] { methodParameter },
            Array.Empty<TypeSignature>(),
            Array.Empty<AttributeSignature>(),
            Array.Empty<AttributeSignature>());
    }

    private static MethodSignature SingleGenericCandidate()
    {
        var assembly = new AssemblyIdentity("Example.Assembly");
        var clsIdentity = new TypeIdentity(
            assembly,
            "X.Y.Z",
            "Cls",
            1,
            TypeKind.Class);
        var parameter = new GenericParameterSignature(
            "A",
            GenericParameterOwnerKind.Type,
            clsIdentity.FullMetadataName,
            assembly,
            0);
        return new MethodSignature(
            "M",
            MethodKind.OrdinaryMethod,
            Accessibility.Public,
            isStatic: false,
            isAsync: false,
            new NamedTypeSignature(clsIdentity),
            new TypeUseSignature(
                Named("System", "Void", TypeKind.Struct),
                RefKind.None,
                TypeUseKind.Return),
            new[]
            {
                new ParameterSignature(
                    "a",
                    0,
                    new TypeUseSignature(
                        new GenericParameterTypeSignature(parameter),
                        RefKind.None,
                        TypeUseKind.Parameter))
            },
            Array.Empty<GenericParameterSignature>(),
            Array.Empty<TypeSignature>(),
            Array.Empty<AttributeSignature>(),
            Array.Empty<AttributeSignature>());
    }

    private static MethodSignature MethodOnlyGenericCandidate()
    {
        var assembly = new AssemblyIdentity("Example.Assembly");
        var clsIdentity = new TypeIdentity(
            assembly,
            "X.Y.Z",
            "Cls",
            0,
            TypeKind.Class);
        var parameter = new GenericParameterSignature(
            "A",
            GenericParameterOwnerKind.Method,
            clsIdentity.FullMetadataName + ".M`1",
            assembly,
            0);
        return new MethodSignature(
            "M",
            MethodKind.OrdinaryMethod,
            Accessibility.Public,
            isStatic: false,
            isAsync: false,
            new NamedTypeSignature(clsIdentity),
            new TypeUseSignature(
                Named("System", "Void", TypeKind.Struct),
                RefKind.None,
                TypeUseKind.Return),
            new[]
            {
                new ParameterSignature(
                    "a",
                    0,
                    new TypeUseSignature(
                        new GenericParameterTypeSignature(parameter),
                        RefKind.None,
                        TypeUseKind.Parameter))
            },
            new[] { parameter },
            Array.Empty<TypeSignature>(),
            Array.Empty<AttributeSignature>(),
            Array.Empty<AttributeSignature>());
    }

    private static MethodSignature ShadowedGenericCandidate()
    {
        var assembly = new AssemblyIdentity("Example.Assembly");
        var clsIdentity = new TypeIdentity(
            assembly,
            "X.Y.Z",
            "Cls",
            1,
            TypeKind.Class);
        var methodParameter = new GenericParameterSignature(
            "TM",
            GenericParameterOwnerKind.Method,
            "X.Y.Z.Cls`1.M`1",
            assembly,
            0);
        return new MethodSignature(
            "M",
            MethodKind.OrdinaryMethod,
            Accessibility.Public,
            isStatic: false,
            isAsync: false,
            new NamedTypeSignature(clsIdentity),
            new TypeUseSignature(
                Named("System", "Void", TypeKind.Struct),
                RefKind.None,
                TypeUseKind.Return),
            new[]
            {
                new ParameterSignature(
                    "x",
                    0,
                    new TypeUseSignature(
                        new GenericParameterTypeSignature(methodParameter),
                        RefKind.None,
                        TypeUseKind.Parameter))
            },
            new[] { methodParameter },
            Array.Empty<TypeSignature>(),
            Array.Empty<AttributeSignature>(),
            Array.Empty<AttributeSignature>());
    }

    private static MethodSignature ConstructedUseCandidate()
    {
        var assembly = new AssemblyIdentity("Example.Assembly");
        var clsIdentity = new TypeIdentity(
            assembly,
            "X.Y.Z",
            "Cls",
            1,
            TypeKind.Class);
        var listIdentity = new TypeIdentity(
            assembly,
            "System.Collections.Generic",
            "List",
            1,
            TypeKind.Class);
        var typeParameter = new GenericParameterSignature(
            "TM",
            GenericParameterOwnerKind.Type,
            clsIdentity.FullMetadataName,
            assembly,
            0);
        return new MethodSignature(
            "M",
            MethodKind.OrdinaryMethod,
            Accessibility.Public,
            isStatic: false,
            isAsync: false,
            new NamedTypeSignature(clsIdentity),
            new TypeUseSignature(
                Named("System", "Void", TypeKind.Struct),
                RefKind.None,
                TypeUseKind.Return),
            new[]
            {
                new ParameterSignature(
                    "items",
                    0,
                    new TypeUseSignature(
                        new NamedTypeSignature(
                            listIdentity,
                            new[]
                            {
                                new GenericParameterTypeSignature(typeParameter)
                            }),
                        RefKind.None,
                        TypeUseKind.Parameter))
            },
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
