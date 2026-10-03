using AspectN.Parsing;
using AspectN.Syntax;
using AspectN.Text;

namespace AspectN;

/// <summary>Provides entry points for parsing AspectN expressions and type patterns.</summary>
public static class AspectNParser
{
    /// <summary>Parses member expression input.</summary>
    /// <param name="source">Specifies the source.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static AspectNSyntaxTree ParseMemberExpression(
        string source)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(source);
#else
        if (source is null) throw new ArgumentNullException(nameof(source));
#endif

        return new Parser(SourceText.From(source)).ParseMemberExpression();
    }

    /// <summary>Parses type pattern input.</summary>
    /// <param name="source">Specifies the source.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static AspectNSyntaxTree ParseTypePattern(
        string source)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(source);
#else
        if (source is null) throw new ArgumentNullException(nameof(source));
#endif

        return new TypePatternParser(SourceText.From(source)).Parse();
    }
}
