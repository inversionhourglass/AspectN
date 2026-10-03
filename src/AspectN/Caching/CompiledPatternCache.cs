using AspectN.Extensibility;
using AspectN.Parsing;

namespace AspectN.Caching;

internal sealed class CompiledPatternCache
{
    private readonly Dictionary<
        CompiledPatternCacheKey,
        LinkedListNode<Entry>> _entries = new();

    private readonly object _gate = new();
    private readonly LinkedList<Entry> _recency = new();

    public CompiledPatternCache(int capacity)
    {
#if NET10_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
#else
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
#endif
        Capacity = capacity;
    }

    public int Capacity { get; }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public AspectNCompilation GetOrAdd(
        CompiledPatternCacheKey key,
        Func<AspectNCompilation> factory)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(factory);
#else
        if (factory is null) throw new ArgumentNullException(nameof(factory));
#endif

        if (Capacity == 0) return factory();

        Lazy<AspectNCompilation> lazy;
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                MoveToMostRecent(existing);
                lazy = existing.Value.Value;
            }
            else
            {
                lazy = new Lazy<AspectNCompilation>(
                    factory,
                    LazyThreadSafetyMode.ExecutionAndPublication);
                var node = _recency.AddFirst(new Entry(key, lazy));
                _entries.Add(key, node);
                EvictLeastRecentIfNeeded();
            }
        }

        try
        {
            return lazy.Value;
        }
        catch
        {
            RemoveFailedEntry(key, lazy);
            throw;
        }
    }

    private void MoveToMostRecent(LinkedListNode<Entry> node)
    {
        if (ReferenceEquals(node, _recency.First)) return;
        _recency.Remove(node);
        _recency.AddFirst(node);
    }

    private void EvictLeastRecentIfNeeded()
    {
        if (_entries.Count <= Capacity) return;
        var leastRecent = _recency.Last!;
        _recency.RemoveLast();
        _entries.Remove(leastRecent.Value.Key);
    }

    private void RemoveFailedEntry(
        CompiledPatternCacheKey key,
        Lazy<AspectNCompilation> value)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var node) ||
                !ReferenceEquals(node.Value.Value, value))
            {
                return;
            }

            _entries.Remove(key);
            _recency.Remove(node);
        }
    }

    private sealed class Entry(
        CompiledPatternCacheKey key,
        Lazy<AspectNCompilation> value)
    {
        public CompiledPatternCacheKey Key { get; } = key;

        public Lazy<AspectNCompilation> Value { get; } = value;
    }
}

internal readonly struct CompiledPatternCacheKey(
    string source,
    AspectNParseKind parseKind,
    AspectNCompilationOptions compilationOptions,
    AspectNExtensionRegistry extensions) :
    IEquatable<CompiledPatternCacheKey>
{
    public string Source { get; } = source ?? throw new ArgumentNullException(nameof(source));

    public AspectNParseKind ParseKind { get; } = parseKind;

    public long RegexTimeoutTicks { get; } = compilationOptions?.RegexTimeout.Ticks ??
            throw new ArgumentNullException(nameof(compilationOptions));

    public Guid ExtensionIdentity { get; } = extensions?.CacheIdentity ??
            throw new ArgumentNullException(nameof(extensions));

    public bool Equals(CompiledPatternCacheKey other)
    {
        return StringComparer.Ordinal.Equals(Source, other.Source) &&
               ParseKind == other.ParseKind &&
               RegexTimeoutTicks == other.RegexTimeoutTicks &&
               ExtensionIdentity == other.ExtensionIdentity;
    }

    public override bool Equals(object? obj)
    {
        return obj is CompiledPatternCacheKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = StringComparer.Ordinal.GetHashCode(Source);
            hash = (hash * 397) ^ (int)ParseKind;
            hash = (hash * 397) ^ RegexTimeoutTicks.GetHashCode();
            hash = (hash * 397) ^ ExtensionIdentity.GetHashCode();
            return hash;
        }
    }

    /// <summary>Determines whether the two values are equal.</summary>
    public static bool operator ==(
        CompiledPatternCacheKey left,
        CompiledPatternCacheKey right)
    {
        return left.Equals(right);
    }

    /// <summary>Determines whether the two values are not equal.</summary>
    public static bool operator !=(
        CompiledPatternCacheKey left,
        CompiledPatternCacheKey right)
    {
        return !left.Equals(right);
    }
}
