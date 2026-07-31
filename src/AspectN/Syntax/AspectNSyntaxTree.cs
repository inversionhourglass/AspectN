using System.Collections.ObjectModel;
using AspectN.Diagnostics;
using AspectN.Parsing;
using AspectN.Text;

namespace AspectN.Syntax;

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

    public SourceText Text { get; }

    public AspectNParseKind ParseKind { get; }

    public SyntaxNode Root { get; }

    public AspectNLanguageVersion LanguageVersion { get; }

    public IReadOnlyList<AspectNDiagnostic> Diagnostics => _diagnostics;
}
