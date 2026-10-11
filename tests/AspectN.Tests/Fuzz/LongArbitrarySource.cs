namespace AspectN.Tests.Fuzz;

public sealed class LongArbitrarySource(string value)
{
    public string Value { get; } = value ?? throw new ArgumentNullException(nameof(value));
}
