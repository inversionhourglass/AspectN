using AspectN.Parsing;
using AspectN.Syntax;
using AspectN.Text;

namespace AspectN;

public static class AspectNParser
{
    public static AspectNSyntaxTree ParseMemberExpression(
        string source,
        AspectNParseOptions? options = null)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(source);
#else
        if (source is null) throw new ArgumentNullException(nameof(source));
#endif

        return new Parser(
            SourceText.From(source),
            options ?? new AspectNParseOptions()).ParseMemberExpression();
    }

    public static AspectNSyntaxTree ParseTypePattern(
        string source,
        AspectNParseOptions? options = null)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(source);
#else
        if (source is null) throw new ArgumentNullException(nameof(source));
#endif

        return new TypePatternParser(
            SourceText.From(source),
            options ?? new AspectNParseOptions()).Parse();
    }
}
