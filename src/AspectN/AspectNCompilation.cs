using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Syntax;

namespace AspectN;

/// <summary>Represents a parsed and bound AspectN expression.</summary>
public sealed class AspectNCompilation
{
    private readonly ReadOnlyCollection<AspectNDiagnostic> _diagnostics;
    private readonly CompiledMemberPattern? _memberPattern;
    private readonly CompiledTypePattern? _typePattern;

    private AspectNCompilation(
        AspectNSyntaxTree syntaxTree,
        AspectNCompilationOptions options,
        BoundNode? boundRoot,
        IEnumerable<AspectNDiagnostic> diagnostics)
    {
        SyntaxTree = syntaxTree;
        Options = options;
        BoundRoot = boundRoot;
        _memberPattern = boundRoot is BoundExpression expression
            ? new CompiledMemberPattern(
                expression,
                options)
            : null;
        _typePattern = boundRoot is BoundTypePattern typePattern
            ? new CompiledTypePattern(
                typePattern,
                options)
            : null;
        _diagnostics = new ReadOnlyCollection<AspectNDiagnostic>(
            DiagnosticSnapshot.Create(diagnostics));
    }

    /// <summary>Gets the syntax tree.</summary>
    public AspectNSyntaxTree SyntaxTree { get; }

    /// <summary>Gets the options.</summary>
    public AspectNCompilationOptions Options { get; }

    /// <summary>Gets immutable diagnostics in deterministic source/code order, with exact duplicates removed.</summary>
    public IReadOnlyList<AspectNDiagnostic> Diagnostics => _diagnostics;

    internal BoundNode? BoundRoot { get; }

    /// <summary>Attempts to get member pattern.</summary>
    /// <param name="pattern">Specifies the pattern.</param>
    /// <returns>Returns the result of the operation.</returns>
    public bool TryGetMemberPattern([NotNullWhen(true)] out CompiledMemberPattern? pattern)
    {
        pattern = _memberPattern;
        return pattern is not null;
    }

    /// <summary>Attempts to get type pattern.</summary>
    /// <param name="pattern">Specifies the pattern.</param>
    /// <returns>Returns the result of the operation.</returns>
    public bool TryGetTypePattern([NotNullWhen(true)] out CompiledTypePattern? pattern)
    {
        pattern = _typePattern;
        return pattern is not null;
    }

    /// <summary>Creates a compilation from the specified syntax tree and options.</summary>
    /// <param name="syntaxTree">Specifies the syntax tree.</param>
    /// <param name="options">Specifies the options.</param>
    /// <returns>Returns the result of the operation.</returns>
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
        // Error recovery nodes preserve source text but are not valid binding input.
        if (syntaxTree.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            return new AspectNCompilation(
                syntaxTree,
                effectiveOptions,
                null,
                syntaxTree.Diagnostics.ToArray());
        }

        var binder = new Binder(syntaxTree, effectiveOptions);
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
