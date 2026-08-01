using AspectN.Binding;
using AspectN.Diagnostics;
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
        var context = new EvaluationContext(typeSystem, cancellationToken);
        var status = Evaluate(_root, candidate, context);
        return context.CreateResult(status);
    }

    private static MatchStatus Evaluate(
        BoundTypePattern pattern,
        TypeSignature candidate,
        EvaluationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        switch (pattern)
        {
            case BoundWildcardTypePattern:
                return MatchStatus.Matched;
            case BoundVariableTypePattern variable:
                return context.Bind(variable.Variable, candidate);
            case BoundUnaryTypePattern unary:
                {
                    var branch = context.Fork();
                    var status = Evaluate(unary.Operand, candidate, branch);
                    context.MergeDiagnosticsFrom(branch);
                    return MatchLogic.Not(status);
                }
            case BoundBinaryTypePattern binary
                when binary.Operator == BoundTypeLogicalOperator.And:
                {
                    var left = Evaluate(binary.Left, candidate, context);
                    var right = Evaluate(binary.Right, candidate, context);
                    return MatchLogic.And(left, right);
                }
            case BoundBinaryTypePattern binary:
                {
                    var leftContext = context.Fork();
                    var rightContext = context.Fork();
                    var left = Evaluate(binary.Left, candidate, leftContext);
                    var right = Evaluate(binary.Right, candidate, rightContext);
                    context.MergeDiagnosticsFrom(leftContext);
                    context.MergeDiagnosticsFrom(rightContext);
                    return MatchLogic.Or(left, right);
                }
            default:
                context.AddDiagnostic(
                    new AspectNDiagnostic(
                        DiagnosticCodes.MissingBackendCapability,
                        DiagnosticSeverity.Warning,
                        "This type-pattern node is not executable in the current core milestone.",
                        pattern.Span));
                return MatchStatus.Unknown;
        }
    }
}
