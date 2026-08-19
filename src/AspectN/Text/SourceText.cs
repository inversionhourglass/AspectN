namespace AspectN.Text;

/// <summary>Represents source text.</summary>
public sealed class SourceText
{
    private readonly string _text;

    private SourceText(string text)
    {
        _text = text;
    }

    /// <summary>Gets the length.</summary>
    public int Length => _text.Length;

    public char this[int index] => _text[index];

    /// <summary>Creates source text from the specified string.</summary>
    /// <param name="text">Specifies the text.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static SourceText From(string text)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(text);
#else
        if (text is null) throw new ArgumentNullException(nameof(text));
#endif
        return new SourceText(text);
    }

    /// <summary>Returns the text covered by the specified span.</summary>
    /// <param name="span">Specifies the span.</param>
    /// <returns>Returns the result of the operation.</returns>
    public string Substring(TextSpan span)
    {
        return _text.Substring(span.Start, span.Length);
    }

    /// <summary>Returns the source text as a string.</summary>
    /// <returns>Returns the result of the operation.</returns>
    public override string ToString()
    {
        return _text;
    }
}
