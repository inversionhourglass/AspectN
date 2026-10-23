using AspectN.Binding;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

public sealed class ModifierMatcherTests
{
    [Theory]
    [InlineData(Accessibility.PrivateProtected, true)]
    [InlineData(Accessibility.Private, false)]
    [InlineData(Accessibility.Protected, false)]
    public void Match_StandardPrivateProtected_IsCompositeAccessibility(
        Accessibility candidate,
        bool expected)
    {
        var signature = Bind(
            "method(private protected * Service.Get(..))");

        var result = ModifierMatcher.IsMatch(
            signature.Modifiers,
            candidate,
            isStatic: false);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("static", true, true)]
    [InlineData("static", false, false)]
    [InlineData("!static", true, false)]
    [InlineData("!static", false, true)]
    [InlineData("!public", false, true)]
    [InlineData("!public", true, false)]
    public void Match_NegatedAndStaticModifiers_ReturnExpected(
        string modifier,
        bool candidateValue,
        bool expected)
    {
        var signature = Bind(
            $"method({modifier} * Service.Get(..))");
        var accessibility = modifier.Contains("public", StringComparison.Ordinal)
            ? candidateValue
                ? Accessibility.Public
                : Accessibility.Private
            : Accessibility.Public;

        var result = ModifierMatcher.IsMatch(
            signature.Modifiers,
            accessibility,
            candidateValue);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Accessibility.Public, true)]
    [InlineData(Accessibility.Private, true)]
    [InlineData(Accessibility.Protected, true)]
    [InlineData(Accessibility.Internal, true)]
    [InlineData(Accessibility.ProtectedInternal, false)]
    public void Match_NegatedProtectedInternal_NegatesWholeComposite(
        Accessibility candidate,
        bool expected)
    {
        var signature = Bind(
            "method(!protected internal * Service.Get(..))");

        var result = ModifierMatcher.IsMatch(
            signature.Modifiers,
            candidate,
            isStatic: false);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Accessibility.Public, true)]
    [InlineData(Accessibility.Protected, true)]
    [InlineData(Accessibility.Internal, true)]
    [InlineData(Accessibility.PrivateProtected, false)]
    public void Match_NegatedPrivateProtected_NegatesWholeComposite(
        Accessibility candidate,
        bool expected)
    {
        var signature = Bind(
            "method(!private protected * Service.Get(..))");

        var result = ModifierMatcher.IsMatch(
            signature.Modifiers,
            candidate,
            isStatic: false);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Accessibility.Private, true)]
    [InlineData(Accessibility.Internal, true)]
    [InlineData(Accessibility.Public, false)]
    [InlineData(Accessibility.PrivateProtected, false)]
    public void Match_StandardAlternation_MatchesAnyAlternative(
        Accessibility candidate,
        bool expected)
    {
        var signature = Bind(
            "method(private|internal * Service.Get(..))");

        var result = ModifierMatcher.IsMatch(
            signature.Modifiers,
            candidate,
            isStatic: false);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Accessibility.PrivateProtected, true)]
    [InlineData(Accessibility.ProtectedInternal, true)]
    [InlineData(Accessibility.Public, true)]
    [InlineData(Accessibility.Private, false)]
    [InlineData(Accessibility.Internal, false)]
    public void Match_StandardCompoundAlternation_MatchesWholeComposite(
        Accessibility candidate,
        bool expected)
    {
        var signature = Bind(
            "method((private protected)|(protected internal)|public * Service.Get(..))");

        var result = ModifierMatcher.IsMatch(
            signature.Modifiers,
            candidate,
            isStatic: false);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Accessibility.PrivateProtected, true)]
    [InlineData(Accessibility.Private, false)]
    [InlineData(Accessibility.Internal, false)]
    [InlineData(Accessibility.Public, false)]
    public void Match_StandardParenthesizedCompoundWithoutPipe_MatchesWholeComposite(
        Accessibility candidate,
        bool expected)
    {
        // 仅为可读性包裹复合权限（无 |）与裸复合形式语义一致。
        var signature = Bind(
            "method((private protected) * Service.Get(..))");

        var result = ModifierMatcher.IsMatch(
            signature.Modifiers,
            candidate,
            isStatic: false);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("method(* Run(..))", Accessibility.Public, true, MatchStatus.Matched)]
    [InlineData("method(public static * Run(..))", Accessibility.Public, true, MatchStatus.Matched)]
    [InlineData("method(private static * Run(..))", Accessibility.Public, true, MatchStatus.NotMatched)]
    [InlineData("method(public|internal static * Run(..))", Accessibility.Internal, true, MatchStatus.Matched)]
    [InlineData("method(!public !static * Run(..))", Accessibility.Internal, false, MatchStatus.Matched)]
    public void Evaluate_KnownModifiers_DoesNotAllocate(string source, Accessibility accessibility, bool isStatic,
        MatchStatus expected)
    {
        var signature = Bind(source);
        var context = new EvaluationContext(new StubTypeSystem(), default);
        var availability = MetadataAvailability.Complete;
        var status = MatchStatus.Unknown;
        // 在计量外完成绑定、上下文构造和预热，只约束已知事实的修饰符判断。
        for (var index = 0; index < 1024; index++)
            status = ModifierMatcher.Evaluate(signature.Modifiers, accessibility, isStatic, availability, signature, context);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 256; index++)
            status = ModifierMatcher.Evaluate(signature.Modifiers, accessibility, isStatic, availability, signature, context);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(expected, status);
        Assert.Equal(0L, allocated);
    }

    private static BoundMethodSignature Bind(
        string source)
    {
        var tree = AspectNParser.ParseMemberExpression(source);
        var compilation = AspectNCompilation.Create(tree);
        var selector = Assert.IsType<BoundSelector>(compilation.BoundRoot);
        return Assert.IsType<BoundMethodSignature>(selector.Payload);
    }
}
