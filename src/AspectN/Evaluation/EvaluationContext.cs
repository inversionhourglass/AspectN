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
        AspectNLanguageVersion languageVersion,
        CancellationToken cancellationToken)
        : this(
            typeSystem,
            languageVersion,
            new AspectNCompilationOptions(),
            cancellationToken)
    {
    }

    public EvaluationContext(
        ITypeSystem typeSystem,
        AspectNLanguageVersion languageVersion,
        AspectNCompilationOptions options,
        CancellationToken cancellationToken)
        : this(
            typeSystem,
            new BindingEnvironment(),
            new List<AspectNDiagnostic>(),
            languageVersion,
            options,
            cancellationToken)
    {
    }

    private EvaluationContext(
        ITypeSystem typeSystem,
        BindingEnvironment bindings,
        List<AspectNDiagnostic> diagnostics,
        AspectNLanguageVersion languageVersion,
        AspectNCompilationOptions options,
        CancellationToken cancellationToken)
    {
        TypeSystem = typeSystem ??
            throw new ArgumentNullException(nameof(typeSystem));
        CancellationToken = cancellationToken;
        LanguageVersion = languageVersion;
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _bindings = bindings;
        _diagnostics = diagnostics;
    }

    public ITypeSystem TypeSystem { get; }

    public CancellationToken CancellationToken { get; }

    public AspectNLanguageVersion LanguageVersion { get; }

    public AspectNCompilationOptions Options { get; }

    public EvaluationContext Fork()
    {
        return new EvaluationContext(
            TypeSystem,
            _bindings.Clone(),
            new List<AspectNDiagnostic>(),
            LanguageVersion,
            Options,
            CancellationToken);
    }

    public MatchStatus Bind(
        VariableSymbol variable,
        TypeSignature candidate)
    {
        CancellationToken.ThrowIfCancellationRequested();
        if (_bindings.TryGet(variable, out var existing))
        {
            var result = TypeSystem.AreEqual(existing!, candidate);
            _diagnostics.AddRange(result.Diagnostics);
            return result.Status;
        }

        _bindings.Add(variable, candidate);
        return MatchStatus.Matched;
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
