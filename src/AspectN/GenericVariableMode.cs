namespace AspectN;

/// <summary>Determines how generic pattern variables are interpreted.</summary>
public enum GenericVariableMode
{
    /// <summary>References a fixed generic parameter slot in the candidate declaration.</summary>
    Declaration = 0,

    /// <summary>Captures arbitrary supported types using finite binding budgets.</summary>
    Capture = 1
}
