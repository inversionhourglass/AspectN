using AspectN.Diagnostics;
using AspectN.Syntax;
using AspectN.Text;

namespace AspectN.Parsing;

internal sealed class TypePatternParser
{
    private readonly DiagnosticBag _diagnostics = new();
    private readonly Lexer _lexer;
    private readonly AspectNParseOptions _options;
    private readonly SourceText _source;
    private SyntaxToken _current;

    public TypePatternParser(SourceText source, AspectNParseOptions options)
    {
        _source = source;
        _options = options;
        _lexer = new Lexer(source);
        _current = _lexer.Lex();
    }

    private SyntaxToken Current => _current;

    public AspectNSyntaxTree Parse()
    {
        SyntaxNode root = ParseTypeUseOrPattern();
        while (Current.Kind != SyntaxKind.EndOfFileToken)
        {
            ReportUnexpected(Current, "end of input");
            NextToken();
        }

        return new AspectNSyntaxTree(
            _source,
            AspectNParseKind.TypePattern,
            root,
            GetDiagnostics(),
            _options.LanguageVersion);
    }

    private SyntaxNode ParseTypeUseOrPattern()
    {
        if (!IsIdentifier("ref") &&
            !IsIdentifier("out") &&
            !IsIdentifier("in"))
        {
            return ParseTypeExpression();
        }

        var refKindTokens = new List<SyntaxToken> { NextToken() };
        if (refKindTokens[0].Text == "ref" && IsIdentifier("readonly"))
        {
            refKindTokens.Add(NextToken());
        }
        else if (refKindTokens[0].Text is "out" or "in")
        {
            _diagnostics.Add(
                DiagnosticCodes.UnexpectedToken,
                DiagnosticSeverity.Error,
                "Only ref and ref readonly are valid at the type-pattern entry point.",
                refKindTokens[0].Span);
        }

        return new TypeUseSyntax(refKindTokens, ParseTypeExpression());
    }

    private TypePatternSyntax ParseTypeExpression(int parentPrecedence = 0)
    {
        TypePatternSyntax left;
        var unaryPrecedence = GetUnaryPrecedence(Current.Kind);
        if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence)
        {
            var operatorToken = NextToken();
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

    private TypePatternSyntax ParseTypePrimaryWithSuffixes()
    {
        var pattern = ParseTypeAtom();
        while (true)
        {
            switch (Current.Kind)
            {
                case SyntaxKind.OpenBracketToken:
                    pattern = ParseArraySuffix(pattern);
                    continue;
                case SyntaxKind.QuestionToken:
                    pattern = new NullableTypePatternSyntax(pattern, NextToken());
                    continue;
                case SyntaxKind.PlusToken:
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

        if (Current.Kind == SyntaxKind.IdentifierToken)
        {
            var first = NextToken();
            if (first.Text == "tasklike" &&
                (_options.LanguageVersion == AspectNLanguageVersion.Standard ||
                 Current.Kind == SyntaxKind.LessThanToken))
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
        if ((Current.Kind == SyntaxKind.IdentifierToken &&
             Current.Span.Start == star.Span.End) ||
            Current.Kind is
                SyntaxKind.LessThanToken or
                SyntaxKind.DotToken or
                SyntaxKind.DotDotToken or
                SyntaxKind.SlashToken)
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

        while (Current.Kind is
               SyntaxKind.DotToken or
               SyntaxKind.DotDotToken or
               SyntaxKind.SlashToken)
        {
            separators.Add(NextToken());
            segments.Add(ParseNameSegment());
        }

        return new NamedTypePatternSyntax(segments, separators);
    }

    private NamePatternSegmentSyntax ParseNameSegment(
        SyntaxToken? firstNamePart = null)
    {
        var nameParts = new List<SyntaxToken>();
        if (firstNamePart is not null) nameParts.Add(firstNamePart);

        while (Current.Kind is
               SyntaxKind.IdentifierToken or
               SyntaxKind.StarToken)
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

        var genericArguments = Current.Kind == SyntaxKind.LessThanToken
            ? ParseGenericPatternList()
            : null;
        return new NamePatternSegmentSyntax(nameParts, genericArguments);
    }

    private GenericPatternListSyntax ParseGenericPatternList()
    {
        var lessThan = NextToken();
        if (_options.LanguageVersion == AspectNLanguageVersion.Legacy &&
            Current.Kind == SyntaxKind.BangToken)
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

        if (Current.Kind == SyntaxKind.CommaToken)
        {
            var commas = new List<SyntaxToken>();
            while (Current.Kind == SyntaxKind.CommaToken)
            {
                commas.Add(NextToken());
            }

            return new GenericPatternListSyntax(
                lessThan,
                GenericPatternForm.FixedArity,
                commas.Count + 1,
                Array.Empty<TypePatternSyntax>(),
                commas,
                null,
                MatchToken(SyntaxKind.GreaterThanToken));
        }

        var arguments = new List<TypePatternSyntax>
        {
            ParseTypeExpression()
        };
        var separators = new List<SyntaxToken>();
        while (Current.Kind == SyntaxKind.CommaToken)
        {
            separators.Add(NextToken());
            if (Current.Kind == SyntaxKind.GreaterThanToken)
            {
                ReportUnexpected(Current, "type pattern after ','");
                break;
            }

            arguments.Add(ParseTypeExpression());
        }

        return new GenericPatternListSyntax(
            lessThan,
            GenericPatternForm.Arguments,
            arguments.Count,
            arguments,
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
