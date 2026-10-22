using System.Collections.ObjectModel;
using AspectN.Text;

namespace AspectN.Syntax;

/// <summary>Represents syntax node.</summary>
public abstract class SyntaxNode
{
    private readonly IReadOnlyList<SyntaxNode> _children;

    /// <summary>Initializes a leaf syntax node with an explicit source span and no children.</summary>
    /// <param name="kind">The syntax kind to store.</param>
    /// <param name="span">The node's source span in UTF-16 code units.</param>
    protected SyntaxNode(SyntaxKind kind, TextSpan span)
    {
        Kind = kind;
        FullSpan = span;
        _children = Array.Empty<SyntaxNode>();
        Depth = 1;
    }

    /// <summary>Initializes a syntax node from a snapshot of its children and derives its span from their source order.</summary>
    /// <param name="kind">The syntax kind to store.</param>
    /// <param name="children">Non-null child nodes in source order. An empty sequence produces a zero-length span at position zero.</param>
    /// <exception cref="ArgumentNullException"><paramref name="children"/> is null.</exception>
    protected SyntaxNode(SyntaxKind kind, IEnumerable<SyntaxNode> children)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(children);
#else
        if (children is null) throw new ArgumentNullException(nameof(children));
#endif
        var copy = children.ToArray();
        Kind = kind;
        _children = new ReadOnlyCollection<SyntaxNode>(copy);
        FullSpan = GetFullSpan(copy);
        Depth = copy.Length == 0 ? 1 : 1 + copy.Max(child => child.Depth);
    }

    /// <summary>Gets the kind.</summary>
    public SyntaxKind Kind { get; }

    /// <summary>Gets the full span.</summary>
    public TextSpan FullSpan { get; }

    /// <summary>Gets the children.</summary>
    public IReadOnlyList<SyntaxNode> Children => _children;

    internal int Depth { get; }

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
