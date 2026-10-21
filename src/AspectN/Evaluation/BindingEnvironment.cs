using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Metadata;
using AspectN.TypeSystem;
using System.Runtime.CompilerServices;

namespace AspectN.Evaluation;

internal sealed class BindingEnvironment
{
    private readonly BindingBudget _budget;
    private List<Alternative> _alternatives;

    public BindingEnvironment(BindingBudget budget)
    {
        _budget = budget;
        budget.Spend(1);
        _alternatives = [new(new Dictionary<VariableSymbol, TypeSignature>(), false)];
    }

    private BindingEnvironment(BindingBudget budget, List<Alternative> alternatives)
    {
        _budget = budget;
        _alternatives = alternatives;
    }

    public BindingEnvironment Clone()
    {
        var alternatives = new List<Alternative>();
        foreach (var alternative in _alternatives) alternatives.Add(Clone(alternative));
        return new BindingEnvironment(_budget, alternatives);
    }

    public IEnumerable<BindingEnvironment> CloneAlternatives()
    {
        foreach (var alternative in _alternatives)
            yield return new BindingEnvironment(_budget, [Clone(alternative)]);
    }

    public void SetUnknown(bool isUnknown)
    {
        foreach (var alternative in _alternatives)
        {
            _budget.Spend(1);
            alternative.IsUnknown = isUnknown;
        }
    }

    public MatchStatus GetStatus(MatchStatus status)
    {
        if (status != MatchStatus.Matched) return status;
        if (_alternatives.Count == 0) return MatchStatus.NotMatched;
        foreach (var alternative in _alternatives)
        {
            _budget.Spend(1);
            if (!alternative.IsUnknown) return MatchStatus.Matched;
        }
        return MatchStatus.Unknown;
    }

    public MatchStatus Bind(VariableSymbol variable, TypeSignature candidate,
        ITypeSystem typeSystem, Action<AspectNDiagnostic> addDiagnostic)
    {
        var surviving = new List<Alternative>();
        var hasMatched = false;
        foreach (var alternative in _alternatives)
        {
            _budget.Spend(1);
            if (!alternative.Bindings.TryGetValue(variable, out var existing))
            {
                _budget.Spend(1);
                alternative.Bindings.Add(variable, candidate);
                surviving.Add(alternative);
                hasMatched = true;
                continue;
            }

            _budget.Spend(1);
            var result = typeSystem.AreEqual(existing, candidate);
            foreach (var diagnostic in result.Diagnostics) addDiagnostic(diagnostic);
            if (result.Status == MatchStatus.Matched)
            {
                surviving.Add(alternative);
                hasMatched = true;
            }
            else if (result.Status == MatchStatus.Unknown)
            {
                alternative.IsUnknown = true;
                surviving.Add(alternative);
            }
        }

        _alternatives = surviving;
        if (hasMatched) return MatchStatus.Matched;
        return surviving.Count > 0 ? MatchStatus.Unknown : MatchStatus.NotMatched;
    }

    public AlternativeCollector CreateCollector() => new(this);

    public void ReplaceWithAlternativesFrom(IEnumerable<BindingEnvironment> branches)
    {
        var collector = CreateCollector();
        foreach (var branch in branches) collector.Add(branch, MatchStatus.Matched);
        collector.Apply();
    }

    private Alternative Clone(Alternative alternative)
    {
        _budget.Spend(1L + alternative.Bindings.Count);
        return new Alternative(new Dictionary<VariableSymbol, TypeSignature>(alternative.Bindings), alternative.IsUnknown);
    }

    // 在产生分支时逐项去重、限额并复制，禁止先积累上下文列表再批量放大。
    internal sealed class AlternativeCollector
    {
        private readonly BindingEnvironment _owner;
        private readonly HashSet<Alternative> _unique;
        private readonly List<Alternative> _retained = [];

        public AlternativeCollector(BindingEnvironment owner)
        {
            _owner = owner;
            _unique = new HashSet<Alternative>(new AlternativeComparer(owner._budget));
        }

        public void Add(BindingEnvironment branch, MatchStatus status)
        {
            if (status == MatchStatus.NotMatched) return;
            if (status == MatchStatus.Unknown) branch.SetUnknown(true);
            foreach (var alternative in branch._alternatives)
            {
                _owner._budget.Spend(1);
                if (_unique.Contains(alternative)) continue;
                _owner._budget.CheckAlternativeCount(_retained.Count);
                var snapshot = _owner.Clone(alternative);
                _unique.Add(snapshot);
                _retained.Add(snapshot);
            }
        }

        public void Apply() => _owner._alternatives = _retained;
    }

    // 只合并相同签名实例的绑定，避免丢失后端保留的闭合泛型查询上下文。
    private sealed class AlternativeComparer(BindingBudget budget) : IEqualityComparer<Alternative>
    {
        public bool Equals(Alternative? left, Alternative? right)
        {
            budget.Spend(1);
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null || left.IsUnknown != right.IsUnknown ||
                left.Bindings.Count != right.Bindings.Count) return false;
            foreach (var binding in left.Bindings)
            {
                budget.Spend(1);
                if (!right.Bindings.TryGetValue(binding.Key, out var value) || !ReferenceEquals(binding.Value, value))
                    return false;
            }
            return true;
        }

        public int GetHashCode(Alternative alternative)
        {
            budget.Spend(1);
            var hash = alternative.IsUnknown ? 1 : 0;
            foreach (var binding in alternative.Bindings)
            {
                budget.Spend(1);
                hash ^= RuntimeHelpers.GetHashCode(binding.Key) * 397 ^ RuntimeHelpers.GetHashCode(binding.Value);
            }
            return hash;
        }
    }

    // 未知约束跟随绑定分支传播，后续筛选只剩该分支时仍应报告 Unknown。
    private sealed class Alternative(Dictionary<VariableSymbol, TypeSignature> bindings, bool isUnknown)
    {
        public Dictionary<VariableSymbol, TypeSignature> Bindings { get; } = bindings;
        public bool IsUnknown { get; set; } = isUnknown;
    }
}
