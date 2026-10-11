using AspectN.Diagnostics;
using AspectN.Syntax;
using AspectN.Text;

namespace AspectN.Parsing;

internal sealed class TypePatternParser
{
    private readonly DiagnosticBag _diagnostics = new();
    private readonly Lexer _lexer;
    private readonly SourceText _source;
    private SyntaxToken _current;
    private readonly ParserDepthGuard _depthGuard = new();

    public TypePatternParser(SourceText source)
    {
        _source = source;
        _lexer = new Lexer(source);
        _current = _lexer.Lex();
    }

    private SyntaxToken Current => _current;

    public AspectNSyntaxTree Parse()
    {
        SyntaxNode root;
        try
        {
            root = ParseTypeUseOrPattern();
        }
        catch (ParserDepthException exception)
        {
            _diagnostics.Add(DiagnosticCodes.InputDepthExceeded, DiagnosticSeverity.Error,
                "Parser recursion exceeds the supported depth of 128 levels.", exception.Span);
            root = CreateMissingType(exception.Span.Start);
            while (Current.Kind != SyntaxKind.EndOfFileToken) NextToken();
        }
        while (Current.Kind != SyntaxKind.EndOfFileToken)
        {
            ReportUnexpected(Current, "end of input");
            NextToken();
        }

        return new AspectNSyntaxTree(
            _source,
            AspectNParseKind.TypePattern,
            root,
            GetDiagnostics());
    }

    private SyntaxNode ParseTypeUseOrPattern()
    {
        var negatedRef = IsNegatedRefPrefix();
        if (!negatedRef && !IsIdentifier("ref") &&
            !IsIdentifier("out") &&
            !IsIdentifier("in"))
        {
            return ParseTypeExpression();
        }

        var refKindTokens = new List<SyntaxToken>();
        if (negatedRef) refKindTokens.Add(NextToken());
        refKindTokens.Add(NextToken());
        if (refKindTokens[0].Text == "ref" && IsIdentifier("readonly"))
        {
            refKindTokens.Add(NextToken());
        }
        _diagnostics.Add(
            DiagnosticCodes.InvalidRefKind,
            DiagnosticSeverity.Error,
            "Ref-kind annotations require a member parameter or return type use; the type-pattern entry point accepts types only.",
            refKindTokens[0].Span);

        return new TypeUseSyntax(refKindTokens, ParseTypeExpression());
    }

    private bool IsNegatedRefPrefix()
    {
        if (Current.Kind != SyntaxKind.BangToken) return false;
        var next = PeekNextToken();
        return next.Kind == SyntaxKind.IdentifierToken && next.Text == "ref" && next.Span.Start == Current.Span.End;
    }

    private TypePatternSyntax ParseTypeExpression(int parentPrecedence = 0)
    {
        ReportNestedRefKindPrefix();
        using var nesting = _depthGuard.Enter(Current.Span);
        TypePatternSyntax left;
        var unaryPrecedence = GetUnaryPrecedence(Current.Kind);
        if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence)
        {
            if (IsNegatedRefPrefix())
            {
                _diagnostics.Add(DiagnosticCodes.InvalidRefKind, DiagnosticSeverity.Error,
                    "Ref-kind annotations require a member parameter or return type use.", Current.Span);
            }
            var operatorToken = NextToken();
            if (Current.Kind == SyntaxKind.BangToken)
            {
                _diagnostics.Add(
                    DiagnosticCodes.UnexpectedToken,
                    DiagnosticSeverity.Error,
                    "Consecutive '!' operators are not allowed; remove the extra negation.",
                    Current.Span);
            }
            left = new UnaryTypePatternSyntax(
                operatorToken,
                ParseTypeExpression(unaryPrecedence));
        }
        else
        {
            left = ParseTypePrimaryWithSuffixes();
        }

        while (true)
        {
            var precedence = GetBinaryPrecedence(Current.Kind);
            if (precedence == 0 || precedence <= parentPrecedence) break;

            var operatorToken = NextToken();
            var right = ParseTypeExpression(precedence);
            left = new BinaryTypePatternSyntax(left, operatorToken, right);
        }

        return left;
    }

    private void ReportNestedRefKindPrefix()
    {
        if (!IsIdentifier("ref") &&
            !IsIdentifier("out") &&
            !IsIdentifier("in"))
        {
            return;
        }

        var next = PeekNextToken();
        var hasReadonly = IsIdentifier("ref") &&
            next.Kind == SyntaxKind.IdentifierToken &&
            next.Text == "readonly";
        var typeStart = hasReadonly ? PeekNextToken(2) : next;
        if (!CanStartTypePattern(typeStart)) return;

        var refKind = NextToken();
        if (hasReadonly) NextToken();
        _diagnostics.Add(
            DiagnosticCodes.InvalidRefKind,
            DiagnosticSeverity.Error,
            "Ref-kind annotations require a member parameter or return type use.",
            refKind.Span);
    }

    private static bool CanStartTypePattern(SyntaxToken token)
    {
        return token.Kind is
                   SyntaxKind.DollarToken or
                   SyntaxKind.OpenParenToken or
                   SyntaxKind.StarToken or
                   SyntaxKind.BangToken ||
               TypePatternSyntaxFacts.IsNamePart(token);
    }

    private TypePatternSyntax ParseTypePrimaryWithSuffixes()
    {
        var pattern = ParseTypeAtom();
        while (true)
        {
            switch (Current.Kind)
            {
                case SyntaxKind.OpenBracketToken:
                    // 类型累积遇到非相邻 token 即截断。
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

                    pattern = new NullableTypePatternSyntax(pattern, NextToken());
                    continue;
                case SyntaxKind.PlusToken:
                    if (Current.Span.Start != pattern.FullSpan.End)
                    {
                        ReportUnexpected(Current, "'+' (must be adjacent to the type pattern)");
                    }

                    pattern = new AssignableTypePatternSyntax(pattern, NextToken());
                    continue;
                default:
                    return pattern;
            }
        }
    }

    private TypePatternSyntax ParseTypeAtom()
    {
        if (Current.Kind == SyntaxKind.DollarToken) return ParseVariable();
        if (Current.Kind == SyntaxKind.OpenParenToken) return ParseParenthesizedOrTuple();
        if (Current.Kind == SyntaxKind.StarToken) return ParseStarOrNamed();

        if (TypePatternSyntaxFacts.IsNamePart(Current))
        {
            var first = NextToken();
            if (first.Text == "tasklike")
            {
                return ParseTaskLike(first);
            }

            return ParseNamed(first);
        }

        var unexpected = Current;
        ReportUnexpected(unexpected, "type pattern");
        if (Current.Kind != SyntaxKind.EndOfFileToken) NextToken();
        return CreateMissingType(unexpected.Span.Start);
    }

    private VariableTypePatternSyntax ParseVariable()
    {
        var dollar = NextToken();
        var identifier = MatchToken(SyntaxKind.IdentifierToken);
        return new VariableTypePatternSyntax(dollar, identifier);
    }

    private TypePatternSyntax ParseStarOrNamed()
    {
        var star = NextToken();
        // 名字延续（标识符、泛型子句、分隔符）必须与 * 相邻；跨空格即槽位边界（§3.4）。
        if (Current.Span.Start == star.Span.End &&
            (TypePatternSyntaxFacts.IsNamePart(Current) ||
             Current.Kind is
                 SyntaxKind.LessThanToken or
                 SyntaxKind.DotToken or
                 SyntaxKind.DotDotToken or
                 SyntaxKind.SlashToken))
        {
            return ParseNamed(star);
        }

        return new WildcardTypePatternSyntax(star);
    }

    private NamedTypePatternSyntax ParseNamed(SyntaxToken firstNamePart)
    {
        var segments = new List<NamePatternSegmentSyntax>
        {
            ParseNameSegment(firstNamePart)
        };
        var separators = new List<SyntaxToken>();
        var inNestedType = false;

        while (Current.Kind is
               SyntaxKind.DotToken or
               SyntaxKind.DotDotToken or
               SyntaxKind.SlashToken)
        {
            // 分隔符必须与前一段名字相邻（§3.4）：跨空格即槽位边界，截断留给下游诊断。
            if (Current.Span.Start != segments[segments.Count - 1].FullSpan.End)
            {
                break;
            }

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
                    "name pattern (must follow the separator without whitespace)");
            }

            segments.Add(ParseNameSegment());
        }

        return new NamedTypePatternSyntax(segments, separators);
    }

    private NamePatternSegmentSyntax ParseNameSegment(
        SyntaxToken? firstNamePart = null)
    {
        var nameParts = new List<SyntaxToken>();
        if (firstNamePart is not null) nameParts.Add(firstNamePart);

        while (TypePatternSyntaxFacts.IsNamePart(Current))
        {
            if (nameParts.Count > 0 &&
                Current.Span.Start != nameParts[nameParts.Count - 1].Span.End)
            {
                break;
            }

            nameParts.Add(NextToken());
        }

        if (nameParts.Count == 0)
        {
            ReportUnexpected(Current, "name pattern");
            nameParts.Add(
                SyntaxToken.Missing(
                    SyntaxKind.IdentifierToken,
                    Current.Span.Start));
        }

        var genericArguments = Current.Kind == SyntaxKind.LessThanToken &&
            Current.Span.Start == nameParts[nameParts.Count - 1].Span.End
            ? ParseGenericPatternList()
            : null;
        return new NamePatternSegmentSyntax(nameParts, genericArguments);
    }

    private GenericPatternListSyntax ParseGenericPatternList()
    {
        var lessThan = NextToken();

        // <!>：显式零泛型。
        if (Current.Kind == SyntaxKind.BangToken && PeekNextToken().Kind == SyntaxKind.GreaterThanToken)
        {
            var bang = NextToken();
            return new GenericPatternListSyntax(
                lessThan,
                GenericPatternForm.FixedArity,
                0,
                Array.Empty<TypePatternSyntax>(),
                Array.Empty<SyntaxToken>(),
                bang,
                MatchToken(SyntaxKind.GreaterThanToken));
        }

        if (Current.Kind == SyntaxKind.GreaterThanToken)
        {
            // <> = 单个 Any 空槽。
            return new GenericPatternListSyntax(
                lessThan,
                GenericPatternForm.FixedArity,
                1,
                Array.Empty<TypePatternSyntax>(),
                Array.Empty<SyntaxToken>(),
                null,
                NextToken());
        }

        if (Current.Kind == SyntaxKind.DotDotToken)
        {
            // <..> = 至少一个泛型参数。
            var wildcard = NextToken();
            return new GenericPatternListSyntax(
                lessThan,
                GenericPatternForm.AnyPositiveArity,
                0,
                Array.Empty<TypePatternSyntax>(),
                Array.Empty<SyntaxToken>(),
                wildcard,
                MatchToken(SyntaxKind.GreaterThanToken));
        }

        // <?> = 任意 arity（含零）。
        if (Current.Kind == SyntaxKind.QuestionToken)
        {
            var question = NextToken();
            return new GenericPatternListSyntax(
                lessThan,
                GenericPatternForm.AnyArity,
                0,
                Array.Empty<TypePatternSyntax>(),
                Array.Empty<SyntaxToken>(),
                question,
                MatchToken(SyntaxKind.GreaterThanToken));
        }

        // 混合空槽：空槽 = Any 占位，arity = 槽数。
        return ParseGenericArguments(lessThan);
    }

    private GenericPatternListSyntax ParseGenericArguments(SyntaxToken lessThan)
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
                arguments.Add(ParseTypeExpression());
                hasTypedArgument = true;
            }

            if (Current.Kind != SyntaxKind.CommaToken) break;
            separators.Add(NextToken());
        }

        return new GenericPatternListSyntax(
            lessThan,
            hasTypedArgument ? GenericPatternForm.Arguments : GenericPatternForm.FixedArity,
            arguments.Count,
            hasTypedArgument ? arguments : Array.Empty<TypePatternSyntax>(),
            separators,
            null,
            MatchToken(SyntaxKind.GreaterThanToken));
    }

    private TaskLikeTypePatternSyntax ParseTaskLike(SyntaxToken keyword)
    {
        // 裸 tasklike（仅 Standard）：匹配非泛型 Task/ValueTask。
        if (Current.Kind != SyntaxKind.LessThanToken)
        {
            return new TaskLikeTypePatternSyntax(keyword, null, null, null);
        }

        if (Current.Span.Start != keyword.Span.End)
        {
            ReportUnexpected(Current, "'<' (must be adjacent to 'tasklike')");
        }
        var lessThan = NextToken();
        TypePatternSyntax resultType;
        if (Current.Kind == SyntaxKind.GreaterThanToken)
        {
            ReportUnexpected(Current, "task-like result type");
            resultType = CreateMissingType(Current.Span.Start);
        }
        else
        {
            resultType = ParseTypeExpression();
        }

        return new TaskLikeTypePatternSyntax(
            keyword,
            lessThan,
            resultType,
            MatchToken(SyntaxKind.GreaterThanToken));
    }

    private TypePatternSyntax ParseParenthesizedOrTuple()
    {
        var openParen = NextToken();
        if (Current.Kind == SyntaxKind.CloseParenToken)
        {
            ReportUnexpected(Current, "type pattern");
            return new ParenthesizedTypePatternSyntax(
                openParen,
                CreateMissingType(Current.Span.Start),
                NextToken());
        }

        var first = ParseTypeExpression();
        if (Current.Kind != SyntaxKind.CommaToken)
        {
            // 括号类型只保留 Boolean 分组含义；单元素括号按缺元素诊断。
            if (!TypePatternSyntaxFacts.IsBooleanGroup(first))
            {
                ReportUnexpected(Current, "',' and another tuple element (a tuple needs at least two elements)");
            }

            return new ParenthesizedTypePatternSyntax(
                openParen,
                first,
                MatchToken(SyntaxKind.CloseParenToken));
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

            elements.Add(ParseTypeExpression());
        }

        return new TupleTypePatternSyntax(
            openParen,
            elements,
            separators,
            MatchToken(SyntaxKind.CloseParenToken));
    }

    private ArrayTypePatternSyntax ParseArraySuffix(TypePatternSyntax elementType)
    {
        var openBracket = NextToken();
        var commas = new List<SyntaxToken>();
        while (Current.Kind == SyntaxKind.CommaToken)
        {
            commas.Add(NextToken());
        }

        return new ArrayTypePatternSyntax(
            elementType,
            openBracket,
            commas,
            MatchToken(SyntaxKind.CloseBracketToken));
    }

    private SyntaxToken MatchToken(SyntaxKind expectedKind)
    {
        if (Current.Kind == expectedKind) return NextToken();

        ReportUnexpected(Current, expectedKind.ToString());
        return SyntaxToken.Missing(expectedKind, Current.Span.Start);
    }

    private SyntaxToken NextToken()
    {
        var current = _current;
        _current = _lexer.Lex();
        return current;
    }

    private bool IsIdentifier(string text)
    {
        return Current.Kind == SyntaxKind.IdentifierToken &&
               Current.Text == text;
    }

    private void ReportUnexpected(SyntaxToken token, string expected)
    {
        _diagnostics.Add(
            DiagnosticCodes.UnexpectedToken,
            DiagnosticSeverity.Error,
            $"Expected {expected}, found {token.Kind}.",
            token.Span);
    }

    private AspectNDiagnostic[] GetDiagnostics()
    {
        return _diagnostics
            .ToArray()
            .Concat(_lexer.Diagnostics)
            .OrderBy(diagnostic => diagnostic.Span?.Start ?? int.MaxValue)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ToArray();
    }

    private static WildcardTypePatternSyntax CreateMissingType(int position)
    {
        return new WildcardTypePatternSyntax(
            SyntaxToken.Missing(SyntaxKind.StarToken, position));
    }

    private SyntaxToken PeekNextToken(int offset = 1)
    {
        var lexer = new Lexer(_source);
        lexer.Reset(Current.Span.End);
        var token = lexer.Lex();
        for (var index = 1; index < offset; index++) token = lexer.Lex();
        return token;
    }

    private static int GetUnaryPrecedence(SyntaxKind kind)
    {
        return kind == SyntaxKind.BangToken ? 3 : 0;
    }

    private static int GetBinaryPrecedence(SyntaxKind kind)
    {
        return kind switch
        {
            SyntaxKind.AmpersandAmpersandToken => 2,
            SyntaxKind.PipePipeToken => 1,
            _ => 0
        };
    }
}
