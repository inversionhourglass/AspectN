using System.Collections.ObjectModel;
using AspectN.Text;

namespace AspectN.Syntax;

/// <summary>Represents syntax node.</summary>
public abstract class SyntaxNode
{
    private readonly IReadOnlyList<SyntaxNode> _children;

    protected SyntaxNode(SyntaxKind kind, TextSpan span)
    {
        Kind = kind;
        FullSpan = span;
        _children = Array.Empty<SyntaxNode>();
    }

    protected SyntaxNode(SyntaxKind kind, IEnumerable<SyntaxNode> children)
    {
        var copy = children.ToArray();
        Kind = kind;
        _children = new ReadOnlyCollection<SyntaxNode>(copy);
        FullSpan = GetFullSpan(copy);
    }

    /// <summary>Gets the kind.</summary>
    public SyntaxKind Kind { get; }

    /// <summary>Gets the full span.</summary>
    public TextSpan FullSpan { get; }

    /// <summary>Gets the children.</summary>
    public IReadOnlyList<SyntaxNode> Children => _children;

    private static TextSpan GetFullSpan(SyntaxNode[] children)
    {
        if (children.Length == 0) return new TextSpan(0, 0);

        var start = children[0].FullSpan.Start;
        var end = children[children.Length - 1].FullSpan.End;
        return new TextSpan(start, end - start);
    }
}

internal static class SyntaxChildren
{
    public static IReadOnlyList<SyntaxNode> Create(params object?[] parts)
    {
        var children = new List<SyntaxNode>();
        foreach (var part in parts)
        {
            switch (part)
            {
                case null:
                    continue;
                case SyntaxNode node:
                    children.Add(node);
                    break;
                case IEnumerable<SyntaxNode> nodes:
                    children.AddRange(nodes);
                    break;
                default:
                    throw new ArgumentException(
                        "Syntax children must be syntax nodes or node sequences.",
                        nameof(parts));
            }
        }

        return children;
    }

    public static IReadOnlyList<SyntaxNode> Interleave<TNode>(
        IReadOnlyList<TNode> nodes,
        IReadOnlyList<SyntaxToken> separators)
        where TNode : SyntaxNode
    {
        var children = new List<SyntaxNode>(nodes.Count + separators.Count);
        for (var index = 0; index < nodes.Count; index++)
        {
            children.Add(nodes[index]);
            if (index < separators.Count) children.Add(separators[index]);
        }

        return children;
    }
}
