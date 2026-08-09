using AspectN.Diagnostics;
using AspectN.Syntax;

namespace AspectN.Parsing;

internal sealed class SelectorParser
{
    private readonly Parser _parser;

    public SelectorParser(Parser parser)
    {
        _parser = parser;
    }

    private SyntaxToken Current => _parser.SelectorCurrent;

    private AspectNLanguageVersion LanguageVersion =>
        _parser.SelectorOptions.LanguageVersion;

    public SelectorSyntax ParseSelector()
    {
        var nameTokens = ParseQualifiedName();
        if (nameTokens.Count != 1)
        {
            return ParseExtension(nameTokens);
        }

        var keyword = nameTokens[0];
        var family = GetSelectorFamily(keyword.Text);
        return family switch
        {
            SelectorFamily.Callable => ParseCallable(keyword, family),
            SelectorFamily.Method => ParseCallable(keyword, family),
            SelectorFamily.Accessor => ParseProperty(keyword, family),
            SelectorFamily.Getter => ParseProperty(keyword, family),
            SelectorFamily.Setter => ParseProperty(keyword, family),
            SelectorFamily.Constructor => ParseConstructor(keyword),
            SelectorFamily.StaticConstructor => ParseStaticConstructor(keyword),
            SelectorFamily.Attribute => ParseAttribute(keyword),
            SelectorFamily.Regex => ParseRegex(keyword),
            _ => ParseExtension(nameTokens)
        };
    }

    private List<SyntaxToken> ParseQualifiedName()
    {
        var tokens = new List<SyntaxToken> { NextToken() };
        while (Current.Kind == SyntaxKind.DotToken)
        {
            tokens.Add(NextToken());
            if (Current.Kind != SyntaxKind.IdentifierToken)
            {
                ReportUnexpected(Current, "extension name segment");
                break;
            }

            tokens.Add(NextToken());
        }

        return tokens;
    }

    private SelectorSyntax ParseCallable(
        SyntaxToken keyword,
        SelectorFamily family)
    {
        AddMigrationDiagnosticIfNeeded(keyword);
        var openParen = MatchToken(SyntaxKind.OpenParenToken);
        var signature = ParseCallableSignature();
        var closeParen = MatchToken(SyntaxKind.CloseParenToken);
        return family == SelectorFamily.Callable
            ? new CallableSelectorSyntax(
                keyword,
                openParen,
                signature,
                closeParen)
            : new MethodSelectorSyntax(
                keyword,
                openParen,
                signature,
                closeParen);
    }

    private SelectorSyntax ParseProperty(
        SyntaxToken keyword,
        SelectorFamily family)
    {
        AddMigrationDiagnosticIfNeeded(keyword);
        var openParen = MatchToken(SyntaxKind.OpenParenToken);
        var signature = ParsePropertySignature();
        var closeParen = MatchToken(SyntaxKind.CloseParenToken);
        return family switch
        {
            SelectorFamily.Accessor => new AccessorSelectorSyntax(
                keyword,
                openParen,
                signature,
                closeParen),
            SelectorFamily.Getter => new GetterSelectorSyntax(
                keyword,
                openParen,
                signature,
                closeParen),
            _ => new SetterSelectorSyntax(
                keyword,
                openParen,
                signature,
                closeParen)
        };
    }

    private ConstructorSelectorSyntax ParseConstructor(SyntaxToken keyword)
    {
        var openParen = MatchToken(SyntaxKind.OpenParenToken);
        var parts = new List<SyntaxNode> { ParseTypePattern() };
        parts.AddRange(ParseParameterPatternList());
        var signature = new ConstructorSignatureSyntax(parts);
        return new ConstructorSelectorSyntax(
            keyword,
            openParen,
            signature,
            MatchToken(SyntaxKind.CloseParenToken));
    }

    private StaticConstructorSelectorSyntax ParseStaticConstructor(
        SyntaxToken keyword)
    {
        var openParen = MatchToken(SyntaxKind.OpenParenToken);
        var typePattern = ParseTypePattern();
        return new StaticConstructorSelectorSyntax(
            keyword,
            openParen,
            typePattern,
            MatchToken(SyntaxKind.CloseParenToken));
    }

    private AttributeSelectorSyntax ParseAttribute(SyntaxToken keyword)
    {
        var openParen = MatchToken(SyntaxKind.OpenParenToken);
        var parts = new List<SyntaxNode>();
        if (Current.Kind == SyntaxKind.IdentifierToken ||
            (LanguageVersion == AspectNLanguageVersion.Legacy &&
             Current.Kind == SyntaxKind.StarToken))
        {
            var position = NextToken();
            parts.Add(position);
            if (!IsAttributePosition(position.Text))
            {
                ReportUnexpected(position, "attribute position");
            }

            if (EqualsKeyword(position.Text, "parameter"))
            {
                parts.Add(MatchToken(SyntaxKind.OpenBracketToken));
                if (Current.Kind is SyntaxKind.IntegerToken or SyntaxKind.StarToken)
                {
                    parts.Add(NextToken());
                }
                else
                {
                    ReportUnexpected(Current, "parameter index or '*'");
                }

                parts.Add(MatchToken(SyntaxKind.CloseBracketToken));
            }
            else if (LanguageVersion == AspectNLanguageVersion.Legacy &&
                     EqualsKeyword(position.Text, "para"))
            {
                if (Current.Kind is SyntaxKind.IntegerToken or SyntaxKind.StarToken)
                {
                    var index = NextToken();
                    parts.Add(index);
                    if (index.Value is long value && value < 0)
                    {
                        ReportUnexpected(index, "non-negative parameter index");
                    }
                }
                else
                {
                    ReportUnexpected(Current, "parameter index or '*'");
                }
            }
        }
        else
        {
            ReportUnexpected(Current, "attribute position");
        }

        parts.Add(ParseTypePattern());
        var signature = new AttributeSignatureSyntax(parts);
        return new AttributeSelectorSyntax(
            keyword,
            openParen,
            signature,
            MatchToken(SyntaxKind.CloseParenToken));
    }

    private RegexSelectorSyntax ParseRegex(SyntaxToken keyword)
    {
        var openParen = _parser.ConsumeRegexOpenParen();
        var body = Current.Kind == SyntaxKind.RegexBodyToken
            ? NextToken()
            : SyntaxToken.Missing(SyntaxKind.RegexBodyToken, Current.Span.Start);
        return new RegexSelectorSyntax(
            keyword,
            openParen,
            body,
            MatchToken(SyntaxKind.CloseParenToken));
    }

    private ExtensionSelectorSyntax ParseExtension(
        IReadOnlyList<SyntaxToken> nameTokens)
    {
        var openParen = MatchToken(SyntaxKind.OpenParenToken);
        var arguments = new List<SyntaxNode>();
        var separators = new List<SyntaxToken>();
        while (Current.Kind is not
               SyntaxKind.CloseParenToken and not
               SyntaxKind.EndOfFileToken)
        {
            arguments.Add(ParseExtensionArgument());
            if (Current.Kind != SyntaxKind.CommaToken) break;
            separators.Add(NextToken());
        }

        return new ExtensionSelectorSyntax(
            nameTokens,
            openParen,
            arguments,
            separators,
            MatchToken(SyntaxKind.CloseParenToken));
    }

    private SyntaxNode ParseExtensionArgument()
    {
        if (Current.Kind is SyntaxKind.StringToken or SyntaxKind.IntegerToken)
        {
            return NextToken();
        }

        if (Current.Kind == SyntaxKind.IdentifierToken &&
            Current.Text is "true" or "false")
        {
            return NextToken();
        }

        return ParseTypePattern();
    }

    private CallableSignatureSyntax ParseCallableSignature()
    {
        var parts = new List<SyntaxNode>();
        parts.AddRange(ParseModifiers());
        if (IsIdentifier("async")) parts.Add(NextToken());
        parts.Add(ParseTypeUse());
        parts.AddRange(ParseMemberPath());
        parts.AddRange(ParseParameterPatternList());
        return new CallableSignatureSyntax(parts);
    }

    private PropertySignatureSyntax ParsePropertySignature()
    {
        var parts = new List<SyntaxNode>();
        parts.AddRange(ParseModifiers());
        parts.Add(ParseTypeUse());
        parts.AddRange(ParseMemberPath());
        return new PropertySignatureSyntax(parts);
    }

    private List<ModifierSyntax> ParseModifiers()
    {
        var modifiers = new List<ModifierSyntax>();
        while (true)
        {
            if (Current.Kind == SyntaxKind.BangToken)
            {
                var tokens = new List<SyntaxToken> { NextToken() };
                if (!IsModifier(Current.Text))
                {
                    ReportUnexpected(Current, "accessibility or static modifier");
                    modifiers.Add(new ModifierSyntax(tokens));
                    break;
                }

                tokens.Add(NextToken());
                modifiers.Add(new ModifierSyntax(tokens));
                continue;
            }

            if (Current.Kind != SyntaxKind.IdentifierToken ||
                !IsModifier(Current.Text))
            {
                break;
            }

            var modifierTokens = new List<SyntaxToken> { NextToken() };
            if (LanguageVersion == AspectNLanguageVersion.Standard &&
                ((modifierTokens[0].Text == "private" &&
                  IsIdentifier("protected")) ||
                 (modifierTokens[0].Text == "protected" &&
                  IsIdentifier("internal"))))
            {
                modifierTokens.Add(NextToken());
            }

            modifiers.Add(new ModifierSyntax(modifierTokens));
        }

        return modifiers;
    }

    private TypeUseSyntax ParseTypeUse()
    {
        var refKind = new List<SyntaxToken>();
        if (IsIdentifier("ref") || IsIdentifier("out") || IsIdentifier("in"))
        {
            refKind.Add(NextToken());
            if (EqualsKeyword(refKind[0].Text, "ref") &&
                IsIdentifier("readonly"))
            {
                refKind.Add(NextToken());
            }
        }

        return new TypeUseSyntax(refKind, ParseTypePattern());
    }

    private TypePatternSyntax ParseTypePattern(int parentPrecedence = 0)
    {
        TypePatternSyntax left;
        var unaryPrecedence = GetUnaryPrecedence(Current.Kind);
        if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence)
        {
            var operatorToken = NextToken();
            left = new UnaryTypePatternSyntax(
                operatorToken,
                ParseTypePattern(unaryPrecedence));
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
            var right = ParseTypePattern(precedence);
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
        if (Current.Kind == SyntaxKind.StarToken)
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
                return ParseNamedType(star);
            }

            return new WildcardTypePatternSyntax(star);
        }

        if (Current.Kind == SyntaxKind.DollarToken)
        {
            var dollar = NextToken();
            return new VariableTypePatternSyntax(
                dollar,
                MatchToken(SyntaxKind.IdentifierToken));
        }

        if (Current.Kind == SyntaxKind.OpenParenToken)
        {
            return ParseParenthesizedOrTupleType();
        }

        if (Current.Kind != SyntaxKind.IdentifierToken)
        {
            var position = Current.Span.Start;
            ReportUnexpected(Current, "type pattern");
            if (Current.Kind != SyntaxKind.EndOfFileToken) NextToken();
            return CreateMissingType(position);
        }

        var first = NextToken();
        if (first.Text == "tasklike" &&
            Current.Kind == SyntaxKind.LessThanToken)
        {
            var lessThan = NextToken();
            var result = ParseTypePattern();
            return new TaskLikeTypePatternSyntax(
                first,
                lessThan,
                result,
                MatchToken(SyntaxKind.GreaterThanToken));
        }

        return ParseNamedType(first);
    }

    private NamedTypePatternSyntax ParseNamedType(SyntaxToken first)
    {
        var segments = new List<NamePatternSegmentSyntax>
        {
            ParseNameSegment(first)
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

    private NamePatternSegmentSyntax ParseNameSegment(SyntaxToken? first = null)
    {
        var parts = new List<SyntaxToken>();
        if (first is not null) parts.Add(first);
        while (Current.Kind is SyntaxKind.IdentifierToken or SyntaxKind.StarToken)
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
                SyntaxToken.Missing(
                    SyntaxKind.IdentifierToken,
                    Current.Span.Start));
        }

        return new NamePatternSegmentSyntax(
            parts,
            Current.Kind == SyntaxKind.LessThanToken
                ? ParseGenericPatternList()
                : null);
    }

    private GenericPatternListSyntax ParseGenericPatternList()
    {
        var lessThan = NextToken();
        if (LanguageVersion == AspectNLanguageVersion.Legacy &&
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

        var arguments = new List<TypePatternSyntax> { ParseTypePattern() };
        var separators = new List<SyntaxToken>();
        while (Current.Kind == SyntaxKind.CommaToken)
        {
            separators.Add(NextToken());
            arguments.Add(ParseTypePattern());
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

    private TypePatternSyntax ParseParenthesizedOrTupleType()
    {
        var openParen = NextToken();
        var first = ParseTypePattern();
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
            elements.Add(ParseTypePattern());
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

    private List<SyntaxNode> ParseMemberPath()
    {
        var parts = new List<SyntaxNode>();
        var hasName = false;
        var hasGeneric = false;
        while (Current.Kind is
               SyntaxKind.IdentifierToken or
               SyntaxKind.StarToken or
               SyntaxKind.DotToken or
               SyntaxKind.DotDotToken or
               SyntaxKind.SlashToken or
               SyntaxKind.LessThanToken or
               SyntaxKind.PlusToken)
        {
            if (Current.Kind == SyntaxKind.LessThanToken)
            {
                if (!hasName || hasGeneric)
                {
                    ReportUnexpected(Current, "member name before generic pattern");
                }

                parts.Add(ParseGenericPatternList());
                hasGeneric = true;
                continue;
            }

            if (Current.Kind is
                SyntaxKind.DotToken or
                SyntaxKind.DotDotToken or
                SyntaxKind.SlashToken)
            {
                parts.Add(NextToken());
                hasName = false;
                hasGeneric = false;
                continue;
            }

            if (hasGeneric)
            {
                ReportUnexpected(Current, "member path separator");
            }

            parts.Add(NextToken());
            hasName = true;
        }

        if (parts.Count == 0) ReportUnexpected(Current, "member path");
        return parts;
    }

    private List<SyntaxNode> ParseParameterPatternList()
    {
        var parts = new List<SyntaxNode>
        {
            MatchToken(SyntaxKind.OpenParenToken)
        };
        if (Current.Kind == SyntaxKind.DotDotToken)
        {
            parts.Add(NextToken());
        }
        else if (LanguageVersion == AspectNLanguageVersion.Legacy)
        {
            ParseLegacyParameterSlots(parts);
        }
        else
        {
            while (Current.Kind is not
                   SyntaxKind.CloseParenToken and not
                   SyntaxKind.EndOfFileToken)
            {
                parts.Add(new ParameterSyntax(ParseTypeUse()));
                if (Current.Kind != SyntaxKind.CommaToken) break;
                parts.Add(NextToken());
            }
        }

        parts.Add(MatchToken(SyntaxKind.CloseParenToken));
        return parts;
    }

    private void ParseLegacyParameterSlots(List<SyntaxNode> parts)
    {
        var expectsParameter = true;
        var sawComma = false;
        while (Current.Kind is not
               SyntaxKind.CloseParenToken and not
               SyntaxKind.EndOfFileToken)
        {
            if (Current.Kind == SyntaxKind.CommaToken)
            {
                if (expectsParameter)
                {
                    parts.Add(CreateLegacyWildcardParameter(Current.Span.Start));
                }

                parts.Add(NextToken());
                expectsParameter = true;
                sawComma = true;
                continue;
            }

            parts.Add(new ParameterSyntax(ParseTypeUse()));
            expectsParameter = false;
            if (Current.Kind != SyntaxKind.CommaToken) break;
        }

        if (sawComma && expectsParameter)
        {
            parts.Add(CreateLegacyWildcardParameter(Current.Span.Start));
        }
    }

    private static ParameterSyntax CreateLegacyWildcardParameter(int position)
    {
        return new ParameterSyntax(
            new TypeUseSyntax(
                Array.Empty<SyntaxToken>(),
                new WildcardTypePatternSyntax(
                    SyntaxToken.Missing(SyntaxKind.StarToken, position))));
    }

    private void AddMigrationDiagnosticIfNeeded(SyntaxToken keyword)
    {
        if (LanguageVersion != AspectNLanguageVersion.Standard) return;

        if (keyword.Text == "execution")
        {
            _parser.AddSelectorDiagnostic(
                DiagnosticCodes.ExecutionSelectorMigration,
                "Use callable(...) instead of execution(...).",
                keyword);
        }
        else if (keyword.Text == "property")
        {
            _parser.AddSelectorDiagnostic(
                DiagnosticCodes.PropertySelectorMigration,
                "Use accessor(...) instead of property(...).",
                keyword);
        }
    }

    private SelectorFamily GetSelectorFamily(string text)
    {
        if (EqualsKeyword(text, "callable")) return SelectorFamily.Callable;
        if (EqualsKeyword(text, "method")) return SelectorFamily.Method;
        if (EqualsKeyword(text, "accessor")) return SelectorFamily.Accessor;
        if (EqualsKeyword(text, "getter")) return SelectorFamily.Getter;
        if (EqualsKeyword(text, "setter")) return SelectorFamily.Setter;
        if (EqualsKeyword(text, "ctor")) return SelectorFamily.Constructor;
        if (EqualsKeyword(text, "cctor")) return SelectorFamily.StaticConstructor;
        if (EqualsKeyword(text, "attr")) return SelectorFamily.Attribute;
        if (EqualsKeyword(text, "regex")) return SelectorFamily.Regex;
        if (text == "execution" ||
            (LanguageVersion == AspectNLanguageVersion.Legacy &&
             string.Equals(text, "execution", StringComparison.OrdinalIgnoreCase)))
        {
            return SelectorFamily.Callable;
        }

        if (text == "property" ||
            (LanguageVersion == AspectNLanguageVersion.Legacy &&
             string.Equals(text, "property", StringComparison.OrdinalIgnoreCase)))
        {
            return SelectorFamily.Accessor;
        }

        return SelectorFamily.Extension;
    }

    private bool IsModifier(string text)
    {
        return EqualsKeyword(text, "public") ||
               EqualsKeyword(text, "protected") ||
               EqualsKeyword(text, "internal") ||
               EqualsKeyword(text, "private") ||
               EqualsKeyword(text, "static") ||
               (LanguageVersion == AspectNLanguageVersion.Legacy &&
                (EqualsKeyword(text, "privateprotected") ||
                 EqualsKeyword(text, "protectedinternal")));
    }

    private bool IsAttributePosition(string text)
    {
        if (LanguageVersion == AspectNLanguageVersion.Legacy)
        {
            return EqualsKeyword(text, "type") ||
                   EqualsKeyword(text, "exec") ||
                   EqualsKeyword(text, "para") ||
                   EqualsKeyword(text, "ret") ||
                   text == "*";
        }

        return text is
            "type" or
            "callable" or
            "property" or
            "parameter" or
            "return" or
            "any";
    }

    private bool IsIdentifier(string text)
    {
        return Current.Kind == SyntaxKind.IdentifierToken &&
               EqualsKeyword(Current.Text, text);
    }

    private bool EqualsKeyword(string actual, string expected)
    {
        return string.Equals(
            actual,
            expected,
            LanguageVersion == AspectNLanguageVersion.Legacy
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
    }

    private SyntaxToken NextToken()
    {
        return _parser.NextSelectorToken();
    }

    private SyntaxToken MatchToken(SyntaxKind expectedKind)
    {
        return _parser.MatchSelectorToken(expectedKind);
    }

    private void ReportUnexpected(SyntaxToken token, string expected)
    {
        _parser.ReportSelectorUnexpected(token, expected);
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

    private enum SelectorFamily
    {
        Extension,
        Callable,
        Method,
        Accessor,
        Getter,
        Setter,
        Constructor,
        StaticConstructor,
        Attribute,
        Regex
    }
}
