using System.Collections.ObjectModel;
using AspectN.Diagnostics;
using AspectN.Parsing;
using AspectN.Text;

namespace AspectN.Syntax;

/// <summary>Contains the source, recovered syntax root, entry point, and parsing diagnostics.</summary>
public sealed class AspectNSyntaxTree
{
    private readonly IReadOnlyList<AspectNDiagnostic> _diagnostics;

    internal AspectNSyntaxTree(
        SourceText text,
        AspectNParseKind parseKind,
        SyntaxNode root,
        IEnumerable<AspectNDiagnostic> diagnostics, AspectNParseOptions? parseOptions = null,
        CancellationToken cancellationToken = default)
    {
        Text = text;
        ParseKind = parseKind;
        Root = root;
        ParseOptions = parseOptions ?? new AspectNParseOptions();
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = diagnostics.ToList();
        // 平铺二元表达式也能形成深树，绑定前必须独立检查树高。
        if (ParseOptions.MaximumSyntaxDepth.ExceededBy(root.Depth))
            snapshot.Add(new AspectNDiagnostic(DiagnosticCodes.InputDepthExceeded,
                DiagnosticSeverity.Error, $"Parsing exceeded MaximumSyntaxDepth ({ParseOptions.MaximumSyntaxDepth}); requested {root.Depth} levels.", root.FullSpan));
        if (TypeStructureGuard.ExceedsLimit(root, ParseOptions, cancellationToken))
            snapshot.Add(new AspectNDiagnostic(DiagnosticCodes.InputDepthExceeded,
                DiagnosticSeverity.Error, $"Parsing exceeded MaximumTypeDepth ({ParseOptions.MaximumTypeDepth}).", root.FullSpan));
        _diagnostics = new ReadOnlyCollection<AspectNDiagnostic>(DiagnosticSnapshot.Create(snapshot));
    }

    /// <summary>Gets the text.</summary>
    public SourceText Text { get; }

    /// <summary>Gets the immutable limits used to parse and validate this tree.</summary>
    /// <remarks>Compilation retains these limits for binding. Recompiling an existing tree cannot override them.</remarks>
    public AspectNParseOptions ParseOptions { get; }

    /// <summary>Gets the parse kind.</summary>
    public AspectNParseKind ParseKind { get; }

    /// <summary>Gets the root.</summary>
    public SyntaxNode Root { get; }

    /// <summary>Gets immutable diagnostics in deterministic source/code order, with exact duplicates removed.</summary>
    public IReadOnlyList<AspectNDiagnostic> Diagnostics => _diagnostics;
}
