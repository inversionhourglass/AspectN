using AspectN.Text;

namespace AspectN.Syntax;

public sealed class SyntaxToken : SyntaxNode
{
    public SyntaxToken(
        SyntaxKind kind,
        TextSpan span,
        string text,
        object? value = null,
        bool isMissing = false)
        : base(kind, span)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(text);
#else
        if (text is null) throw new ArgumentNullException(nameof(text));
#endif

        Text = text;
        Value = value;
        IsMissing = isMissing;
    }

    public string Text { get; }

    public object? Value { get; }

    public bool IsMissing { get; }

    public TextSpan Span => FullSpan;

    public static SyntaxToken Missing(SyntaxKind kind, int position)
    {
        return new SyntaxToken(
            kind,
            new TextSpan(position, 0),
            string.Empty,
            isMissing: true);
    }
}
