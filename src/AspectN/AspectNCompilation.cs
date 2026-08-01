using System.Collections.ObjectModel;
using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Extensibility;
using AspectN.Syntax;

namespace AspectN;

public sealed class AspectNCompilation
{
    private readonly ReadOnlyCollection<AspectNDiagnostic> _diagnostics;
    private readonly CompiledMemberPattern? _memberPattern;
    private readonly CompiledTypePattern? _typePattern;

    private AspectNCompilation(
        AspectNSyntaxTree syntaxTree,
        AspectNCompilationOptions options,
        AspectNExtensionRegistry extensions,
        BoundNode? boundRoot,
        IEnumerable<AspectNDiagnostic> diagnostics)
    {
        SyntaxTree = syntaxTree;
        Options = options;
        Extensions = extensions;
        BoundRoot = boundRoot;
        _memberPattern = boundRoot is BoundExpression expression
            ? new CompiledMemberPattern(
                expression,
                syntaxTree.LanguageVersion,
                options)
            : null;
        _typePattern = boundRoot is BoundTypePattern typePattern
            ? new CompiledTypePattern(
                typePattern,
                syntaxTree.LanguageVersion,
                options)
            : null;
        _diagnostics = new ReadOnlyCollection<AspectNDiagnostic>(
            diagnostics.ToArray());
    }

    public AspectNSyntaxTree SyntaxTree { get; }

    public AspectNCompilationOptions Options { get; }

    public AspectNExtensionRegistry Extensions { get; }

    public AspectNLanguageVersion LanguageVersion => SyntaxTree.LanguageVersion;

    public IReadOnlyList<AspectNDiagnostic> Diagnostics => _diagnostics;

    internal BoundNode? BoundRoot { get; }

    public bool TryGetMemberPattern(out CompiledMemberPattern? pattern)
    {
        pattern = _memberPattern;
        return pattern is not null;
    }

    public bool TryGetTypePattern(out CompiledTypePattern? pattern)
    {
        pattern = _typePattern;
        return pattern is not null;
    }

    public static AspectNCompilation Create(
        AspectNSyntaxTree syntaxTree,
        AspectNCompilationOptions? options = null,
        AspectNExtensionRegistry? extensions = null)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(syntaxTree);
#else
        if (syntaxTree is null)
            throw new ArgumentNullException(nameof(syntaxTree));
#endif

        var effectiveOptions = options ?? new AspectNCompilationOptions();
        var effectiveExtensions = extensions ?? AspectNExtensionRegistry.Empty;
        var binder = new Binder(syntaxTree, effectiveExtensions);
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
            effectiveExtensions,
            boundRoot,
            diagnostics);
    }
}
