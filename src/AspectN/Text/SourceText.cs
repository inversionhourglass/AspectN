namespace AspectN.Text;

public sealed class SourceText
{
    private readonly string _text;

    private SourceText(string text)
    {
        _text = text;
    }

    public int Length => _text.Length;

    public char this[int index] => _text[index];

    public static SourceText From(string text)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(text);
#else
        if (text is null) throw new ArgumentNullException(nameof(text));
#endif
        return new SourceText(text);
    }

    public string Substring(TextSpan span)
    {
        return _text.Substring(span.Start, span.Length);
    }

    public override string ToString()
    {
        return _text;
    }
}
