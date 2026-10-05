using AspectN.Text;

namespace AspectN.Parsing;

internal sealed class ParserDepthGuard
{
    internal const int MAXIMUM_RECURSION = 128;
    internal const int MAXIMUM_SYNTAX_DEPTH = 256;
    private int _depth;

    public Scope Enter(TextSpan span)
    {
        if (_depth >= MAXIMUM_RECURSION) throw new ParserDepthException(span);
        _depth++;
        return new Scope(this);
    }

    internal readonly struct Scope(ParserDepthGuard guard) : IDisposable
    {
        public void Dispose() => guard._depth--;
    }
}

internal sealed class ParserDepthException(TextSpan span) : Exception
{
    public TextSpan Span { get; } = span;
}
