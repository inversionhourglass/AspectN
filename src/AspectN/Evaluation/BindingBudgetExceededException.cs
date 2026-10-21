using AspectN.Diagnostics;

namespace AspectN.Evaluation;

internal sealed class BindingBudgetExceededException(AspectNDiagnostic diagnostic) : Exception
{
    public AspectNDiagnostic Diagnostic { get; } = diagnostic;
}
