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

    public void ReplaceBindingsWithAlternativesFrom(
        IEnumerable<EvaluationContext> branches)
    {
        _bindings.ReplaceWithAlternativesFrom(
            branches.Select(branch => branch._bindings));
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
        return new MatchResult(status, _diagnostics);
    }
}
