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
        IEnumerable<AspectNDiagnostic> diagnostics)
    {
        Text = text;
        ParseKind = parseKind;
        Root = root;
        var snapshot = diagnostics.ToList();
        // 平铺二元表达式也能形成深树，绑定前必须独立检查树高。
        if (root.Depth > ParserDepthGuard.MAXIMUM_SYNTAX_DEPTH)
            snapshot.Add(new AspectNDiagnostic(DiagnosticCodes.InputDepthExceeded,
                DiagnosticSeverity.Error, "The syntax tree exceeds the supported depth of 256 nodes.", root.FullSpan));
        _diagnostics = new ReadOnlyCollection<AspectNDiagnostic>(DiagnosticSnapshot.Create(snapshot));
    }

    /// <summary>Gets the text.</summary>
    public SourceText Text { get; }

    /// <summary>Gets the parse kind.</summary>
    public AspectNParseKind ParseKind { get; }

    /// <summary>Gets the root.</summary>
    public SyntaxNode Root { get; }

    /// <summary>Gets immutable diagnostics in deterministic source/code order, with exact duplicates removed.</summary>
    public IReadOnlyList<AspectNDiagnostic> Diagnostics => _diagnostics;
}
