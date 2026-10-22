using AspectN.Parsing;
using AspectN.Syntax;
using AspectN.Text;

namespace AspectN;

/// <summary>Provides entry points for parsing AspectN expressions and type patterns.</summary>
public static class AspectNParser
{
    /// <summary>Parses a member expression without binding names or creating a matching pattern.</summary>
    /// <param name="source">The complete member expression source, including any selector keywords.</param>
    /// <param name="parseOptions">Immutable parse limits; null uses defaults, including 256 UTF-16 source code units.</param>
    /// <param name="cancellationToken">Cancels parsing and syntax validation.</param>
    /// <returns>A member-expression syntax tree retaining the source and any syntax or resource-limit diagnostics. A returned tree does not imply valid input.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public static AspectNSyntaxTree ParseMemberExpression(
        string source, AspectNParseOptions? parseOptions = null, CancellationToken cancellationToken = default)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(source);
#else
        if (source is null) throw new ArgumentNullException(nameof(source));
#endif

        cancellationToken.ThrowIfCancellationRequested();
        var options = parseOptions ?? new AspectNParseOptions();
        if (options.MaximumSourceLength.ExceededBy(source.Length)) return SourceLimitFailure(source, options, AspectNParseKind.MemberExpression);
        return new Parser(SourceText.From(source), options, cancellationToken).ParseMemberExpression();
    }

    /// <summary>Parses a standalone type pattern without binding names or creating a matching pattern.</summary>
    /// <param name="source">The complete type pattern source, without a member selector wrapper.</param>
    /// <param name="parseOptions">Immutable parse limits; null uses defaults, including 256 UTF-16 source code units.</param>
    /// <param name="cancellationToken">Cancels parsing and syntax validation.</param>
    /// <returns>A type-pattern syntax tree retaining the source and any syntax or resource-limit diagnostics. A returned tree does not imply valid input.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public static AspectNSyntaxTree ParseTypePattern(
        string source, AspectNParseOptions? parseOptions = null, CancellationToken cancellationToken = default)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(source);
#else
        if (source is null) throw new ArgumentNullException(nameof(source));
#endif

        cancellationToken.ThrowIfCancellationRequested();
        var options = parseOptions ?? new AspectNParseOptions();
        if (options.MaximumSourceLength.ExceededBy(source.Length)) return SourceLimitFailure(source, options, AspectNParseKind.TypePattern);
        return new TypePatternParser(SourceText.From(source), options, cancellationToken).Parse();
    }

    private static AspectNSyntaxTree SourceLimitFailure(string source, AspectNParseOptions options, AspectNParseKind kind)
    {
        var diagnostic = new Diagnostics.AspectNDiagnostic(Diagnostics.DiagnosticCodes.ParseResourceLimitExceeded,
            Diagnostics.DiagnosticSeverity.Error,
            $"Parsing exceeded MaximumSourceLength ({options.MaximumSourceLength}); requested {source.Length} UTF-16 code units.", new TextSpan(0, 0));
        SyntaxNode root = kind == AspectNParseKind.TypePattern
            ? new WildcardTypePatternSyntax(SyntaxToken.Missing(SyntaxKind.StarToken, 0))
            : new InvalidSelectorSyntax(Array.Empty<SyntaxNode>(),
                SyntaxToken.Missing(SyntaxKind.OpenParenToken, 0), SyntaxToken.Missing(SyntaxKind.CloseParenToken, 0));
        return new AspectNSyntaxTree(SourceText.From(source), kind, root, new[] { diagnostic }, options);
    }
}
