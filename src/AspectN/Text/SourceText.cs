namespace AspectN.Text;

/// <summary>Provides an immutable view of the original UTF-16 source string.</summary>
public sealed class SourceText
{
    private readonly string _text;

    private SourceText(string text)
    {
        _text = text;
    }

    /// <summary>Gets the number of UTF-16 code units in the source.</summary>
    public int Length => _text.Length;

    /// <summary>Gets the UTF-16 code unit at a zero-based source index.</summary>
    /// <param name="index">An index from zero through Length minus one.</param>
    /// <value>The code unit at the requested index.</value>
    /// <exception cref="IndexOutOfRangeException">The index is outside the source.</exception>
    public char this[int index] => _text[index];

    /// <summary>Creates source text from the specified string.</summary>
    /// <param name="text">The source string to retain without normalization or trimming.</param>
    /// <returns>A source view containing the supplied string unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
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
    /// <param name="span">The zero-based source range, measured in UTF-16 code units.</param>
    /// <returns>The characters in the span, or an empty string for a valid zero-length span.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The span extends outside the source.</exception>
    public string Substring(TextSpan span)
    {
        return _text.Substring(span.Start, span.Length);
    }

    /// <summary>Returns the source text as a string.</summary>
    /// <returns>The complete original source string.</returns>
    public override string ToString()
    {
        return _text;
    }
}
