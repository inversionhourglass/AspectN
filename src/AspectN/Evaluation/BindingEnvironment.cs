using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Evaluation;

internal sealed class BindingEnvironment
{
    private List<Alternative> _alternatives;

    public BindingEnvironment()
        : this(
            new List<Alternative>
            {
                new(new Dictionary<VariableSymbol, TypeSignature>(), false)
            })
    {
    }

    private BindingEnvironment(
        List<Alternative> alternatives)
    {
        _alternatives = alternatives;
    }

    public BindingEnvironment Clone()
    {
        return new BindingEnvironment(
            _alternatives
                .Select(alternative => alternative.Clone())
                .ToList());
    }

    public IEnumerable<BindingEnvironment> CloneAlternatives()
    {
        return _alternatives.Select(alternative =>
            new BindingEnvironment(
                new List<Alternative>
                {
                    alternative.Clone()
                }));
    }

    public void SetUnknown(bool isUnknown)
    {
        foreach (var alternative in _alternatives)
        {
            alternative.IsUnknown = isUnknown;
        }
    }

    public MatchStatus GetStatus(MatchStatus status)
    {
        if (status != MatchStatus.Matched) return status;
        if (_alternatives.Count == 0) return MatchStatus.NotMatched;
        return _alternatives.Any(alternative => !alternative.IsUnknown)
            ? MatchStatus.Matched
            : MatchStatus.Unknown;
    }

    public MatchStatus Bind(
        VariableSymbol variable,
        TypeSignature candidate,
        ITypeSystem typeSystem,
        ICollection<AspectNDiagnostic> diagnostics)
    {
        var surviving = new List<Alternative>();
        var hasMatched = false;
        foreach (var alternative in _alternatives)
        {
            if (!alternative.Bindings.TryGetValue(variable, out var existing))
            {
                alternative.Bindings.Add(variable, candidate);
                surviving.Add(alternative);
                hasMatched = true;
                continue;
            }

            var result = typeSystem.AreEqual(existing, candidate);
            foreach (var diagnostic in result.Diagnostics)
            {
                diagnostics.Add(diagnostic);
            }

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

    public void ReplaceWithAlternativesFrom(
        IEnumerable<BindingEnvironment> branches)
    {
        _alternatives = branches
            .SelectMany(branch => branch._alternatives)
            .Select(alternative => alternative.Clone())
            .ToList();
    }

    // 未知约束跟随绑定分支传播，后续筛选只剩该分支时仍应报告 Unknown。
    private sealed class Alternative(
        Dictionary<VariableSymbol, TypeSignature> bindings,
        bool isUnknown)
    {
        public Dictionary<VariableSymbol, TypeSignature> Bindings { get; } = bindings;

        public bool IsUnknown { get; set; } = isUnknown;

        public Alternative Clone()
        {
            return new Alternative(new Dictionary<VariableSymbol, TypeSignature>(Bindings), IsUnknown);
        }
    }
}
