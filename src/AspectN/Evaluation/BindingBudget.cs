using AspectN.Diagnostics;
using AspectN.Text;
using AspectN.TypeSystem;

namespace AspectN.Evaluation;

// 一次 Match 的所有捕获分支共享额度；先检查剩余额度，再执行受控处理。
internal sealed class BindingBudget(AspectNCompilationOptions options, CancellationToken cancellationToken,
    TypeSystemQueryContext? queryContext = null)
{
    private readonly AspectNMatchOptions _options = options.MatchOptions;
    private readonly TypeSystemQueryContext _queryContext = queryContext ?? new TypeSystemQueryContext(options.MatchOptions, cancellationToken);
    private TextSpan? _span;

    public long WorkUsed => _queryContext.BindingWorkUsed;

    public void Visit(TextSpan span)
    {
        _span = span;
        Spend(1);
    }

    public void Spend(long work)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_queryContext.TryConsumeBindingWork(work)) return;
        if (_queryContext.LimitDiagnostic!.Code == DiagnosticCodes.BindingBudgetExceeded)
            throw new BindingBudgetExceededException(_queryContext.LimitDiagnostic);
        _queryContext.ThrowIfExhausted();
    }

    public void CheckAlternativeCount(int currentCount)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_options.MaximumBindingAlternatives.ExceededBy((long)currentCount + 1))
            Exceeded(nameof(_options.MaximumBindingAlternatives), _options.MaximumBindingAlternatives);
    }

    private void Exceeded(string limit, ResourceLimit value)
    {
        var diagnostic = new AspectNDiagnostic(
            DiagnosticCodes.BindingBudgetExceeded,
            DiagnosticSeverity.Warning,
            $"Capture matching exceeded {limit} ({value}).",
            _span);
        _queryContext.ReportResourceLimit(diagnostic);
        throw new BindingBudgetExceededException(diagnostic);
    }
}
