using AspectN.Text;

namespace AspectN.Parsing;

internal sealed class ParserDepthGuard(ParseContext context)
{
    private long _depth;

    public Scope Enter(TextSpan span)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        if (context.Options.MaximumParserDepth.ExceededBy(_depth + 1)) throw new ParserDepthException(span);
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
