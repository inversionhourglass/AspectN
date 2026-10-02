using AspectN.Binding;
using AspectN.Evaluation;
using AspectN.Metadata;

namespace AspectN.Tests.Evaluation;

public sealed class RefKindTests
{
    [Theory]
    [InlineData("int", RefKind.None, true)]
    // Standard（规范 §11）：未标注 = 不约束，任意 ref kind 都命中。
    [InlineData("int", RefKind.Ref, true)]
    [InlineData("int", RefKind.Out, true)]
    [InlineData("int", RefKind.In, true)]
    // !ref = 仅非 by-ref。
    [InlineData("!ref int", RefKind.None, true)]
    [InlineData("!ref int", RefKind.Ref, false)]
    [InlineData("!ref int", RefKind.Out, false)]
    [InlineData("ref int", RefKind.None, false)]
    [InlineData("ref int", RefKind.Ref, true)]
    [InlineData("out int", RefKind.Out, true)]
    [InlineData("in int", RefKind.In, true)]
    [InlineData("ref readonly int", RefKind.RefReadOnly, true)]
    public void IsRefKindMatch_ParameterPattern_ReturnsExpected(
        string parameterPattern,
        RefKind candidateRefKind,
        bool expected)
    {
        var parameter = BindParameter(parameterPattern);
        var candidate = new TypeUseSignature(
            Named("Int32", TypeKind.Struct),
            candidateRefKind,
            TypeUseKind.Parameter);

        var result = ModifierMatcher.IsRefKindMatch(
            parameter,
            candidate,
            isParameter: true,
            AspectNLanguageVersion.Standard);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(RefKind.None)]
    [InlineData(RefKind.Ref)]
    [InlineData(RefKind.Out)]
    [InlineData(RefKind.In)]
    [InlineData(RefKind.RefReadOnly)]
    public void IsRefKindMatch_FullWildcardParameter_IgnoresRefKind(
        RefKind candidateRefKind)
    {
        var parameter = BindParameter("*");
        var candidate = new TypeUseSignature(
            Named("Int32", TypeKind.Struct),
            candidateRefKind,
            TypeUseKind.Parameter);

        Assert.True(
            ModifierMatcher.IsRefKindMatch(
                parameter,
                candidate,
                isParameter: true,
                AspectNLanguageVersion.Standard));
    }

    [Fact]
    public void IsRefKindMatch_RefReadonlyReturn_MatchesExactly()
    {
        var signature = BindCallable(
            "method(ref readonly int Service.Get())");
        var matching = new TypeUseSignature(
            Named("Int32", TypeKind.Struct),
            RefKind.RefReadOnly,
            TypeUseKind.Return);
        var byValue = new TypeUseSignature(
            Named("Int32", TypeKind.Struct),
            RefKind.None,
            TypeUseKind.Return);

        Assert.True(
            ModifierMatcher.IsRefKindMatch(
                signature.ReturnType,
                matching,
                isParameter: false,
                AspectNLanguageVersion.Standard));
        Assert.False(
            ModifierMatcher.IsRefKindMatch(
                signature.ReturnType,
                byValue,
                isParameter: false,
                AspectNLanguageVersion.Standard));
    }

    private static BoundTypeUsePattern BindParameter(string parameterPattern)
    {
        return Assert.Single(
            BindCallable($"method(* Service.Get({parameterPattern}))")
                .Parameters);
    }

    private static BoundMethodSignature BindCallable(string source)
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(source));
        var selector = Assert.IsType<BoundSelector>(compilation.BoundRoot);
        return Assert.IsType<BoundMethodSignature>(selector.Payload);
    }

    private static NamedTypeSignature Named(
        string name,
        TypeKind kind = TypeKind.Class)
    {
        return new NamedTypeSignature(
            new TypeIdentity(
                new AssemblyIdentity("Example.Assembly"),
                "System",
                name,
                0,
                kind));
    }
}
