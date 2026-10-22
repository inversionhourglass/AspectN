using AspectN.Diagnostics;
using AspectN.Metadata;
using System.Text;

namespace AspectN.TypeSystem;

// 公开映射入口创建独立实例；查询中的映射会把同一子预算显式传给全部后代映射。
internal sealed class MappingContext(AspectNMappingOptions options, CancellationToken cancellationToken,
    TypeSystemQueryContext? query = null)
{
    private readonly object _gate = new();
    private readonly OperationQuota _work = new(options.MaximumWork);
    private readonly OperationQuota _attributes = new(options.MaximumAttributeRecords);

    public AspectNMappingOptions Options { get; } = options;
    public CancellationToken CancellationToken { get; } = cancellationToken;
    public TypeSystemQueryContext? Query { get; } = query;

    public void SpendWork(long count = 1) => Spend(count, false);
    public void ScanAttribute() => Spend(1, true);

    public IEnumerable<T> Attributes<T>(IEnumerable<T> records)
    {
        foreach (var record in records) { ScanAttribute(); yield return record; }
    }

    private void Spend(long count, bool attribute)
    {
        CancellationToken.ThrowIfCancellationRequested();
#if NET10_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
#else
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
#endif
        lock (_gate)
        {
            var failed = !_work.CanSpend(count) ? _work : attribute && !_attributes.CanSpend(count) ? _attributes : null;
            if (failed is not null)
                Fail(ReferenceEquals(failed, _work) ? nameof(Options.MaximumWork) : nameof(Options.MaximumAttributeRecords),
                    failed.Limit, failed.Used, count);
            if (Query is not null && !(attribute ? Query.TryConsumeAttributeRecords(count) : Query.TryConsumeWork(count)))
                Query.ThrowIfExhausted();
            _work.Spend(count);
            if (attribute) _attributes.Spend(count);
        }
    }

    public void CheckTextLength(long currentLength, long additionalLength)
    {
        CancellationToken.ThrowIfCancellationRequested();
        var limit = Options.MaximumGeneratedTextLength;
        if (!limit.IsUnlimited && (currentLength > limit.Value || additionalLength > limit.Value - currentLength))
            Fail(nameof(Options.MaximumGeneratedTextLength), limit, currentLength, additionalLength);
        if (additionalLength > 0) SpendWork(additionalLength);
    }

    public string Text(string text)
    {
        CheckTextLength(0, text.Length);
        return text;
    }

    public bool StringsEqual(string left, string right)
    {
        SpendWork(Math.Max(1L, left.Length + (long)right.Length));
        return StringComparer.Ordinal.Equals(left, right);
    }

    public string Join(string separator, IEnumerable<string> parts)
    {
        var output = new StringBuilder();
        var first = true;
        foreach (var part in parts)
        {
            SpendWork();
            if (!first) { CheckTextLength(output.Length, separator.Length); output.Append(separator); }
            first = false;
            CheckTextLength(output.Length, part.Length);
            output.Append(part);
        }
        return output.ToString();
    }

    public string Concat(params string[] parts) => Join(string.Empty, parts);

    public string FullName(TypeIdentity identity)
    {
        var chain = new Stack<TypeIdentity>();
        for (var level = identity; level is not null; level = level.ContainingType)
        {
            SpendWork();
            CheckTextLength(0, level.MetadataName.Length);
            chain.Push(level);
        }
        var names = new List<string>();
        while (chain.Count > 0)
        {
            SpendWork();
            var level = chain.Pop();
            if (names.Count == 0 && level.Namespace.Length > 0) names.Add(Concat(level.Namespace, "."));
            else if (names.Count > 0) names.Add("/");
            names.Add(level.MetadataName);
            if (level.GenericArity != 0) names.Add(Concat("`", level.GenericArity.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        return Join(string.Empty, names);
    }

    public void CheckDepth(long depth)
    {
        CancellationToken.ThrowIfCancellationRequested();
        if (Options.MaximumTypeDepth.ExceededBy(depth))
            throw new AspectNLimitException(new AspectNDiagnostic(DiagnosticCodes.MetadataDepthExceeded, DiagnosticSeverity.Warning,
                $"Mapping exceeded MaximumTypeDepth ({Options.MaximumTypeDepth}); requested {depth} levels."));
    }

    public void CheckArity(long arity, bool method = false)
    {
        CancellationToken.ThrowIfCancellationRequested();
        var limit = method ? Options.MaximumMethodGenericParameters : Options.MaximumTypeGenericParameters;
        if (limit.ExceededBy(arity))
            throw new AspectNLimitException(new AspectNDiagnostic(DiagnosticCodes.MetadataGenericArityExceeded, DiagnosticSeverity.Warning,
                $"Mapping exceeded {(method ? nameof(Options.MaximumMethodGenericParameters) : nameof(Options.MaximumTypeGenericParameters))} ({limit}); requested {arity} slots."));
    }

    private void Fail(string name, ResourceLimit limit, long used, long requested)
    {
        CancellationToken.ThrowIfCancellationRequested();
        var diagnostic = new AspectNDiagnostic(DiagnosticCodes.ResourceLimitExceeded, DiagnosticSeverity.Warning,
            $"Mapping exceeded {name} ({limit}); used {used}, requested {requested} units.");
        Query?.ReportResourceLimit(diagnostic);
        throw new AspectNLimitException(diagnostic);
    }
}
