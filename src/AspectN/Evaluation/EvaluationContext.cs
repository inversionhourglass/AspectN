using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Metadata;
using AspectN.Text;
using AspectN.TypeSystem;

namespace AspectN.Evaluation;

internal sealed class EvaluationContext
{
    private readonly List<AspectNDiagnostic> _diagnostics = [];
    private readonly List<AspectNDiagnostic> _allDiagnostics;
    private readonly BindingEnvironment? _bindings;
    private readonly BindingBudget? _budget;
    private readonly DeclarationBindings _declarations;

    public EvaluationContext(ITypeSystem typeSystem, CancellationToken cancellationToken)
        : this(typeSystem, new AspectNCompilationOptions(), cancellationToken)
    {
    }

    public EvaluationContext(ITypeSystem typeSystem, AspectNCompilationOptions options,
        CancellationToken cancellationToken, TypeSignature? type = null, MethodSignature? method = null)
    {
        TypeSystem = typeSystem ?? throw new ArgumentNullException(nameof(typeSystem));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        CancellationToken = cancellationToken;
        _allDiagnostics = [];
        _declarations = new DeclarationBindings(type, method);
        if (options.GenericVariableMode == GenericVariableMode.Capture)
        {
            _budget = new BindingBudget(options, cancellationToken);
            _bindings = new BindingEnvironment(_budget);
        }
    }

    private EvaluationContext(EvaluationContext parent, BindingEnvironment? bindings)
    {
        TypeSystem = parent.TypeSystem;
        Options = parent.Options;
        CancellationToken = parent.CancellationToken;
        _allDiagnostics = parent._allDiagnostics;
        _declarations = parent._declarations;
        _budget = parent._budget;
        _bindings = bindings;
    }

    public ITypeSystem TypeSystem { get; }
    public CancellationToken CancellationToken { get; }
    public AspectNCompilationOptions Options { get; }
    internal bool HasDynamicBindings => _bindings is not null;
    internal long BindingWorkUsed => _budget?.WorkUsed ?? 0;

    public void Visit(TextSpan span)
    {
        CancellationToken.ThrowIfCancellationRequested();
        _budget?.Visit(span);
    }

    public EvaluationContext Fork() => new(this, _bindings?.Clone());

    public MatchStatus Bind(BoundVariableTypePattern variable, TypeSignature candidate)
    {
        CancellationToken.ThrowIfCancellationRequested();
        return _bindings is null
            ? _declarations.Compare(variable, candidate, this)
            : _bindings.Bind(variable.Variable, candidate, TypeSystem, AddDiagnostic);
    }

    public MatchStatus EvaluateNegation(Func<EvaluationContext, MatchStatus> evaluateOperand)
    {
        if (_bindings is null)
        {
            var operand = Fork();
            var result = MatchLogic.Not(evaluateOperand(operand));
            MergeDiagnosticsFrom(operand);
            return result;
        }

        var alternatives = _bindings.CreateCollector();
        var status = MatchStatus.NotMatched;
        // 否定逐个筛选已有绑定；操作数副本不能把内部绑定带出作用域。
        foreach (var bindings in _bindings.CloneAlternatives())
        {
            CancellationToken.ThrowIfCancellationRequested();
            var branch = new EvaluationContext(this, bindings);
            var operand = branch.Fork();
            operand._bindings!.SetUnknown(false);
            var operandStatus = operand._bindings.GetStatus(evaluateOperand(operand));
            var branchStatus = MatchLogic.Not(operandStatus);
            MergeDiagnosticsFrom(operand);
            status = MatchLogic.Or(status, branchStatus);
            alternatives.Add(bindings, branchStatus);
        }
        alternatives.Apply();
        return status;
    }

    public AlternativeCollector? CreateAlternativeCollector() =>
        _bindings is null ? null : new AlternativeCollector(_bindings.CreateCollector());

    internal sealed class AlternativeCollector(BindingEnvironment.AlternativeCollector collector)
    {
        public void Add(EvaluationContext branch, MatchStatus status) => collector.Add(branch._bindings!, status);
        public void Apply() => collector.Apply();
    }

    public void AddDiagnostic(AspectNDiagnostic diagnostic)
    {
        _diagnostics.Add(diagnostic);
        _allDiagnostics.Add(diagnostic);
    }

    public void AddDiagnostics(IEnumerable<AspectNDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics) AddDiagnostic(diagnostic);
    }

    public void MergeDiagnosticsFrom(EvaluationContext branch) => _diagnostics.AddRange(branch._diagnostics);

    public MatchResult CreateResult(MatchStatus status) => new(_bindings?.GetStatus(status) ?? status, _diagnostics);

    public MatchResult CreateBudgetResult(BindingBudgetExceededException failure)
    {
        CancellationToken.ThrowIfCancellationRequested();
        return new MatchResult(MatchStatus.Unknown, _allDiagnostics.Concat([failure.Diagnostic]));
    }
}
