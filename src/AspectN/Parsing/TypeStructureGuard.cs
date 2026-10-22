using AspectN.Syntax;

namespace AspectN.Parsing;

internal static class TypeStructureGuard
{
    public static bool ExceedsLimit(SyntaxNode root, AspectNParseOptions options, CancellationToken cancellationToken)
    {
        var pending = new Stack<(SyntaxNode Node, int Depth)>();
        pending.Push((root, 0));
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (node, depth) = pending.Pop();
            if (node is TypePatternSyntax) depth++;
            if (options.MaximumTypeDepth.ExceededBy(depth)) return true;
            if (node is NamedTypePatternSyntax named &&
                options.MaximumTypeDepth.ExceededBy((long)named.Separators.Count(separator => separator.Kind == SyntaxKind.SlashToken) + depth))
                return true;
            foreach (var child in node.Children) pending.Push((child, depth));
        }
        return false;
    }
}
