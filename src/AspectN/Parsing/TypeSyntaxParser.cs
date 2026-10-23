using AspectN.Execution;
using AspectN.Syntax;

namespace AspectN.Parsing;

// 两个入口复用类型语法，词法流、诊断与预算仍由各自入口持有。
internal abstract class TypeSyntaxParser
{
    protected abstract ParseContext Context { get; }
    protected abstract ParserDepthGuard DepthGuard { get; }
    protected abstract SyntaxToken Current { get; }
    protected abstract SyntaxToken NextToken();
    protected abstract SyntaxToken MatchToken(SyntaxKind expectedKind);
    protected abstract SyntaxToken PeekNextToken(int offset = 1);
    protected abstract void ReportUnexpected(SyntaxToken token, string expected);
    protected abstract void ReportConsecutiveTypeNegation();

    protected virtual int? MemberSeparatorStart => null;
    protected virtual void PrepareTypePattern() { }
    protected virtual void ValidateNegatedRefType() { }

    protected TypePatternSyntax ParseTypePattern(int parentPrecedence = 0, bool taskLikeIsName = false) =>
        WorkStack.Run(ParseTypePatternWork(parentPrecedence, taskLikeIsName), Context.CancellationToken);

    protected bool IsIdentifier(string text) => Current.Kind == SyntaxKind.IdentifierToken && Current.Text == text;

    // 选择器在读取标识符、泛型前预留节点；独立入口保留原有的另一种收费顺序。
    protected virtual VariableTypePatternSyntax ParseVariable()
    {
        var dollar = NextToken();
        return Context.Create(() => new VariableTypePatternSyntax(dollar, MatchToken(SyntaxKind.IdentifierToken)));
    }

    protected virtual async StackWork<NamePatternSegmentSyntax> CreateNameSegmentWork(List<SyntaxToken> parts) =>
        await Context.CreateWork(async () => new NamePatternSegmentSyntax(parts, await ParseOptionalGenericArgumentsWork(parts)));

    protected async StackWork<GenericPatternListSyntax?> ParseOptionalGenericArgumentsWork(List<SyntaxToken> parts) =>
        Current.Kind == SyntaxKind.LessThanToken && Current.Span.Start == parts[parts.Count - 1].Span.End
            ? await ParseGenericPatternListWork() : null;

    // 使用优先级爬升解析类型表达式，以保持一元、二元和后缀构造的结合顺序。
    protected async StackWork<TypePatternSyntax> ParseTypePatternWork(int parentPrecedence = 0, bool taskLikeIsName = false)
    {
        PrepareTypePattern();
        using var nesting = DepthGuard.Enter(Current.Span);
        TypePatternSyntax left;
        var unaryPrecedence = GetUnaryPrecedence(Current.Kind);
        if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence)
        {
            ValidateNegatedRefType();
            var operatorToken = NextToken();
            ReportConsecutiveTypeNegation();
            left = await Context.CreateWork(async () => new UnaryTypePatternSyntax(
                operatorToken,
                await ParseTypePatternWork(unaryPrecedence, taskLikeIsName)));
        }
        else
        {
            left = await ParseTypePrimaryWithSuffixesWork(taskLikeIsName);
        }

        while (true)
        {
            var precedence = GetBinaryPrecedence(Current.Kind);
            if (precedence == 0 || precedence <= parentPrecedence) break;

            var operatorToken = NextToken();
            var right = await ParseTypePatternWork(precedence, taskLikeIsName);
            left = Context.Create(() => new BinaryTypePatternSyntax(left, operatorToken, right));
        }

        return left;
    }

    private async StackWork<TypePatternSyntax> ParseTypePrimaryWithSuffixesWork(bool taskLikeIsName)
    {
        var pattern = await ParseTypeAtomWork(taskLikeIsName);
        while (true)
        {
            switch (Current.Kind)
            {
                case SyntaxKind.OpenBracketToken:
                    // 类型累积遇到非相邻 token 即截断：
                    // int [] / int ? 报错；数组括号内部的空格不受影响。
                    if (Current.Span.Start != pattern.FullSpan.End)
                    {
                        ReportUnexpected(Current, "'[' (must be adjacent to the type pattern)");
                    }

                    pattern = ParseArraySuffix(pattern);
                    continue;
                case SyntaxKind.QuestionToken:
                    if (Current.Span.Start != pattern.FullSpan.End)
                    {
                        ReportUnexpected(Current, "'?' (must be adjacent to the type pattern)");
                    }

                    pattern = Context.Create(() => new NullableTypePatternSyntax(pattern, NextToken()));
                    continue;
                case SyntaxKind.PlusToken:
                    if (Current.Span.Start != pattern.FullSpan.End)
                    {
                        ReportUnexpected(Current, "'+' (must be adjacent to the type pattern)");
                    }

                    pattern = Context.Create(() => new AssignableTypePatternSyntax(pattern, NextToken()));
                    continue;
                default:
                    return pattern;
            }
        }
    }

    private async StackWork<TypePatternSyntax> ParseTypeAtomWork(bool taskLikeIsName)
    {
        if (Current.Kind == SyntaxKind.StarToken)
        {
            var star = NextToken();
            // 名字延续（标识符、泛型子句、分隔符）必须与 * 相邻；跨空格即槽位边界（§3.4）。
            if (Current.Span.Start == star.Span.End &&
                Current.Span.Start != MemberSeparatorStart &&
                (TypePatternSyntaxFacts.IsNamePart(Current) ||
                 Current.Kind is
                     SyntaxKind.LessThanToken or
                     SyntaxKind.DotToken or
                     SyntaxKind.DotDotToken or
                     SyntaxKind.SlashToken))
            {
                return await ParseNamedTypeWork(star);
            }

            return Context.Create(() => new WildcardTypePatternSyntax(star));
        }

        if (Current.Kind == SyntaxKind.DollarToken) return ParseVariable();

        if (Current.Kind == SyntaxKind.OpenParenToken)
        {
            return await ParseParenthesizedOrTupleTypeWork(taskLikeIsName);
        }

        if (!TypePatternSyntaxFacts.IsNamePart(Current))
        {
            var position = Current.Span.Start;
            ReportUnexpected(Current, "type pattern");
            if (Current.Kind != SyntaxKind.EndOfFileToken) NextToken();
            return CreateMissingType(position);
        }

        var first = NextToken();
        if (first.Text == "tasklike" && !taskLikeIsName)
        {
            if (Current.Kind != SyntaxKind.LessThanToken)
            {
                return Context.Create(() => new TaskLikeTypePatternSyntax(first, null, null, null));
            }

            if (Current.Span.Start != first.Span.End)
            {
                ReportUnexpected(Current, "'<' (must be adjacent to 'tasklike')");
            }
            var lessThan = NextToken();
            TypePatternSyntax result;
            if (Current.Kind == SyntaxKind.GreaterThanToken)
            {
                ReportUnexpected(Current, "task-like result type");
                result = CreateMissingType(Current.Span.Start);
            }
            else result = await ParseTypePatternWork();
            return Context.Create(() => new TaskLikeTypePatternSyntax(
                first,
                lessThan,
                result,
                MatchToken(SyntaxKind.GreaterThanToken)));
        }

        return await ParseNamedTypeWork(first);
    }

    private async StackWork<NamedTypePatternSyntax> ParseNamedTypeWork(SyntaxToken first)
    {
        var segments = new List<NamePatternSegmentSyntax>
        {
            await ParseNameSegmentWork(first)
        };
        var separators = new List<SyntaxToken>();
        var inNestedType = false;
        while (Current.Kind is
               SyntaxKind.DotToken or
               SyntaxKind.DotDotToken or
               SyntaxKind.SlashToken)
        {
            // 分隔符必须与前一段名字相邻（§3.4）：跨空格即槽位边界。
            // 不消费该分隔符，留给成员路径诊断与恢复。
            if (Current.Span.Start != segments[segments.Count - 1].FullSpan.End)
            {
                break;
            }

            if (Current.Span.Start == MemberSeparatorStart) break;

            var separator = NextToken();
            if (inNestedType && separator.Kind != SyntaxKind.SlashToken)
                ReportUnexpected(separator, "'/' between nested types");
            if (separator.Kind == SyntaxKind.SlashToken) inNestedType = true;
            separators.Add(separator);
            if (TypePatternSyntaxFacts.IsNamePart(Current) &&
                Current.Span.Start != separator.Span.End)
            {
                // 分隔符后的名字也必须相邻；诊断后继续恢复解析，保持段/分隔符计数成对。
                ReportUnexpected(
                    Current,
                    MemberSeparatorStart is null
                        ? "name pattern (must follow the separator without whitespace)"
                        : "member path separator (names must follow without whitespace)");
            }

            segments.Add(await ParseNameSegmentWork());
        }

        return Context.Create(() => new NamedTypePatternSyntax(segments, separators));
    }

    protected async StackWork<NamePatternSegmentSyntax> ParseNameSegmentWork(SyntaxToken? first = null)
    {
        var parts = new List<SyntaxToken>();
        if (first is not null) parts.Add(first);
        while (TypePatternSyntaxFacts.IsNamePart(Current))
        {
            if (parts.Count > 0 &&
                Current.Span.Start != parts[parts.Count - 1].Span.End)
            {
                break;
            }

            parts.Add(NextToken());
        }

        if (parts.Count == 0)
        {
            ReportUnexpected(Current, "name pattern");
            parts.Add(
                Context.Create(() => SyntaxToken.Missing(
                    SyntaxKind.IdentifierToken,
                    Current.Span.Start)));
        }

        return await CreateNameSegmentWork(parts);
    }

    private async StackWork<GenericPatternListSyntax> ParseGenericPatternListWork()
    {
        var lessThan = NextToken();

        // <!>：显式零泛型。
        if (Current.Kind == SyntaxKind.BangToken &&
            PeekNextToken().Kind == SyntaxKind.GreaterThanToken)
        {
            var bang = NextToken();
            return Context.Create(() => new GenericPatternListSyntax(
                lessThan,
                GenericPatternForm.FixedArity,
                0,
                Array.Empty<TypePatternSyntax>(),
                Array.Empty<SyntaxToken>(),
                bang,
                MatchToken(SyntaxKind.GreaterThanToken)));
        }

        if (Current.Kind == SyntaxKind.GreaterThanToken)
        {
            // <> = 单个 Any 空槽。
            return Context.Create(() => new GenericPatternListSyntax(
                lessThan,
                GenericPatternForm.FixedArity,
                1,
                Array.Empty<TypePatternSyntax>(),
                Array.Empty<SyntaxToken>(),
                null,
                NextToken()));
        }

        if (Current.Kind == SyntaxKind.DotDotToken)
        {
            // <..> = 至少一个泛型参数。
            var wildcard = NextToken();
            return Context.Create(() => new GenericPatternListSyntax(
                lessThan,
                GenericPatternForm.AnyPositiveArity,
                0,
                Array.Empty<TypePatternSyntax>(),
                Array.Empty<SyntaxToken>(),
                wildcard,
                MatchToken(SyntaxKind.GreaterThanToken)));
        }

        // <?> = 任意 arity（含零）。
        if (Current.Kind == SyntaxKind.QuestionToken)
        {
            var question = NextToken();
            return Context.Create(() => new GenericPatternListSyntax(
                lessThan,
                GenericPatternForm.AnyArity,
                0,
                Array.Empty<TypePatternSyntax>(),
                Array.Empty<SyntaxToken>(),
                question,
                MatchToken(SyntaxKind.GreaterThanToken)));
        }

        // 混合空槽：空槽 = Any 占位，arity = 槽数；
        // 全空槽（<,>、<,,>）等价于纯逗号计数形式。
        return await ParseGenericArgumentsWork(lessThan);
    }

    private async StackWork<GenericPatternListSyntax> ParseGenericArgumentsWork(SyntaxToken lessThan)
    {
        var arguments = new List<TypePatternSyntax>();
        var separators = new List<SyntaxToken>();
        var hasTypedArgument = false;
        while (true)
        {
            if (Current.Kind is SyntaxKind.CommaToken or SyntaxKind.GreaterThanToken)
            {
                arguments.Add(CreateMissingType(Current.Span.Start));
            }
            else
            {
                arguments.Add(await ParseTypePatternWork());
                hasTypedArgument = true;
            }

            if (Current.Kind != SyntaxKind.CommaToken) break;
            separators.Add(NextToken());
        }

        return Context.Create(() => new GenericPatternListSyntax(
            lessThan,
            hasTypedArgument ? GenericPatternForm.Arguments : GenericPatternForm.FixedArity,
            arguments.Count,
            hasTypedArgument ? arguments : Array.Empty<TypePatternSyntax>(),
            separators,
            null,
            MatchToken(SyntaxKind.GreaterThanToken)));
    }

    private async StackWork<TypePatternSyntax> ParseParenthesizedOrTupleTypeWork(bool taskLikeIsName)
    {
        var openParen = NextToken();
        if (Current.Kind == SyntaxKind.CloseParenToken)
        {
            ReportUnexpected(Current, "type pattern");
            return Context.Create(() => new ParenthesizedTypePatternSyntax(openParen,
                CreateMissingType(Current.Span.Start), NextToken()));
        }
        var first = await ParseTypePatternWork(taskLikeIsName: taskLikeIsName);
        if (Current.Kind != SyntaxKind.CommaToken)
        {
            // 括号类型只保留 Boolean 分组含义；单元素括号按缺元素诊断。
            if (!TypePatternSyntaxFacts.IsBooleanGroup(first))
            {
                ReportUnexpected(Current, "',' and another tuple element (a tuple needs at least two elements)");
            }

            return Context.Create(() => new ParenthesizedTypePatternSyntax(
                openParen,
                first,
                MatchToken(SyntaxKind.CloseParenToken)));
        }

        var elements = new List<TypePatternSyntax> { first };
        var separators = new List<SyntaxToken>();
        while (Current.Kind == SyntaxKind.CommaToken)
        {
            separators.Add(NextToken());
            if (Current.Kind == SyntaxKind.CloseParenToken)
            {
                ReportUnexpected(Current, "tuple element");
                elements.Add(CreateMissingType(Current.Span.Start));
                break;
            }
            elements.Add(await ParseTypePatternWork(taskLikeIsName: taskLikeIsName));
        }

        return Context.Create(() => new TupleTypePatternSyntax(
            openParen,
            elements,
            separators,
            MatchToken(SyntaxKind.CloseParenToken)));
    }

    private ArrayTypePatternSyntax ParseArraySuffix(TypePatternSyntax elementType)
    {
        var openBracket = NextToken();
        var commas = new List<SyntaxToken>();
        while (Current.Kind == SyntaxKind.CommaToken)
        {
            commas.Add(NextToken());
        }

        return Context.Create(() => new ArrayTypePatternSyntax(
            elementType,
            openBracket,
            commas,
            MatchToken(SyntaxKind.CloseBracketToken)));
    }

    protected WildcardTypePatternSyntax CreateMissingType(int position)
    {
        return Context.Create(() => new WildcardTypePatternSyntax(
            Context.Create(() => SyntaxToken.Missing(SyntaxKind.StarToken, position))));
    }

    protected static int GetUnaryPrecedence(SyntaxKind kind)
    {
        return kind == SyntaxKind.BangToken ? 3 : 0;
    }

    protected static int GetBinaryPrecedence(SyntaxKind kind)
    {
        return kind switch
        {
            SyntaxKind.AmpersandAmpersandToken => 2,
            SyntaxKind.PipePipeToken => 1,
            _ => 0
        };
    }

}
