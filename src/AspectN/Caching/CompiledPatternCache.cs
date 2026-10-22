using AspectN.Parsing;

namespace AspectN.Caching;

internal sealed class CompiledPatternCache(int capacity)
{
    private readonly Dictionary<CompiledPatternCacheKey, LinkedListNode<Entry>> _entries = new();
    private readonly object _gate = new();
    private readonly LinkedList<Entry> _recency = new();

    public int Capacity { get; } = capacity >= 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));

    public int Count
    {
        get
        {
            lock (_gate) return _entries.Count;
        }
    }

    public AspectNCompilation GetOrAdd(CompiledPatternCacheKey key, Func<AspectNCompilation> factory, CancellationToken cancellationToken = default)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(factory);
#else
        if (factory is null) throw new ArgumentNullException(nameof(factory));
#endif
        cancellationToken.ThrowIfCancellationRequested();
        if (Capacity == 0) return factory();
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_entries.TryGetValue(key, out var existing))
            {
                MoveToMostRecent(existing);
                return existing.Value.Value;
            }
        }

        // 未命中者独立编译；不共享尚未完成的工作或调用者的取消令牌。
        var completed = factory();
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_entries.TryGetValue(key, out var existing))
            {
                MoveToMostRecent(existing);
                return existing.Value.Value;
            }
            var node = _recency.AddFirst(new Entry(key, completed));
            _entries.Add(key, node);
            if (_entries.Count > Capacity)
            {
                var leastRecent = _recency.Last!;
                _recency.RemoveLast();
                _entries.Remove(leastRecent.Value.Key);
            }
            return completed;
        }
    }

    private void MoveToMostRecent(LinkedListNode<Entry> node)
    {
        if (ReferenceEquals(node, _recency.First)) return;
        _recency.Remove(node);
        _recency.AddFirst(node);
    }

    private sealed class Entry(CompiledPatternCacheKey key, AspectNCompilation value)
    {
        public CompiledPatternCacheKey Key { get; } = key;
        public AspectNCompilation Value { get; } = value;
    }
}

internal readonly struct CompiledPatternCacheKey(string source, AspectNParseKind parseKind,
    AspectNParseOptions parseOptions, AspectNCompilationOptions compilationOptions) : IEquatable<CompiledPatternCacheKey>
{
    private readonly string _source = source;
    private readonly AspectNParseKind _parseKind = parseKind;
    private readonly GenericVariableMode _mode = compilationOptions.GenericVariableMode;
    private readonly ResourceLimit[] _limits = parseOptions.Limits.Concat(compilationOptions.MatchOptions.Limits).ToArray();
    private readonly TimeLimit _regexTimeout = compilationOptions.MatchOptions.RegexTimeout;

    public bool Equals(CompiledPatternCacheKey other) => _parseKind == other._parseKind && _mode == other._mode &&
        _regexTimeout == other._regexTimeout && _limits.SequenceEqual(other._limits) && StringComparer.Ordinal.Equals(_source, other._source);

    public override bool Equals(object? obj) => obj is CompiledPatternCacheKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = (StringComparer.Ordinal.GetHashCode(_source) * 397 ^ (int)_parseKind) * 397 ^ (int)_mode;
            hash = hash * 397 ^ _regexTimeout.GetHashCode();
            foreach (var limit in _limits) hash = hash * 397 ^ limit.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(CompiledPatternCacheKey left, CompiledPatternCacheKey right) => left.Equals(right);
    public static bool operator !=(CompiledPatternCacheKey left, CompiledPatternCacheKey right) => !left.Equals(right);
}
