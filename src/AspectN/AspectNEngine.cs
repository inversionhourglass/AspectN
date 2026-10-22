using AspectN.Caching;
using AspectN.Parsing;

namespace AspectN;

/// <summary>Provides the high-level API for compiling AspectN expressions and type patterns.</summary>
public sealed class AspectNEngine
{
    private readonly CompiledPatternCache _cache;

    /// <summary>Initializes a new instance of the AspectNEngine class.</summary>
    /// <param name="options">Engine-wide cache settings; null creates an engine with default settings.</param>
    public AspectNEngine(
        AspectNEngineOptions? options = null)
    {
        Options = options ?? new AspectNEngineOptions();
        _cache = new CompiledPatternCache(Options.CacheCapacity);
    }

    /// <summary>Gets the engine-wide settings, including this instance's cache capacity.</summary>
    public AspectNEngineOptions Options { get; }

    internal int CacheCount => _cache.Count;

    /// <summary>Parses and binds a member expression, retaining diagnostics if compilation fails.</summary>
    /// <param name="source">The complete member expression, such as <c>method(public string *(..))</c>.</param>
    /// <param name="parseOptions">Immutable source, syntax, and binding limits; null uses defaults.</param>
    /// <param name="compilationOptions">Binding and matching settings retained by the compiled pattern; null uses defaults.</param>
    /// <param name="cancellationToken">Cancels this caller's compilation, including cache hits; other callers remain independent.</param>
    /// <returns>A compilation containing diagnostics and, on success, a member pattern accessible through <see cref="AspectNCompilation.TryGetMemberPattern"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public AspectNCompilation CompileMemberExpression(
        string source,
        AspectNParseOptions? parseOptions = null,
        AspectNCompilationOptions? compilationOptions = null,
        CancellationToken cancellationToken = default)
    {
        return Compile(
            source,
            AspectNParseKind.MemberExpression,
            parseOptions, compilationOptions, cancellationToken);
    }

    /// <summary>Parses and binds a standalone type pattern, retaining diagnostics if compilation fails.</summary>
    /// <param name="source">The complete type pattern, such as <c>System.String</c>.</param>
    /// <param name="parseOptions">Immutable source, syntax, and binding limits; null uses defaults.</param>
    /// <param name="compilationOptions">Binding and matching settings retained by the compiled pattern; null uses defaults.</param>
    /// <param name="cancellationToken">Cancels this caller's compilation, including cache hits; other callers remain independent.</param>
    /// <returns>A compilation containing diagnostics and, on success, a type pattern accessible through <see cref="AspectNCompilation.TryGetTypePattern"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public AspectNCompilation CompileTypePattern(
        string source,
        AspectNParseOptions? parseOptions = null,
        AspectNCompilationOptions? compilationOptions = null,
        CancellationToken cancellationToken = default)
    {
        return Compile(
            source,
            AspectNParseKind.TypePattern,
            parseOptions, compilationOptions, cancellationToken);
    }

    private AspectNCompilation Compile(
        string source,
        AspectNParseKind parseKind,
        AspectNParseOptions? parseOptions, AspectNCompilationOptions? compilationOptions, CancellationToken cancellationToken)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(source);
#else
        if (source is null) throw new ArgumentNullException(nameof(source));
#endif

        cancellationToken.ThrowIfCancellationRequested();
        var effectiveParseOptions = parseOptions ?? new AspectNParseOptions();
        var effectiveCompilationOptions = compilationOptions ??
            new AspectNCompilationOptions();
        // 源码边界先于缓存键散列；失败结果仍经正常解析/编译诊断通道返回。
        if (effectiveParseOptions.MaximumSourceLength.ExceededBy(source.Length))
            return CompileUncached(source, parseKind, effectiveParseOptions, effectiveCompilationOptions, cancellationToken);
        var key = new CompiledPatternCacheKey(
            source,
            parseKind,
            effectiveParseOptions,
            effectiveCompilationOptions);
        return _cache.GetOrAdd(
            key,
            () => CompileUncached(
                source,
                parseKind,
                effectiveParseOptions, effectiveCompilationOptions, cancellationToken), cancellationToken);
    }

    private static AspectNCompilation CompileUncached(
        string source,
        AspectNParseKind parseKind,
        AspectNParseOptions parseOptions, AspectNCompilationOptions compilationOptions, CancellationToken cancellationToken)
    {
        var syntaxTree = parseKind == AspectNParseKind.MemberExpression
            ? AspectNParser.ParseMemberExpression(source, parseOptions, cancellationToken)
            : AspectNParser.ParseTypePattern(source, parseOptions, cancellationToken);
        return AspectNCompilation.Create(
            syntaxTree,
            compilationOptions, cancellationToken);
    }
}
