using AspectN.Diagnostics;
using AspectN.Syntax;

namespace AspectN.Tests.Parsing;

public sealed class TypeSyntaxParityTests
{
    public static TheoryData<string, bool> CommonTypes
    {
        get
        {
            string[] sources =
            [
                "*", "*Service", "Company..Services.*Service", "Outer<string>/Inner<int[,]>",
                "List<int[]>", "Map<string,List<int?[]>>", "Box<>", "Box<,>", "Box<,,>",
                "Box<..>", "Box<!>", "Box<?>", "Box<,int>", "Box<int,>", "Box<int,,string>",
                "$T", "Pair<$T,$U>", "int?[]?", "int[ , ][]", "IDisposable+",
                "(int,string,List<int>)", "!(A || B)", "A || B && !C", "tasklike", "tasklike<List<int[]>>"
            ];
            var cases = new TheoryData<string, bool>();
            foreach (var source in sources)
            {
                cases.Add(source, false);
                cases.Add(source, true);
            }
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(CommonTypes))]
    public void Parse_CommonTypeInReturnOrParameter_PreservesSameSyntax(string source, bool parameter)
    {
        var standalone = AspectNParser.ParseTypePattern(source);
        var member = AspectNParser.ParseMemberExpression(parameter
            ? $"method(* Load({source}))" : $"method({source} Load())");

        Assert.Empty(standalone.Diagnostics);
        Assert.Empty(member.Diagnostics);
        var expected = Assert.IsAssignableFrom<TypePatternSyntax>(standalone.Root);
        var actual = GetTypeUse(member, parameter).TypePattern;
        AssertSameShape(expected, actual, actual.FullSpan.Start - expected.FullSpan.Start);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("Company..Services.*Service")]
    [InlineData("Outer<string>/Inner<int[,]>")]
    [InlineData("Box<!>")]
    [InlineData("Box<?>")]
    [InlineData("Box<int,,string>")]
    [InlineData("Pair<$T,$U>")]
    [InlineData("int?[]")]
    [InlineData("IDisposable+")]
    [InlineData("(A || B)")]
    [InlineData("Outer<tasklike<int>>/Inner<?>")]
    public void Parse_DeclaringTypeBoundary_PreservesTypeAndFollowingMember(string source)
    {
        var standalone = AspectNParser.ParseTypePattern(source);
        var member = AspectNParser.ParseMemberExpression($"method(* {source}.Load())");

        Assert.Empty(standalone.Diagnostics);
        Assert.Empty(member.Diagnostics);
        var path = Assert.Single(GetSignature(member).Parts.OfType<MemberPathSyntax>());
        Assert.NotNull(path.DeclaringType);
        Assert.Equal(source.Length + "method(* ".Length, path.SeparatorToken!.Span.Start);
        Assert.Equal("Load", Assert.IsType<NamedMemberPatternSyntax>(path.Name).Segment.NameParts.Single().Text);
        AssertSameShape(standalone.Root, path.DeclaringType!, "method(* ".Length);
    }

    [Theory]
    [InlineData("ref int")]
    [InlineData("out int")]
    [InlineData("in int")]
    [InlineData("ref readonly int")]
    [InlineData("!ref int")]
    public void Parse_RefAnnotation_RemainsSpecificToMemberTypeUse(string source)
    {
        var standalone = AspectNParser.ParseTypePattern(source);
        var member = AspectNParser.ParseMemberExpression($"method(* Load({source}))");

        Assert.Contains(standalone.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.InvalidRefKind);
        Assert.Empty(member.Diagnostics);
        var typeUse = GetTypeUse(member, parameter: true);
        var annotation = string.Join(" ", typeUse.NegatedRefTokens.Concat(typeUse.RefKindTokens).Select(token => token.Text));
        Assert.Equal(source.StartsWith('!') ? "! ref" : source[..^4], annotation);
        Assert.IsType<NamedTypePatternSyntax>(typeUse.TypePattern);
    }

    [Fact]
    public void Parse_TaskLikeInDeclaringType_RemainsNameWhileReturnKeepsSpecialMeaning()
    {
        var tree = AspectNParser.ParseMemberExpression("method(tasklike tasklike<tasklike<int>>.Load(ref readonly int))");

        Assert.Empty(tree.Diagnostics);
        Assert.IsType<TaskLikeTypePatternSyntax>(GetTypeUse(tree, parameter: false).TypePattern);
        var path = Assert.Single(GetSignature(tree).Parts.OfType<MemberPathSyntax>());
        var name = Assert.IsType<NamedTypePatternSyntax>(path.DeclaringType);
        var segment = Assert.Single(name.Segments);
        Assert.Equal("tasklike", Assert.Single(segment.NameParts).Text);
        Assert.IsType<TaskLikeTypePatternSyntax>(Assert.Single(segment.GenericArguments!.Arguments));
        Assert.Equal("ref readonly", string.Join(" ", GetTypeUse(tree, parameter: true).RefKindTokens.Select(token => token.Text)));
    }

    [Theory]
    [InlineData("int []")]
    [InlineData("int ?")]
    [InlineData("int +")]
    [InlineData("Outer/ Inner")]
    [InlineData("tasklike <int>")]
    [InlineData("tasklike<>")]
    [InlineData("(int)")]
    [InlineData("()")]
    [InlineData("(int,)")]
    public void Parse_InvalidCommonType_PreservesDiagnosticsAndRecovery(string source)
    {
        var standalone = AspectNParser.ParseTypePattern(source);
        var member = AspectNParser.ParseMemberExpression($"method({source} Load())");

        Assert.NotEmpty(standalone.Diagnostics);
        Assert.Equal(Diagnostics(standalone, 0), Diagnostics(member, "method(".Length));
        AssertSameShape(standalone.Root, GetTypeUse(member, parameter: false).TypePattern, "method(".Length);
    }

    [Theory]
    [InlineData("$T", "type", 4)]
    [InlineData("$T", "return", 18)]
    [InlineData("$T", "parameter", 22)]
    [InlineData("List<int>", "type", 10)]
    [InlineData("List<int>", "return", 26)]
    [InlineData("List<int>", "parameter", 28)]
    [InlineData("Box<List<$T>>", "type", 16)]
    [InlineData("Box<List<$T>>", "return", 32)]
    [InlineData("Box<List<$T>>", "parameter", 34)]
    public void Parse_NodeBudgetBoundary_PreservesExistingReservationCount(string type, string entry, int requiredNodes)
    {
        var source = entry switch { "type" => type, "return" => $"method({type} Load())", _ => $"method(* Load({type}))" };
        AspectNSyntaxTree Parse(int limit)
        {
            var options = new AspectNParseOptions(maximumSyntaxNodes: ResourceLimit.Finite(limit));
            return entry == "type" ? AspectNParser.ParseTypePattern(source, options)
                : AspectNParser.ParseMemberExpression(source, options);
        }

        var failure = Assert.Single(Parse(requiredNodes - 1).Diagnostics);
        Assert.Equal(DiagnosticCodes.ParseResourceLimitExceeded, failure.Code);
        Assert.Equal($"Parsing exceeded MaximumSyntaxNodes ({requiredNodes - 1}); used {requiredNodes - 1}, requested 1 additional unit.", failure.Message);
        Assert.Empty(Parse(requiredNodes).Diagnostics);
    }

    [Fact]
    public void Parse_NestedTypesAndMethodArguments_PreservesParametersAndFollowingSelector()
    {
        var tree = AspectNParser.ParseMemberExpression(
            "method(tasklike<List<int?[,]>> Outer<$T>/Inner<string>.Load<$U>(ref $U, (int,string))) && property(Box<> Name)");

        Assert.Empty(tree.Diagnostics);
        var expression = Assert.IsType<BinaryExpressionSyntax>(tree.Root);
        var signature = Assert.IsType<MethodSignatureSyntax>(Assert.IsType<MethodSelectorSyntax>(expression.Left).Body);
        Assert.IsType<PropertySelectorSyntax>(expression.Right);
        var parameters = signature.Parts.OfType<ParameterSyntax>().ToArray();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("ref", Assert.Single(parameters[0].TypeUse.RefKindTokens).Text);
        Assert.IsType<VariableTypePatternSyntax>(parameters[0].TypeUse.TypePattern);
        Assert.IsType<TupleTypePatternSyntax>(parameters[1].TypeUse.TypePattern);
        var path = Assert.Single(signature.Parts.OfType<MemberPathSyntax>());
        Assert.Equal(1, Assert.IsType<NamedMemberPatternSyntax>(path.Name).Segment.GenericArguments!.Arity);
    }

    private static SignatureSyntax GetSignature(AspectNSyntaxTree tree) =>
        Assert.IsAssignableFrom<SignatureSyntax>(Assert.IsAssignableFrom<SelectorSyntax>(tree.Root).Body);

    private static TypeUseSyntax GetTypeUse(AspectNSyntaxTree tree, bool parameter)
    {
        var signature = GetSignature(tree);
        return parameter ? Assert.Single(signature.Parts.OfType<ParameterSyntax>()).TypeUse
            : Assert.Single(signature.Parts.OfType<TypeUseSyntax>());
    }

    private static IEnumerable<object> Diagnostics(AspectNSyntaxTree tree, int offset) => tree.Diagnostics.Select(item =>
        (object)(item.Code, item.Severity, item.Message, item.Span!.Value.Start - offset, item.Span.Value.Length));

    private static void AssertSameShape(SyntaxNode expected, SyntaxNode actual, int offset)
    {
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.FullSpan.Start + offset, actual.FullSpan.Start);
        Assert.Equal(expected.FullSpan.Length, actual.FullSpan.Length);
        Assert.Equal(expected.Children.Count, actual.Children.Count);
        if (expected is SyntaxToken token)
        {
            var other = Assert.IsType<SyntaxToken>(actual);
            Assert.Equal(token.Text, other.Text);
            Assert.Equal(token.Value, other.Value);
            Assert.Equal(token.IsMissing, other.IsMissing);
        }
        if (expected is GenericPatternListSyntax generic)
        {
            var other = Assert.IsType<GenericPatternListSyntax>(actual);
            Assert.Equal(generic.Form, other.Form);
            Assert.Equal(generic.Arity, other.Arity);
        }
        for (var index = 0; index < expected.Children.Count; index++)
            AssertSameShape(expected.Children[index], actual.Children[index], offset);
    }
}
