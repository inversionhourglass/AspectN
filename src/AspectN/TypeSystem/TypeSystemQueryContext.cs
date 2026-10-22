using AspectN.Diagnostics;
using AspectN.Metadata;
using AspectN.Text;

namespace AspectN.TypeSystem;

// 一次操作共享的预算；锁内先验证所有相关余额，再同时扣减，避免专项失败后消耗总额度。
internal sealed class TypeSystemQueryContext(AspectNMatchOptions options, CancellationToken cancellationToken)
{
    private readonly object _gate = new();
    private readonly OperationQuota _work = new(options.MaximumWork);
    private readonly OperationQuota _nodes = new(options.MaximumRelationNodes);
    private readonly OperationQuota _edges = new(options.MaximumRelationEdges);
    private readonly OperationQuota _attributes = new(options.MaximumAttributeRecords);
    private readonly OperationQuota _binding = new(options.MaximumBindingWork);
    private AspectNDiagnostic? _limitDiagnostic;

    public AspectNMatchOptions Options { get; } = options;
    public CancellationToken CancellationToken { get; } = cancellationToken;
    public TextSpan? Span { get; set; }
    public bool IsExhausted { get { lock (_gate) return _limitDiagnostic is not null; } }
    public AspectNDiagnostic? LimitDiagnostic { get { lock (_gate) return _limitDiagnostic; } }
    public long BindingWorkUsed { get { lock (_gate) return _binding.Used; } }

    public bool TryConsumeWork(long count) => TryConsume(null, count, nameof(Options.MaximumWork));
    public bool TryConsumeRelationNodes(long count) => TryConsume(_nodes, count, nameof(Options.MaximumRelationNodes));
    public bool TryConsumeRelationEdges(long count) => TryConsume(_edges, count, nameof(Options.MaximumRelationEdges));
    public bool TryConsumeAttributeRecords(long count) => TryConsume(_attributes, count, nameof(Options.MaximumAttributeRecords));
    public bool TryConsumeBindingWork(long count) => TryConsume(_binding, count, nameof(Options.MaximumBindingWork), DiagnosticCodes.BindingBudgetExceeded);

    private bool TryConsume(OperationQuota? quota, long count, string name, string? code = null)
    {
        CancellationToken.ThrowIfCancellationRequested();
#if NET10_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
#else
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
#endif
        lock (_gate)
        {
            CancellationToken.ThrowIfCancellationRequested();
            if (_limitDiagnostic is not null) return false;
            var failure = !_work.CanSpend(count) ? _work : quota is not null && !quota.CanSpend(count) ? quota : null;
            if (failure is not null)
            {
                var total = ReferenceEquals(failure, _work);
                _limitDiagnostic = new AspectNDiagnostic(total ? DiagnosticCodes.ResourceLimitExceeded : code ?? DiagnosticCodes.ResourceLimitExceeded,
                    DiagnosticSeverity.Warning,
                    $"Matching exceeded {(total ? nameof(Options.MaximumWork) : name)} ({failure.Limit}); used {failure.Used}, requested {count} units.", Span);
                return false;
            }
            _work.Spend(count);
            quota?.Spend(count);
            return true;
        }
    }

    public void SpendWork(long count = 1)
    {
        if (!TryConsumeWork(count)) ThrowIfExhausted();
    }

    public void ReportResourceLimit(AspectNDiagnostic diagnostic)
    {
        CancellationToken.ThrowIfCancellationRequested();
        if (diagnostic.Code != DiagnosticCodes.BindingBudgetExceeded && diagnostic.Code != DiagnosticCodes.ResourceLimitExceeded) return;
        lock (_gate) _limitDiagnostic ??= diagnostic;
    }

    public void ThrowIfExhausted()
    {
        CancellationToken.ThrowIfCancellationRequested();
        if (LimitDiagnostic is { } diagnostic) throw new AspectNLimitException(diagnostic);
    }

    public void CheckTextLength(long currentLength, long additionalLength, ResourceLimit limit, string name)
    {
        ValidateTextLength(currentLength, additionalLength, limit, name);
        if (additionalLength > 0) SpendWork(additionalLength);
    }

    public void ValidateTextLength(long currentLength, long additionalLength, ResourceLimit limit, string name)
    {
        CancellationToken.ThrowIfCancellationRequested();
        if (!limit.IsUnlimited && (currentLength > limit.Value || additionalLength > limit.Value - currentLength))
        {
            ReportResourceLimit(new AspectNDiagnostic(DiagnosticCodes.ResourceLimitExceeded, DiagnosticSeverity.Warning,
                $"Matching exceeded {name} ({limit}); used {currentLength}, requested {additionalLength} UTF-16 code units.", Span));
            ThrowIfExhausted();
        }
    }

    public bool StringsEqual(string left, string right, bool ignoreCase = false)
    {
        SpendWork(Math.Max(1L, (long)left.Length + right.Length));
        return (ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).Equals(left, right);
    }

    public bool AssembliesEqual(AssemblyIdentity left, AssemblyIdentity right) =>
        StringsEqual(left.Name, right.Name) && Equals(left.Version, right.Version) &&
        StringsEqual(left.CultureName, right.CultureName) && StringsEqual(left.PublicKeyToken, right.PublicKeyToken, true);

    public bool IdentitiesEqual(TypeIdentity? left, TypeIdentity? right)
    {
        while (left is not null && right is not null)
        {
            SpendWork();
            if (left.GenericArity != right.GenericArity || left.Kind != right.Kind ||
                !AssembliesEqual(left.Assembly, right.Assembly) || !StringsEqual(left.Namespace, right.Namespace) ||
                !StringsEqual(left.MetadataName, right.MetadataName)) return false;
            left = left.ContainingType;
            right = right.ContainingType;
        }
        return left is null && right is null;
    }
}

internal sealed class OperationQuota(ResourceLimit limit)
{
    private long _remaining = limit.IsUnlimited ? 0 : limit.Value;
    public ResourceLimit Limit { get; } = limit;
    public long Used => Limit.IsUnlimited ? 0 : Limit.Value - _remaining;
    public bool CanSpend(long count) => Limit.IsUnlimited || count <= _remaining;
    public void Spend(long count)
    {
        if (!Limit.IsUnlimited) _remaining -= count;
    }
}
