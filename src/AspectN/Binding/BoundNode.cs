using AspectN.Text;

namespace AspectN.Binding;

internal abstract class BoundNode
{
    protected BoundNode(TextSpan span)
    {
        Span = span;
    }

    public TextSpan Span { get; }
}
