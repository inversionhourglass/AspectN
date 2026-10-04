using AspectN.Caching;
using AspectN.Parsing;

namespace AspectN;

/// <summary>Provides the high-level API for compiling AspectN expressions and type patterns.</summary>
public sealed class AspectNEngine
{
    private readonly CompiledPatternCache _cache;

    /// <summary>Initializes a new instance of the AspectNEngine class.</summary>
    /// <param name="options">Specifies the options.</param>
    public AspectNEngine(
        AspectNEngineOptions? options = null)
    {
        Options = options ?? new AspectNEngineOptions();
        _cache = new CompiledPatternCache(Options.CacheCapacity);
    }

    /// <summary>Gets the options.</summary>
    public AspectNEngineOptions Options { get; }

    internal int CacheCount => _cache.Count;

    /// <summary>Compiles member expression into a matching pattern.</summary>
    /// <param name="source">Specifies the source.</param>
    /// <param name="compilationOptions">Specifies the compilation options.</param>
    /// <returns>Returns the result of the operation.</returns>
    public AspectNCompilation CompileMemberExpression(
        string source,
        AspectNCompilationOptions? compilationOptions = null)
    {
        return Compile(
            source,
            AspectNParseKind.MemberExpression,
            compilationOptions);
    }

    /// <summary>Compiles type pattern into a matching pattern.</summary>
    /// <param name="source">Specifies the source.</param>
    /// <param name="compilationOptions">Specifies the compilation options.</param>
    /// <returns>Returns the result of the operation.</returns>
    public AspectNCompilation CompileTypePattern(
        string source,
        AspectNCompilationOptions? compilationOptions = null)
    {
        return Compile(
            source,
            AspectNParseKind.TypePattern,
            compilationOptions);
    }

    private AspectNCompilation Compile(
        string source,
        AspectNParseKind parseKind,
        AspectNCompilationOptions? compilationOptions)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(source);
#else
        if (source is null) throw new ArgumentNullException(nameof(source));
#endif

        var effectiveCompilationOptions = compilationOptions ??
            new AspectNCompilationOptions();
        var key = new CompiledPatternCacheKey(
            source,
            parseKind,
            effectiveCompilationOptions);
        return _cache.GetOrAdd(
            key,
            () => CompileUncached(
                source,
                parseKind,
                effectiveCompilationOptions));
    }

    private static AspectNCompilation CompileUncached(
        string source,
        AspectNParseKind parseKind,
        AspectNCompilationOptions compilationOptions)
    {
        var syntaxTree = parseKind == AspectNParseKind.MemberExpression
            ? AspectNParser.ParseMemberExpression(source)
            : AspectNParser.ParseTypePattern(source);
        return AspectNCompilation.Create(
            syntaxTree,
            compilationOptions);
    }
}
