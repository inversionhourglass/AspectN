namespace AspectN.Benchmarks;

/// <summary>Identifies a precompiled matching workload with a fixed expected outcome.</summary>
public enum MatchScenario
{
    /// <summary>A method name match without modifier constraints.</summary>
    BareMethod,
    /// <summary>A successful public and static modifier match.</summary>
    Modifiers,
    /// <summary>A failed private modifier match against a public method.</summary>
    ModifierMiss,
    /// <summary>A successful accessibility alternative and static modifier match.</summary>
    ModifierAlternatives,
    /// <summary>A successful namespace and type-name wildcard match.</summary>
    Namespace,
    /// <summary>A failed namespace path after the type-name wildcard has matched.</summary>
    NamespaceMiss,
    /// <summary>A closed nested generic type match.</summary>
    NestedType,
    /// <summary>An open nested generic type matched with two generic variables.</summary>
    OpenVariables,
    /// <summary>A wildcard relationship target that enumerates ancestors.</summary>
    AncestorWalk
}
