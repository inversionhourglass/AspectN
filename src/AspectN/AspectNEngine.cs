using AspectN.Caching;
using AspectN.Extensibility;
using AspectN.Parsing;

namespace AspectN;

public sealed class AspectNEngine
{
    private readonly CompiledPatternCache _cache;

    public AspectNEngine(
        AspectNEngineOptions? options = null,
        AspectNExtensionRegistry? extensions = null)
    {
        Options = options ?? new AspectNEngineOptions();
        Extensions = extensions ?? AspectNExtensionRegistry.Empty;
        _cache = new CompiledPatternCache(Options.CacheCapacity);
    }

    public AspectNEngineOptions Options { get; }

    public AspectNExtensionRegistry Extensions { get; }

    internal int CacheCount => _cache.Count;

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
