using AspectN.Execution;
using AspectN.Diagnostics;
using AspectN.Syntax;

namespace AspectN.Binding;

internal sealed partial class Binder
{
    private async StackWork<BoundMemberPattern> BindMemberPatternWork(MemberPathSyntax syntax, VariableScope scope, bool isProperty)
    {
        var isNegated = syntax.NegationToken is not null;
        var pathScope = isNegated ? scope.Clone() : scope;
        if (isNegated) _negationDepth++;
        try
        {
            var declaringType = syntax.DeclaringType is null
                ? null
                : await BindDeclarationTypeWork(syntax.DeclaringType, pathScope, IsUnderNegation,
                    isProperty ? DeclarationSource.PropertyDeclaringType : DeclarationSource.DeclaringType);
            var name = await BindMemberNameWork(syntax.Name, pathScope, isProperty, IsUnderNegation);
            return new BoundMemberPattern(declaringType, name, isNegated);
        }
        finally
        {
            if (isNegated)
            {
                _negationDepth--;
                scope.HideNewVariablesFrom(pathScope);
            }
        }
    }

    private async StackWork<BoundMemberNamePattern> BindMemberNameWork(
        MemberNamePatternSyntax syntax, VariableScope scope, bool isProperty, bool underNegation)
    {
        switch (syntax)
        {
            case NamedMemberPatternSyntax named:
                var segment = named.Segment;
                var name = string.Concat(segment.NameParts.Select(token => token.Text));
                if (isProperty && segment.GenericArguments is not null)
                {
                    _diagnostics.Add(
                        DiagnosticCodes.InvalidGenericClause,
                        DiagnosticSeverity.Error,
                        "A property name cannot have a generic clause; remove the clause.",
                        segment.GenericArguments.FullSpan);
                }
                return new BoundNamedMemberPattern(named.FullSpan, name,
                    isProperty ? null : await BindGenericPatternWork(segment.GenericArguments, name == "*", scope, underNegation,
                        DeclarationSource.Method));
            case ParenthesizedMemberNamePatternSyntax parenthesized:
                return await BindMemberNameWork(parenthesized.Pattern, scope, isProperty, underNegation);
            case UnaryMemberNamePatternSyntax unary:
                var childScope = scope.Clone();
                var operand = await BindMemberNameWork(unary.Operand, childScope, isProperty, true);
                scope.HideNewVariablesFrom(childScope);
                return new BoundUnaryMemberNamePattern(unary.FullSpan, operand);
            case BinaryMemberNamePatternSyntax binary when binary.OperatorToken.Kind == SyntaxKind.AmpersandAmpersandToken:
                return new BoundBinaryMemberNamePattern(binary.FullSpan,
                    await BindMemberNameWork(binary.Left, scope, isProperty, underNegation),
                    BoundLogicalOperator.And,
                    await BindMemberNameWork(binary.Right, scope, isProperty, underNegation));
            case BinaryMemberNamePatternSyntax binary:
                var leftScope = scope.Clone();
                var rightScope = scope.Clone();
                var left = await BindMemberNameWork(binary.Left, leftScope, isProperty, underNegation);
                var right = await BindMemberNameWork(binary.Right, rightScope, isProperty, underNegation);
                ReportDuplicateBranchVariables(scope, leftScope, rightScope, binary.OperatorToken);
                scope.HideNewVariablesFrom(leftScope);
                scope.HideNewVariablesFrom(rightScope);
                return new BoundBinaryMemberNamePattern(binary.FullSpan, left, BoundLogicalOperator.Or, right);
            default:
                throw new InvalidOperationException($"Unsupported member-name syntax '{syntax.Kind}'.");
        }
    }
}
