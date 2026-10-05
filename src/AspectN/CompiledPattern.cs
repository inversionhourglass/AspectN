namespace AspectN;

/// <summary>Stores options shared by compiled member and type patterns.</summary>
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
