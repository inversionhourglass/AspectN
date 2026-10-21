namespace AspectN.Binding;

internal enum DeclarationSource
{
    RootType,
    DeclaringType,
    PropertyDeclaringType,
    Method
}

internal enum DeclarationTypeStep
{
    ArrayElement,
    NullableUnderlying
}

// 来源相对原始候选固定；祖先和特性枚举不能重新解释同一占位符。
internal sealed class DeclarationSlot(DeclarationSource source, int containingDepth, int position,
    IEnumerable<DeclarationTypeStep>? path = null)
{
    public DeclarationSource Source { get; } = source;
    public int ContainingDepth { get; } = containingDepth;
    public int Position { get; } = position;
    public IReadOnlyList<DeclarationTypeStep> Path { get; } = (path ?? []).ToArray();
}
