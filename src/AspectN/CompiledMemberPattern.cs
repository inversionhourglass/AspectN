using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN;

public sealed class CompiledMemberPattern : CompiledPattern
{
    private readonly BoundExpression _root;

    internal CompiledMemberPattern(
        BoundExpression root,
        AspectNLanguageVersion languageVersion,
        AspectNCompilationOptions options)
        : base(languageVersion, options)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
    }

    public MatchResult Match(
        CallableSignature candidate,
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
        return MatchResult.Unknown(
            new AspectNDiagnostic(
                DiagnosticCodes.MissingBackendCapability,
                DiagnosticSeverity.Warning,
                "Member-pattern evaluation is introduced by the selector evaluation milestone.",
                _root.Span));
    }
}
