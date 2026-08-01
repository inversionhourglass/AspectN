using AspectN.Text;

namespace AspectN.Binding;

internal enum BoundLogicalOperator
{
    And,
    Or
}

internal abstract class BoundExpression : BoundNode
{
    protected BoundExpression(
        TextSpan span,
        SelectorTargetDomain targetDomain)
        : base(span)
    {
        TargetDomain = targetDomain;
    }

    public SelectorTargetDomain TargetDomain { get; }
}

internal sealed class BoundUnaryExpression : BoundExpression
{
    public BoundUnaryExpression(TextSpan span, BoundExpression operand)
        : base(span, SelectorTargetDomain.All)
    {
        Operand = operand ?? throw new ArgumentNullException(nameof(operand));
    }

    public BoundExpression Operand { get; }
}

internal sealed class BoundBinaryExpression : BoundExpression
{
    public BoundBinaryExpression(
        TextSpan span,
        BoundExpression left,
        BoundLogicalOperator @operator,
        BoundExpression right,
        SelectorTargetDomain targetDomain)
        : base(span, targetDomain)
    {
        Left = left ?? throw new ArgumentNullException(nameof(left));
        Operator = @operator;
        Right = right ?? throw new ArgumentNullException(nameof(right));
    }

    public BoundExpression Left { get; }

    public BoundLogicalOperator Operator { get; }

    public BoundExpression Right { get; }
}
