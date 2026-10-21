using AspectN.Diagnostics;
using AspectN.Text;

namespace AspectN.Evaluation;

// 一次 Match 的所有捕获分支共享额度；先检查剩余额度，再执行受控处理。
internal sealed class BindingBudget(AspectNCompilationOptions options, CancellationToken cancellationToken)
{
    private long _remainingWork = options.MaximumBindingWork;
    private TextSpan? _span;

    public long WorkUsed => options.MaximumBindingWork - _remainingWork;

    public void Visit(TextSpan span)
    {
        _span = span;
        Spend(1);
    }

    public void Spend(long work)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (work > _remainingWork) Exceeded(nameof(options.MaximumBindingWork), options.MaximumBindingWork);
        _remainingWork -= work;
    }

    public void CheckAlternativeCount(int currentCount)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (currentCount >= options.MaximumBindingAlternatives)
            Exceeded(nameof(options.MaximumBindingAlternatives), options.MaximumBindingAlternatives);
    }

    private void Exceeded(string limit, long value)
    {
        throw new BindingBudgetExceededException(new AspectNDiagnostic(
            DiagnosticCodes.BindingBudgetExceeded,
            DiagnosticSeverity.Warning,
            $"Capture matching exceeded {limit} ({value}).",
            _span));
    }
}
