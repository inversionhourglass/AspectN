using AspectN.Text;

namespace AspectN.Syntax;

/// <summary>Represents syntax token.</summary>
public sealed class SyntaxToken : SyntaxNode
{
    /// <summary>Initializes a new instance of the SyntaxToken class.</summary>
    /// <param name="kind">Specifies the kind.</param>
    /// <param name="span">Specifies the span.</param>
    /// <param name="text">Specifies the text.</param>
    /// <param name="value">Specifies the value.</param>
    /// <param name="isMissing">Specifies the is missing.</param>
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

    /// <summary>Gets the is missing.</summary>
    public bool IsMissing { get; }

    /// <summary>Gets the span.</summary>
    public TextSpan Span => FullSpan;

    /// <summary>Creates a missing token of the specified kind.</summary>
    /// <param name="kind">Specifies the kind.</param>
    /// <param name="position">Specifies the position.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static SyntaxToken Missing(SyntaxKind kind, int position)
    {
        return new SyntaxToken(
            kind,
            new TextSpan(position, 0),
            string.Empty,
            isMissing: true);
    }
}
