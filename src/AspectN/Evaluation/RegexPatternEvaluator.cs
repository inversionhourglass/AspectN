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
        var signature = CanonicalSignatureFormatter.Format(
            candidate,
            context.LanguageVersion);
        try
        {
            return Regex.IsMatch(
                signature,
                pattern.Pattern,
                RegexOptions.CultureInvariant,
                context.Options.RegexTimeout)
                ? MatchStatus.Matched
                : MatchStatus.NotMatched;
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
