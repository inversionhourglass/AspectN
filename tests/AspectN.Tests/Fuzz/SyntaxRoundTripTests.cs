using AspectN.Parsing;
using AspectN.Syntax;

namespace AspectN.Tests.Fuzz;

public sealed class SyntaxRoundTripTests
{
    public static TheoryData<AspectNParseKind, string> CanonicalSources => new()
    {
        {
            AspectNParseKind.MemberExpression,
            "method(public static string MyCompany.Services.UserService.Get*(..))"
        },
        {
            AspectNParseKind.MemberExpression,
            "callable(!public * *..*.*(..))"
        },
        {
            AspectNParseKind.MemberExpression,
            "accessor(public string MyCompany.Models.User.Name)"
        },
        {
            AspectNParseKind.MemberExpression,
            "method(tasklike<string> *..*.*Async(..))"
        },
        {
            AspectNParseKind.MemberExpression,
            "method($T Converter.Convert<$T>($T))"
        },
        {
            AspectNParseKind.MemberExpression,
            "method(* Service.*(..)) && !attr(callable ObsoleteAttribute)"
        },
        {
            AspectNParseKind.TypePattern,
            "MyCompany..Services.*Service"
        },
        {
            AspectNParseKind.TypePattern,
            "Outer<T>/Inner<string>"
        },
        {
            AspectNParseKind.TypePattern,
            "System.IDisposable+"
        },
        { AspectNParseKind.TypePattern, "int?[]" },
        { AspectNParseKind.TypePattern, "(int, string)" },
        { AspectNParseKind.TypePattern, "tasklike<void>" },
        { AspectNParseKind.TypePattern, "ref readonly int" },
        { AspectNParseKind.TypePattern, "Name<..>" }
    };

    [Theory]
    [MemberData(nameof(CanonicalSources))]
    public void Parse_CanonicalSource_ReparsePreservesSyntaxShape(
        AspectNParseKind parseKind,
        string source)
    {
        var first = Parse(parseKind, source);

        Assert.Empty(first.Diagnostics);
        var canonicalSource = first.Text.ToString();
        var second = Parse(parseKind, canonicalSource);

        Assert.Empty(second.Diagnostics);
        Assert.Equal(first.ParseKind, second.ParseKind);
        AssertSameShape(first.Root, second.Root);
    }

    private static AspectNSyntaxTree Parse(
        AspectNParseKind parseKind,
        string source)
    {
        return parseKind == AspectNParseKind.MemberExpression
            ? AspectNParser.ParseMemberExpression(source)
            : AspectNParser.ParseTypePattern(source);
    }

    private static void AssertSameShape(SyntaxNode expected, SyntaxNode actual)
    {
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.Children.Count, actual.Children.Count);

        if (expected is SyntaxToken expectedToken)
        {
            var actualToken = Assert.IsType<SyntaxToken>(actual);
            Assert.Equal(expectedToken.Text, actualToken.Text);
            Assert.Equal(expectedToken.Value, actualToken.Value);
            Assert.Equal(expectedToken.IsMissing, actualToken.IsMissing);
        }

        for (var index = 0; index < expected.Children.Count; index++)
        {
            AssertSameShape(expected.Children[index], actual.Children[index]);
        }
    }
}
