using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Evaluation;

internal sealed class BindingEnvironment
{
    private List<Dictionary<VariableSymbol, TypeSignature>> _alternatives;

    public BindingEnvironment()
        : this(
            new List<Dictionary<VariableSymbol, TypeSignature>>
            {
                new()
            })
    {
    }

    private BindingEnvironment(
        List<Dictionary<VariableSymbol, TypeSignature>> alternatives)
    {
        _alternatives = alternatives;
    }

    public BindingEnvironment Clone()
    {
        return new BindingEnvironment(
            _alternatives
                .Select(alternative =>
                    new Dictionary<VariableSymbol, TypeSignature>(alternative))
                .ToList());
    }

    public MatchStatus Bind(
        VariableSymbol variable,
        TypeSignature candidate,
        ITypeSystem typeSystem,
        ICollection<AspectNDiagnostic> diagnostics)
    {
        var matched = new List<Dictionary<VariableSymbol, TypeSignature>>();
        var hasUnknown = false;
        foreach (var alternative in _alternatives)
        {
            if (!alternative.TryGetValue(variable, out var existing))
            {
                alternative.Add(variable, candidate);
                matched.Add(alternative);
                continue;
            }

            var result = typeSystem.AreEqual(existing, candidate);
            foreach (var diagnostic in result.Diagnostics)
            {
                diagnostics.Add(diagnostic);
            }

            if (result.Status == MatchStatus.Matched)
            {
                matched.Add(alternative);
            }
            else if (result.Status == MatchStatus.Unknown)
            {
                hasUnknown = true;
            }
        }

        if (matched.Count > 0)
        {
            _alternatives = matched;
            return MatchStatus.Matched;
        }

        if (hasUnknown) return MatchStatus.Unknown;

        _alternatives.Clear();
        return MatchStatus.NotMatched;
    }

    public void ReplaceWithAlternativesFrom(
        IEnumerable<BindingEnvironment> branches)
    {
        _alternatives = branches
            .SelectMany(branch => branch._alternatives)
            .Select(alternative =>
                new Dictionary<VariableSymbol, TypeSignature>(alternative))
            .ToList();
    }
}
