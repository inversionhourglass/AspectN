using AspectN.Text;

namespace AspectN.Binding;

internal enum BoundLogicalOperator
{
    And,
    Or
}

internal abstract class BoundExpression(
    TextSpan span,
    SelectorTargetDomain targetDomain) : BoundNode(span)
{
    public SelectorTargetDomain TargetDomain { get; } = targetDomain;
}

internal sealed class BoundUnaryExpression(TextSpan span, BoundExpression operand) : BoundExpression(span, SelectorTargetDomain.All)
{
    public BoundExpression Operand { get; } = operand ?? throw new ArgumentNullException(nameof(operand));
}

internal sealed class BoundBinaryExpression(
    TextSpan span,
    BoundExpression left,
    BoundLogicalOperator @operator,
    BoundExpression right,
    SelectorTargetDomain targetDomain) : BoundExpression(span, targetDomain)
{
    public BoundExpression Left { get; } = left ?? throw new ArgumentNullException(nameof(left));

    public BoundLogicalOperator Operator { get; } = @operator;

    public BoundExpression Right { get; } = right ?? throw new ArgumentNullException(nameof(right));
}
