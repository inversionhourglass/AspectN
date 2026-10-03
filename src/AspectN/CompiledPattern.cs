namespace AspectN;

/// <summary>Represents a compiled compiled pattern.</summary>
public abstract class CompiledPattern
{
    internal CompiledPattern(
        AspectNCompilationOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Gets the options.</summary>
    public AspectNCompilationOptions Options { get; }
}
