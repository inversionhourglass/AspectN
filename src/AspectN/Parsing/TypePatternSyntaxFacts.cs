using AspectN.Syntax;

namespace AspectN.Parsing;

internal static class TypePatternSyntaxFacts
{
    public static bool IsNamePart(SyntaxToken token)
    {
        return token.Kind is SyntaxKind.IdentifierToken or SyntaxKind.StarToken;
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
