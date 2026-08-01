using System.Collections.ObjectModel;
using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Syntax;

namespace AspectN;

public sealed class AspectNCompilation
{
    private readonly ReadOnlyCollection<AspectNDiagnostic> _diagnostics;

    private AspectNCompilation(
        AspectNSyntaxTree syntaxTree,
        AspectNCompilationOptions options,
        BoundNode? boundRoot,
        IEnumerable<AspectNDiagnostic> diagnostics)
    {
        SyntaxTree = syntaxTree;
        Options = options;
        BoundRoot = boundRoot;
        _diagnostics = new ReadOnlyCollection<AspectNDiagnostic>(
            diagnostics.ToArray());
    }

    public AspectNSyntaxTree SyntaxTree { get; }

    public AspectNCompilationOptions Options { get; }

    public AspectNLanguageVersion LanguageVersion => SyntaxTree.LanguageVersion;

    public IReadOnlyList<AspectNDiagnostic> Diagnostics => _diagnostics;

    internal BoundNode? BoundRoot { get; }

    public static AspectNCompilation Create(
        AspectNSyntaxTree syntaxTree,
        AspectNCompilationOptions? options = null)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(syntaxTree);
#else
        if (syntaxTree is null)
            throw new ArgumentNullException(nameof(syntaxTree));
#endif

        var effectiveOptions = options ?? new AspectNCompilationOptions();
        var binder = new Binder(syntaxTree);
        var boundRoot = binder.Bind();
        var diagnostics = syntaxTree.Diagnostics
            .Concat(binder.Diagnostics)
            .ToArray();
        if (diagnostics.Any(
                diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            boundRoot = null;
        }

        return new AspectNCompilation(
            syntaxTree,
            effectiveOptions,
            boundRoot,
            diagnostics);
    }
}
