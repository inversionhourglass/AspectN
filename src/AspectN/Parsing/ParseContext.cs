using AspectN.Diagnostics;
using AspectN.Execution;
using AspectN.Syntax;
using AspectN.Text;

namespace AspectN.Parsing;

// 每次解析独立；所有前瞻词法器及临时节点共享额度。恢复保留只容纳固定大小的根。
internal sealed class ParseContext(AspectNParseOptions options, CancellationToken cancellationToken)
{
    private long _tokens;
    private long _nodes;

    public AspectNParseOptions Options { get; } = options;
    public CancellationToken CancellationToken { get; } = cancellationToken;
    public bool IsStopped { get; private set; }

    public void Stop() => IsStopped = true;

    public void EnsureNodeAvailable(TextSpan span)
    {
        CancellationToken.ThrowIfCancellationRequested();
        if (!IsStopped && !Options.MaximumSyntaxNodes.IsUnlimited && _nodes >= Options.MaximumSyntaxNodes.Value)
            throw new ParseLimitException(new AspectNDiagnostic(DiagnosticCodes.ParseResourceLimitExceeded, DiagnosticSeverity.Error,
                $"Parsing exceeded MaximumSyntaxNodes ({Options.MaximumSyntaxNodes}); used {_nodes}, requested 1 additional node.", span));
    }

    public void Token(TextSpan span)
    {
        CancellationToken.ThrowIfCancellationRequested();
        Consume(Options.MaximumTokens, ref _tokens, nameof(Options.MaximumTokens), span);
    }

    public T Create<T>(Func<T> factory, TextSpan span = default) where T : SyntaxNode
    {
        CancellationToken.ThrowIfCancellationRequested();
        if (!IsStopped) Consume(Options.MaximumSyntaxNodes, ref _nodes, nameof(Options.MaximumSyntaxNodes), span);
        return factory();
    }

    public async StackWork<T> CreateWork<T>(Func<StackWork<T>> factory) where T : SyntaxNode
    {
        CancellationToken.ThrowIfCancellationRequested();
        if (!IsStopped) Consume(Options.MaximumSyntaxNodes, ref _nodes, nameof(Options.MaximumSyntaxNodes), default);
        return await factory();
    }

    private static void Consume(ResourceLimit limit, ref long used, string name, TextSpan span)
    {
        if (limit.IsUnlimited) return;
        if (used >= limit.Value) throw new ParseLimitException(new AspectNDiagnostic(
            DiagnosticCodes.ParseResourceLimitExceeded, DiagnosticSeverity.Error,
            $"Parsing exceeded {name} ({limit}); used {used}, requested 1 additional unit.", span));
        used++;
    }
}

internal sealed class ParseLimitException(AspectNDiagnostic diagnostic) : Exception
{
    public AspectNDiagnostic Diagnostic { get; } = diagnostic;
}
