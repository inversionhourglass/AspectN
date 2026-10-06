using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Evaluation;

internal sealed class EvaluationContext
{
    private readonly List<AspectNDiagnostic> _diagnostics;
    private readonly BindingEnvironment _bindings;

    public EvaluationContext(
        ITypeSystem typeSystem,
        CancellationToken cancellationToken)
        : this(
            typeSystem,
            new AspectNCompilationOptions(),
            cancellationToken)
    {
    }

    public EvaluationContext(
        ITypeSystem typeSystem,
        AspectNCompilationOptions options,
        CancellationToken cancellationToken)
        : this(
            typeSystem,
            new BindingEnvironment(),
            new List<AspectNDiagnostic>(),
            options,
            cancellationToken)
    {
    }

    private EvaluationContext(
        ITypeSystem typeSystem,
        BindingEnvironment bindings,
        List<AspectNDiagnostic> diagnostics,
        AspectNCompilationOptions options,
        CancellationToken cancellationToken)
    {
        TypeSystem = typeSystem ??
            throw new ArgumentNullException(nameof(typeSystem));
        CancellationToken = cancellationToken;
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _bindings = bindings;
        _diagnostics = diagnostics;
    }

    public ITypeSystem TypeSystem { get; }

    public CancellationToken CancellationToken { get; }

    public AspectNCompilationOptions Options { get; }

    public EvaluationContext Fork()
    {
        return new EvaluationContext(
            TypeSystem,
            _bindings.Clone(),
            new List<AspectNDiagnostic>(),
            Options,
            CancellationToken);
    }

    public MatchStatus Bind(
        VariableSymbol variable,
        TypeSignature candidate)
    {
        CancellationToken.ThrowIfCancellationRequested();
        return _bindings.Bind(
            variable,
            candidate,
            TypeSystem,
            _diagnostics);
    }

    public MatchStatus EvaluateNegation(Func<EvaluationContext, MatchStatus> evaluateOperand)
    {
        var survivingBranches = new List<EvaluationContext>();
        var status = MatchStatus.NotMatched;
        // 否定逐个筛选已有绑定；操作数在副本上求值，不能把其内部绑定带出否定作用域。
        foreach (var bindings in _bindings.CloneAlternatives())
        {
            CancellationToken.ThrowIfCancellationRequested();
            var branch = new EvaluationContext(
                TypeSystem,
                bindings,
                new List<AspectNDiagnostic>(),
                Options,
                CancellationToken);
            var operandContext = branch.Fork();
            // 传入分支的未知约束属于外层合取；操作数只判断自身的真假。
            operandContext._bindings.SetUnknown(false);
            var operandStatus = operandContext._bindings.GetStatus(evaluateOperand(operandContext));
            var branchStatus = MatchLogic.Not(operandStatus);
            MergeDiagnosticsFrom(operandContext);
            status = MatchLogic.Or(status, branchStatus);
            if (branchStatus == MatchStatus.Matched)
            {
                survivingBranches.Add(branch);
            }
            else if (branchStatus == MatchStatus.Unknown)
            {
                branch._bindings.SetUnknown(true);
                survivingBranches.Add(branch);
            }
        }

        ReplaceBindingsWithAlternativesFrom(survivingBranches);
        return status;
    }

    public void ReplaceBindingsWithAlternativesFrom(
        IEnumerable<EvaluationContext> branches)
    {
        _bindings.ReplaceWithAlternativesFrom(
            branches.Select(branch => branch._bindings));
    }

    public static void RetainAlternative(
        ICollection<EvaluationContext> alternatives,
        EvaluationContext branch,
        MatchStatus status)
    {
        if (status == MatchStatus.NotMatched) return;
        if (status == MatchStatus.Unknown) branch._bindings.SetUnknown(true);
        alternatives.Add(branch);
    }

    public void AddDiagnostic(AspectNDiagnostic diagnostic)
    {
        _diagnostics.Add(diagnostic);
    }

    public void AddDiagnostics(IEnumerable<AspectNDiagnostic> diagnostics)
    {
        _diagnostics.AddRange(diagnostics);
    }

    public void MergeDiagnosticsFrom(EvaluationContext branch)
    {
        _diagnostics.AddRange(branch._diagnostics);
    }

    public MatchResult CreateResult(MatchStatus status)
    {
        return new MatchResult(_bindings.GetStatus(status), _diagnostics);
    }
}
