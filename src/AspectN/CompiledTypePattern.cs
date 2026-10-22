using AspectN.Binding;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN;

/// <summary>Evaluates a bound type pattern against a backend-neutral type signature.</summary>
public sealed class CompiledTypePattern : CompiledPattern
{
    private readonly BoundTypePattern _root;

    internal CompiledTypePattern(
        BoundTypePattern root,
        AspectNCompilationOptions options)
        : base(options)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
    }

    /// <summary>Evaluates the candidate using the supplied backend and cancellation token.</summary>
    /// <param name="candidate">Backend-neutral candidate signature.</param>
    /// <param name="typeSystem">Backend providing metadata queries and declared type relationships.</param>
    /// <param name="cancellationToken">Cancels this evaluation independently of other uses of the compiled pattern.</param>
    /// <returns>Matched, NotMatched, or Unknown with diagnostics when required facts are unavailable or a structural, work, relation, attribute, text, or Capture limit prevents a decision.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="candidate"/> or <paramref name="typeSystem"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    /// <remarks>Uses Options.MatchOptions fixed at compilation, with a fresh budget for this call. All branches and built-in queries share its quotas. AN3007/AN3008 abort the whole operation; a successful OR branch cannot mask exhaustion. Structural AN3005/AN3006 and regex timeout AN3003 retain local three-state behavior. Mapping limits remain attached to the backend.</remarks>
    public MatchResult Match(
        TypeSignature candidate,
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
        var context = new EvaluationContext(
                    typeSystem,
                    Options,
                    cancellationToken,
                    type: candidate);
        try
        {
            var failure = MetadataLimitGuard.GetFailure(candidate, context.QueryContext);
            if (failure is not null) return MatchResult.Unknown(failure);
            var status = TypePatternEvaluator.Evaluate(_root, candidate, context);
            return context.CreateResult(status);
        }
        catch (AspectNLimitException exhausted)
        {
            return context.CreateResourceResult(exhausted);
        }
        catch (BindingBudgetExceededException exhausted)
        {
            return context.CreateBudgetResult(exhausted);
        }
    }

}
