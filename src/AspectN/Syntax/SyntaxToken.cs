using AspectN.Text;

namespace AspectN.Syntax;

/// <summary>Represents syntax token.</summary>
public sealed class SyntaxToken : SyntaxNode
{
    /// <summary>Initializes a new instance of the SyntaxToken class.</summary>
    /// <param name="kind">The lexical kind of the token.</param>
    /// <param name="span">The token range in UTF-16 source code units.</param>
    /// <param name="text">The original token text, or an empty string for a missing token.</param>
    /// <param name="value">The interpreted token value when supplied by the lexer, or null.</param>
    /// <param name="isMissing">Whether this token was inserted for error recovery rather than read from the source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
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

    /// <summary>Gets the text.</summary>
    public string Text { get; }

    /// <summary>Gets the token value.</summary>
    public object? Value { get; }

    /// <summary>Gets whether the parser inserted this token during error recovery.</summary>
    public bool IsMissing { get; }

    /// <summary>Gets the span.</summary>
    public TextSpan Span => FullSpan;

    /// <summary>Creates a missing token of the specified kind.</summary>
    /// <param name="kind">The lexical kind of the token.</param>
    /// <param name="position">The nonnegative source offset at which the missing token is inserted.</param>
    /// <returns>A missing token with empty text and a zero-length span at the requested position.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is negative.</exception>
    public static SyntaxToken Missing(SyntaxKind kind, int position)
    {
        return new SyntaxToken(
            kind,
            new TextSpan(position, 0),
            string.Empty,
            isMissing: true);
    }
}
