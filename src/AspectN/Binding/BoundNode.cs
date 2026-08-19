using AspectN.Text;

namespace AspectN.Binding;

internal abstract class BoundNode(TextSpan span)
{
    public TextSpan Span { get; } = span;
}
