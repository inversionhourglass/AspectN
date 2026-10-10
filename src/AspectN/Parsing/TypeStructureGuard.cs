using AspectN.Syntax;

namespace AspectN.Parsing;

internal static class TypeStructureGuard
{
    public static bool ExceedsLimit(SyntaxNode root)
    {
        var pending = new Stack<(SyntaxNode Node, int Depth)>();
        pending.Push((root, 0));
        while (pending.Count > 0)
        {
            var (node, depth) = pending.Pop();
            if (node is TypePatternSyntax) depth++;
            if (depth > AspectNLimits.MaximumTypeDepth) return true;
            if (node is NamedTypePatternSyntax named &&
                named.Separators.Count(separator => separator.Kind == SyntaxKind.SlashToken) + depth > AspectNLimits.MaximumTypeDepth)
                return true;
            foreach (var child in node.Children) pending.Push((child, depth));
        }
        return false;
    }
}
