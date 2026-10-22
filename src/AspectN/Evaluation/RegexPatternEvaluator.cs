using System.Text.RegularExpressions;
using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Metadata;

namespace AspectN.Evaluation;

internal static class RegexPatternEvaluator
{
    public static MatchStatus Evaluate(
        BoundRegexPattern pattern,
        MethodSignature candidate,
        EvaluationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var metadataStatus = MetadataQuery.Require(candidate.Availability,
            MetadataFacts.Accessibility | MetadataFacts.IsStatic | MetadataFacts.Name, pattern, context);
        if (candidate.Accessibility == Accessibility.Unknown && metadataStatus == MatchStatus.Matched)
            metadataStatus = MetadataQuery.Require(new MetadataAvailability(MetadataFacts.Accessibility),
                MetadataFacts.Accessibility, pattern, context);
        metadataStatus = MatchLogic.And(metadataStatus, MetadataQuery.Require(candidate.ReturnType.Availability,
            MetadataFacts.RefKind | MetadataFacts.ByReference, pattern, context));
        foreach (var parameter in candidate.Parameters)
            metadataStatus = MatchLogic.And(metadataStatus, MetadataQuery.Require(parameter.Type.Availability,
                MetadataFacts.RefKind | MetadataFacts.ByReference, pattern, context));
        if (metadataStatus == MatchStatus.Unknown) return metadataStatus;
        try
        {
            var signature = CanonicalSignatureFormatter.Format(candidate, context.QueryContext);
            return Regex.IsMatch(
                signature,
                pattern.Pattern,
                RegexOptions.CultureInvariant,
                context.Options.MatchOptions.RegexTimeout.RegexTimeout)
                ? MatchStatus.Matched
                : MatchStatus.NotMatched;
        }
        catch (CanonicalSignatureUnavailableException exception)
        {
            context.AddDiagnostic(new AspectNDiagnostic(DiagnosticCodes.MissingBackendCapability,
                DiagnosticSeverity.Warning, exception.Message, pattern.Span));
            return MatchStatus.Unknown;
        }
        catch (RegexMatchTimeoutException)
        {
            context.AddDiagnostic(
                new AspectNDiagnostic(
                    DiagnosticCodes.RegexTimeout,
                    DiagnosticSeverity.Warning,
                    "Regular expression evaluation exceeded its configured timeout.",
                    pattern.Span));
            return MatchStatus.Unknown;
        }
        catch (ArgumentException exception)
        {
            context.AddDiagnostic(
                new AspectNDiagnostic(
                    DiagnosticCodes.InvalidRegex,
                    DiagnosticSeverity.Warning,
                    $"Regular expression is invalid: {exception.Message}",
                    pattern.Span));
            return MatchStatus.Unknown;
        }
    }
}
