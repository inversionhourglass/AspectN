using AspectN.Binding;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN;

/// <summary>Evaluates a bound member expression against a backend-neutral method signature.</summary>
public sealed class CompiledMemberPattern : CompiledPattern
{
    private readonly BoundExpression _root;

    internal CompiledMemberPattern(
        BoundExpression root,
        AspectNCompilationOptions options)
        : base(options)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
    }

    /// <summary>Evaluates the candidate using the supplied backend and cancellation token.</summary>
    /// <param name="candidate">Backend-neutral candidate signature.</param>
    /// <param name="typeSystem">Backend providing metadata queries and declared type relationships.</param>
    /// <param name="cancellationToken">Specifies the cancellation token.</param>
    /// <returns>Matched, NotMatched, or Unknown with diagnostics when required facts are unavailable.</returns>
    public MatchResult Match(
        MethodSignature candidate,
        ITypeSystem typeSystem,
        CancellationToken cancellationToken = default)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(typeSystem);
#else
        if (candidate is null)
            throw new ArgumentNullException(nameof(candidate));
        if (typeSystem is null)
            throw new ArgumentNullException(nameof(typeSystem));
#endif

        cancellationToken.ThrowIfCancellationRequested();
        var failure = MetadataLimitGuard.GetFailure(candidate, cancellationToken);
        if (failure is not null) return MatchResult.Unknown(failure);
        var context = new EvaluationContext(
            typeSystem,
            Options,
            cancellationToken);
        var status = MemberPatternEvaluator.Evaluate(
            _root,
            candidate,
            context);
        return context.CreateResult(status);
    }
}
