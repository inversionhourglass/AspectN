namespace AspectN;

/// <summary>Fixes generic variable semantics and match limits for a compiled plan.</summary>
/// <remarks>Immutable and safe for concurrent reuse. Parsing limits belong to the syntax tree and cannot be replaced here. MatchOptions are used for every Match call; each call has independent counters. Adapter mapping limits remain independent.</remarks>
public sealed class AspectNCompilationOptions
{
    /// <summary>Creates the options retained by the compilation and its compiled pattern.</summary>
    /// <param name="genericVariableMode">Declaration (default) references declaration slots; Capture binds arbitrary candidate types.</param>
    /// <param name="matchOptions">Limits for candidate validation, evaluation, and queries; null uses the defaults in AspectNMatchOptions.</param>
    /// <exception cref="ArgumentOutOfRangeException">The variable mode is undefined.</exception>
    public AspectNCompilationOptions(GenericVariableMode genericVariableMode = GenericVariableMode.Declaration,
        AspectNMatchOptions? matchOptions = null)
    {
        if (genericVariableMode is not (GenericVariableMode.Declaration or GenericVariableMode.Capture))
            throw new ArgumentOutOfRangeException(nameof(genericVariableMode));
        GenericVariableMode = genericVariableMode;
        MatchOptions = matchOptions ?? new AspectNMatchOptions();
    }

    /// <summary>Gets the variable semantics fixed at compilation, Declaration by default.</summary>
    public GenericVariableMode GenericVariableMode { get; }

    /// <summary>Gets the immutable limits shared by all branches and built-in queries of each match.</summary>
    /// <value>The supplied object, or a default AspectNMatchOptions instance.</value>
    /// <remarks>Each match creates a fresh budget. Declaration mode does not use Capture quotas but still uses total work. Independent public type-system queries use their instance defaults instead.</remarks>
    public AspectNMatchOptions MatchOptions { get; }
}
