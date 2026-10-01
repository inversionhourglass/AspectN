using AspectN.Syntax;

namespace AspectN.Parsing;

internal static class LegacyParsingRules
{
    public static bool IsNamePart(SyntaxToken token, AspectNLanguageVersion version)
    {
        return token.Kind is SyntaxKind.IdentifierToken or SyntaxKind.StarToken ||
               (version == AspectNLanguageVersion.Legacy &&
                token.Kind == SyntaxKind.IntegerToken &&
                token.Text.Length > 0 && token.Text[0] != '-');
    }

    public static bool IsBooleanGroup(TypePatternSyntax pattern)
    {
        while (pattern is ParenthesizedTypePatternSyntax parenthesized)
        {
            pattern = parenthesized.Pattern;
        }

        return pattern is BinaryTypePatternSyntax or UnaryTypePatternSyntax;
    }
}
