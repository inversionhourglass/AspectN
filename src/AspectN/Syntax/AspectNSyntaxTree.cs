using System.Collections.ObjectModel;
using AspectN.Diagnostics;
using AspectN.Parsing;
using AspectN.Text;

namespace AspectN.Syntax;

/// <summary>Represents aspect nsyntax tree.</summary>
public sealed class AspectNSyntaxTree
{
    private readonly IReadOnlyList<AspectNDiagnostic> _diagnostics;

    internal AspectNSyntaxTree(
        SourceText text,
        AspectNParseKind parseKind,
        SyntaxNode root,
        IEnumerable<AspectNDiagnostic> diagnostics,
        AspectNLanguageVersion languageVersion = AspectNLanguageVersion.Standard)
    {
        Text = text;
        ParseKind = parseKind;
        Root = root;
        LanguageVersion = languageVersion;
        _diagnostics = new ReadOnlyCollection<AspectNDiagnostic>(
            diagnostics.ToArray());
    }

    /// <summary>Gets the text.</summary>
    public SourceText Text { get; }

    /// <summary>Gets the parse kind.</summary>
    public AspectNParseKind ParseKind { get; }

    /// <summary>Gets the root.</summary>
    public SyntaxNode Root { get; }

    /// <summary>Gets the language version.</summary>
    public AspectNLanguageVersion LanguageVersion { get; }

    /// <summary>Gets the diagnostics.</summary>
    public IReadOnlyList<AspectNDiagnostic> Diagnostics => _diagnostics;
}
