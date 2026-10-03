using AspectN.Metadata;

namespace AspectN.AdapterInternals;

internal static class OperatorMetadata
{
    // 名称只是元数据约定的一部分；普通 op_ 方法以及无效的特殊名称仍属于普通方法。
    public static MethodKind Classify(string name, bool hasOperatorMetadata, bool isStatic, int parameterCount, bool returnsVoid)
    {
        if (!hasOperatorMetadata) return MethodKind.OrdinaryMethod;
        if (!isStatic)
        {
            var instanceCount = name switch
            {
                "op_IncrementAssignment" or "op_DecrementAssignment" or
                "op_CheckedIncrementAssignment" or "op_CheckedDecrementAssignment" => 0,
                "op_AdditionAssignment" or "op_SubtractionAssignment" or "op_MultiplicationAssignment" or
                "op_DivisionAssignment" or "op_ModulusAssignment" or "op_BitwiseAndAssignment" or
                "op_BitwiseOrAssignment" or "op_ExclusiveOrAssignment" or "op_LeftShiftAssignment" or
                "op_RightShiftAssignment" or "op_UnsignedRightShiftAssignment" or
                "op_CheckedAdditionAssignment" or "op_CheckedSubtractionAssignment" or
                "op_CheckedMultiplicationAssignment" or "op_CheckedDivisionAssignment" => 1,
                _ => -1
            };
            return returnsVoid && parameterCount == instanceCount ? MethodKind.Operator : MethodKind.OrdinaryMethod;
        }
        if (returnsVoid) return MethodKind.OrdinaryMethod;
        if (parameterCount == 1 && name is "op_Implicit" or "op_Explicit" or "op_CheckedExplicit")
        {
            return MethodKind.Conversion;
        }

        var expectedCount = name switch
        {
            "op_UnaryPlus" or "op_UnaryNegation" or "op_LogicalNot" or "op_OnesComplement" or
            "op_Increment" or "op_Decrement" or "op_True" or "op_False" or
            "op_CheckedUnaryNegation" or "op_CheckedIncrement" or "op_CheckedDecrement" => 1,
            "op_Addition" or "op_Subtraction" or "op_Multiply" or "op_Division" or "op_Modulus" or
            "op_BitwiseAnd" or "op_BitwiseOr" or "op_ExclusiveOr" or "op_LeftShift" or
            "op_RightShift" or "op_UnsignedRightShift" or "op_Equality" or "op_Inequality" or
            "op_GreaterThan" or "op_LessThan" or "op_GreaterThanOrEqual" or "op_LessThanOrEqual" or
            "op_CheckedAddition" or "op_CheckedSubtraction" or "op_CheckedMultiply" or "op_CheckedDivision" or
            "op_Exponent" or "op_IntegerDivision" or "op_Concatenate" or "op_Like" => 2,
            _ => -1
        };
        return parameterCount == expectedCount ? MethodKind.Operator : MethodKind.OrdinaryMethod;
    }
}
