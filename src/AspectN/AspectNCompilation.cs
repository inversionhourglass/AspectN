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

    /// <summary>Gets the parsed source tree, including its parse kind and recovery diagnostics.</summary>
    public AspectNSyntaxTree SyntaxTree { get; }

    /// <summary>Gets the binding and matching settings used to create this compilation's pattern.</summary>
    public AspectNCompilationOptions Options { get; }

    /// <summary>Gets immutable diagnostics in deterministic source/code order, with exact duplicates removed.</summary>
    public IReadOnlyList<AspectNDiagnostic> Diagnostics => _diagnostics;

    internal BoundNode? BoundRoot { get; }

    /// <summary>Attempts to retrieve the compiled member expression.</summary>
    /// <param name="pattern">Receives the member pattern on success, or null when compilation failed or the input was a type pattern.</param>
    /// <returns>True when a member pattern is available; otherwise false. Inspect Diagnostics for compilation failures.</returns>
    public bool TryGetMemberPattern([NotNullWhen(true)] out CompiledMemberPattern? pattern)
    {
        pattern = _memberPattern;
        return pattern is not null;
    }

    /// <summary>Attempts to retrieve the compiled standalone type pattern.</summary>
    /// <param name="pattern">Receives the type pattern on success, or null when compilation failed or the input was a member expression.</param>
    /// <returns>True when a type pattern is available; otherwise false. Inspect Diagnostics for compilation failures.</returns>
    public bool TryGetTypePattern([NotNullWhen(true)] out CompiledTypePattern? pattern)
    {
        pattern = _typePattern;
        return pattern is not null;
    }

    /// <summary>Creates a compilation from the specified syntax tree and options.</summary>
    /// <param name="syntaxTree">The parsed member expression or type pattern. Existing parse errors prevent pattern creation.</param>
    /// <param name="options">Binding and matching settings retained by the compilation; null uses defaults.</param>
    /// <param name="cancellationToken">Cancels binding; parsing limits are retained by the supplied tree.</param>
    /// <returns>A compilation containing parse and binding diagnostics and a pattern only when no errors were reported.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="syntaxTree"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public static AspectNCompilation Create(
        AspectNSyntaxTree syntaxTree,
        AspectNCompilationOptions? options = null, CancellationToken cancellationToken = default)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(syntaxTree);
#else
        if (syntaxTree is null)
            throw new ArgumentNullException(nameof(syntaxTree));
#endif

        cancellationToken.ThrowIfCancellationRequested();
        var effectiveOptions = options ?? new AspectNCompilationOptions();
        // 错误恢复节点保留源码，但不能作为绑定输入。
        if (syntaxTree.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            return new AspectNCompilation(
                syntaxTree,
                effectiveOptions,
                null,
                syntaxTree.Diagnostics.ToArray());
        }

        var binder = new Binder(syntaxTree, effectiveOptions, cancellationToken);
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
