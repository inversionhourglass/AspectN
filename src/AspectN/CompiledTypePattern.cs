using AspectN.Binding;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN;

public sealed class CompiledTypePattern : CompiledPattern
{
    private readonly BoundTypePattern _root;

    internal CompiledTypePattern(
        BoundTypePattern root,
        AspectNLanguageVersion languageVersion,
        AspectNCompilationOptions options)
        : base(languageVersion, options)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
    }

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
            LanguageVersion,
            Options,
            cancellationToken);
        var status = TypePatternEvaluator.Evaluate(_root, candidate, context);
        return context.CreateResult(status);
    }

}
