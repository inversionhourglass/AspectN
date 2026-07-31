using AspectN.Text;

namespace AspectN.Syntax;

public sealed class SyntaxToken
{
    public SyntaxToken(
        SyntaxKind kind,
        TextSpan span,
        string text,
        object? value = null)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(text);
#else
        if (text is null) throw new ArgumentNullException(nameof(text));
#endif

        Kind = kind;
        Span = span;
        Text = text;
        Value = value;
    }

    public SyntaxKind Kind { get; }

    public TextSpan Span { get; }

    public string Text { get; }

    public object? Value { get; }
}
