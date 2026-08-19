using AspectN.Caching;
using AspectN.Extensibility;
using AspectN.Parsing;

namespace AspectN;

/// <summary>Provides the high-level API for compiling AspectN expressions and type patterns.</summary>
public sealed class AspectNEngine
{
    private readonly CompiledPatternCache _cache;

    /// <summary>Initializes a new instance of the AspectNEngine class.</summary>
    /// <param name="options">Specifies the options.</param>
    /// <param name="extensions">Specifies the extensions.</param>
    public AspectNEngine(
        AspectNEngineOptions? options = null,
        AspectNExtensionRegistry? extensions = null)
    {
        Options = options ?? new AspectNEngineOptions();
        Extensions = extensions ?? AspectNExtensionRegistry.Empty;
        _cache = new CompiledPatternCache(Options.CacheCapacity);
    }

    /// <summary>Gets the options.</summary>
    public AspectNEngineOptions Options { get; }

    /// <summary>Gets the extensions.</summary>
    public AspectNExtensionRegistry Extensions { get; }

    internal int CacheCount => _cache.Count;

    /// <summary>Compiles member expression into a matching pattern.</summary>
    /// <param name="source">Specifies the source.</param>
    /// <param name="parseOptions">Specifies the parse options.</param>
    /// <param name="compilationOptions">Specifies the compilation options.</param>
    /// <param name="extensions">Specifies the extensions.</param>
    /// <returns>Returns the result of the operation.</returns>
    public AspectNCompilation CompileMemberExpression(
        string source,
        AspectNParseOptions? parseOptions = null,
        AspectNCompilationOptions? compilationOptions = null,
        AspectNExtensionRegistry? extensions = null)
    {
        return Compile(
            source,
            AspectNParseKind.MemberExpression,
            parseOptions,
            compilationOptions,
            extensions);
    }

    /// <summary>Compiles type pattern into a matching pattern.</summary>
    /// <param name="source">Specifies the source.</param>
    /// <param name="parseOptions">Specifies the parse options.</param>
    /// <param name="compilationOptions">Specifies the compilation options.</param>
    /// <param name="extensions">Specifies the extensions.</param>
    /// <returns>Returns the result of the operation.</returns>
    public AspectNCompilation CompileTypePattern(
        string source,
        AspectNParseOptions? parseOptions = null,
        AspectNCompilationOptions? compilationOptions = null,
        AspectNExtensionRegistry? extensions = null)
    {
        return Compile(
            source,
            AspectNParseKind.TypePattern,
            parseOptions,
            compilationOptions,
            extensions);
    }

    private AspectNCompilation Compile(
        string source,
        AspectNParseKind parseKind,
        AspectNParseOptions? parseOptions,
        AspectNCompilationOptions? compilationOptions,
        AspectNExtensionRegistry? extensions)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(source);
#else
        if (source is null) throw new ArgumentNullException(nameof(source));
#endif

        var effectiveParseOptions = parseOptions ?? new AspectNParseOptions();
        var effectiveCompilationOptions = compilationOptions ??
            new AspectNCompilationOptions();
        var effectiveExtensions = extensions ?? Extensions;
        var key = new CompiledPatternCacheKey(
            source,
            parseKind,
            effectiveParseOptions.LanguageVersion,
            effectiveCompilationOptions,
            effectiveExtensions);
        return _cache.GetOrAdd(
            key,
            () => CompileUncached(
                source,
                parseKind,
                effectiveParseOptions,
                effectiveCompilationOptions,
                effectiveExtensions));
    }

    private static AspectNCompilation CompileUncached(
        string source,
        AspectNParseKind parseKind,
        AspectNParseOptions parseOptions,
        AspectNCompilationOptions compilationOptions,
        AspectNExtensionRegistry extensions)
    {
        var syntaxTree = parseKind == AspectNParseKind.MemberExpression
            ? AspectNParser.ParseMemberExpression(source, parseOptions)
            : AspectNParser.ParseTypePattern(source, parseOptions);
        return AspectNCompilation.Create(
            syntaxTree,
            compilationOptions,
            extensions);
    }
}
