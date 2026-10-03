using AspectN.Diagnostics;
using AspectN.Syntax;

namespace AspectN.Parsing;

internal sealed class SelectorParser(Parser parser)
{
    private readonly Parser _parser = parser;

    private SyntaxToken Current => _parser.SelectorCurrent;

    // 选择器解析先识别族类型，再把后续签名解析委托给对应分支。
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
            SelectorFamily.Property => ParseProperty(keyword, family),
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
        var openParen = MatchToken(SyntaxKind.OpenParenToken);
        var signature = ParseMethodSignature();
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
        var openParen = MatchToken(SyntaxKind.OpenParenToken);
        var signature = ParsePropertySignature();
        var closeParen = MatchToken(SyntaxKind.CloseParenToken);
        return family switch
        {
            SelectorFamily.Property => new PropertySelectorSyntax(
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
            Current.Kind == SyntaxKind.StarToken)
        {
            var position = NextToken();
            parts.Add(position);
            if (!IsAttributePosition(position.Text))
            {
                ReportUnexpected(position, "attribute position");
            }

            ReportAttributePositionMigration(position);

            // 参数位置支持括号与空格写法；负索引（含 "-0"）按文本拒绝。
            var isParameterPosition = position.Text is "para" or "parameter";
            if (isParameterPosition)
            {
                if (Current.Kind == SyntaxKind.OpenBracketToken)
                {
                    parts.Add(MatchToken(SyntaxKind.OpenBracketToken));
                    if (Current.Kind is SyntaxKind.IntegerToken or SyntaxKind.StarToken)
                    {
                        var index = NextToken();
                        parts.Add(index);
                        if (IsNegativeIndex(index))
                        {
                            ReportUnexpected(index, "non-negative parameter index");
                        }
                    }
                    else
                    {
                        ReportUnexpected(Current, "parameter index or '*'");
                    }

                    parts.Add(MatchToken(SyntaxKind.CloseBracketToken));
                }
                else
                {
                    if (Current.Kind is SyntaxKind.IntegerToken or SyntaxKind.StarToken)
                    {
                        var index = NextToken();
                        parts.Add(index);
                        if (IsNegativeIndex(index))
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

    // Standard 的 attr 长形式位置（callable/property/parameter/return）已简写为
    // call/prop/para/ret；识别长形式并给出可操作的迁移诊断（规范 §17）。
    private void ReportAttributePositionMigration(SyntaxToken position)
    {
        var replacement = position.Text switch
        {
            "callable" => "call",
            "property" => "prop",
            "parameter" => "para",
            "return" => "ret",
            _ => null
        };
        if (replacement is null)
        {
            return;
        }

        _parser.AddSelectorDiagnostic(
            DiagnosticCodes.AttributePositionMigration,
            $"Use '{replacement}' instead of '{position.Text}'.",
            position);
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

    private MethodSignatureSyntax ParseMethodSignature()
    {
        var parts = new List<SyntaxNode>();
        parts.AddRange(ParseModifiers());
        // 识别不支持的 async 修饰符，避免静默当作返回类型名。
        if (Current.Kind == SyntaxKind.IdentifierToken &&
            Current.Text == "async")
        {
            ReportUnexpected(
                Current,
                "return type ('async' is not supported in Standard)");

            parts.Add(NextToken());
        }

        parts.Add(ParseTypeUse());
        parts.AddRange(ParseMemberPath());
        parts.AddRange(ParseParameterPatternList());
        return new MethodSignatureSyntax(parts);
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
                var following = _parser.PeekSelectorTokens(2)[1];
                if (!IsModifier(following.Text) &&
                    !TryGetCompoundAccessibilityReplacement(following.Text, out _))
                {
                    // Otherwise ! starts the return type pattern, e.g. !int.
                    break;
                }

                var tokens = new List<SyntaxToken> { NextToken() };
                if (!IsModifier(Current.Text))
                {
                    // Standard 下的连写复合访问性按旧拼写识别，给出可操作的迁移诊断（规范 §17）。
                    if (TryGetCompoundAccessibilityReplacement(
                            Current.Text,
                            out var negatedReplacement))
                    {
                        tokens.Add(NextToken());
                        AddCompoundAccessibilityMigration(
                            tokens[1],
                            negatedReplacement);
                        modifiers.Add(new ModifierSyntax(tokens));
                        continue;
                    }

                    ReportUnexpected(Current, "accessibility or static modifier");
                    modifiers.Add(new ModifierSyntax(tokens));
                    break;
                }

                tokens.Add(NextToken());
                // 否定作用于完整 accessibility 短语，复合访问修饰符必须整体消费，不能拆成两条约束。
                if (Current.Kind == SyntaxKind.IdentifierToken &&
                    IsCompoundAccessibilityPair(tokens[1].Text, Current.Text))
                {
                    tokens.Add(NextToken());
                }

                if (Current.Kind == SyntaxKind.PipeToken)
                {
                    ReportUnexpected(
                        Current,
                        "'|' (cannot follow a negated modifier)");
                    tokens.Add(NextToken());
                }

                modifiers.Add(new ModifierSyntax(tokens));
                continue;
            }

            if (Current.Kind == SyntaxKind.OpenParenToken)
            {
                if (IsParenthesizedAccessibilityStart())
                {
                    var parenthesizedTokens = new List<SyntaxToken>
                    {
                        NextToken(),
                        NextToken()
                    };
                    ConsumeParenthesizedAccessibility(parenthesizedTokens);
                    ParseAccessibilityAlternatives(parenthesizedTokens);
                    modifiers.Add(new ModifierSyntax(parenthesizedTokens));
                    continue;
                }
            }

            if (Current.Kind != SyntaxKind.IdentifierToken)
            {
                break;
            }

            if (!IsModifier(Current.Text))
            {
                if (TryGetCompoundAccessibilityReplacement(
                        Current.Text,
                        out var replacement))
                {
                    var misspelled = NextToken();
                    AddCompoundAccessibilityMigration(misspelled, replacement);
                    modifiers.Add(
                        new ModifierSyntax(new List<SyntaxToken> { misspelled }));
                    continue;
                }

                break;
            }

            var modifierTokens = new List<SyntaxToken> { NextToken() };
            if (Current.Kind == SyntaxKind.IdentifierToken &&
                IsCompoundAccessibilityPair(modifierTokens[0].Text, Current.Text))
            {
                modifierTokens.Add(NextToken());
            }

            if (Current.Kind == SyntaxKind.PipeToken)
            {
                ParseAccessibilityAlternatives(modifierTokens);
            }

            modifiers.Add(new ModifierSyntax(modifierTokens));
        }

        return modifiers;
    }

    // 规范 §10：Standard 用 | 连接多个正向 accessibility 备选；复合权限参与多选时必须加括号，
    // 如 (private protected)|(protected internal)|public；仅为可读性包裹复合权限（无 |）同样合法。
    // 备选内容（static、否定、未加括号的复合短语）的合法性由解析与绑定各自校验。
    private void ParseAccessibilityAlternatives(List<SyntaxToken> modifierTokens)
    {
        if (modifierTokens.Count > 1 &&
            modifierTokens[0].Kind != SyntaxKind.OpenParenToken)
        {
            ReportUnexpected(
                Current,
                "'|' (compound accessibility must be parenthesized)");
        }

        while (Current.Kind == SyntaxKind.PipeToken)
        {
            modifierTokens.Add(NextToken());
            if (Current.Kind == SyntaxKind.OpenParenToken)
            {
                modifierTokens.Add(NextToken());
                modifierTokens.Add(NextToken());
                ConsumeParenthesizedAccessibility(modifierTokens);
                continue;
            }

            if (Current.Kind == SyntaxKind.IdentifierToken &&
                IsSingleAccessibility(Current.Text))
            {
                modifierTokens.Add(NextToken());
                if (Current.Kind == SyntaxKind.IdentifierToken &&
                    IsSingleAccessibility(Current.Text))
                {
                    // 两个权限词只有构成合法复合权限（如 private protected）才谈得上“加括号”，
                    // 否则是漏写 | 的多选。
                    ReportUnexpected(
                        Current,
                        IsCompoundAccessibilityPair(
                            modifierTokens[modifierTokens.Count - 1].Text,
                            Current.Text)
                            ? "'|' (compound accessibility must be parenthesized)"
                            : "'|' (accessibility alternatives must be joined with '|')");
                    if (!IsCompoundAccessibilityPair(
                            modifierTokens[modifierTokens.Count - 1].Text, Current.Text))
                    {
                        _parser.AddSelectorDiagnostic(
                            DiagnosticCodes.InvalidModifierCombination,
                            "Accessibility alternatives must form one group joined with '|'.",
                            Current);
                    }
                    modifierTokens.Add(NextToken());
                }

                continue;
            }

            ReportUnexpected(Current, "accessibility alternative");
            break;
        }
    }

    // 消费括号化访问性的其余部分：调用前已消费 ( 与首个权限词；括号内必须是恰两个
    // 权限词构成的合法复合权限，单独一个（如 (public)）或非法搭配（如 (internal public)）
    // 都报错——多选必须用 | 连接，空格在括号内不表达“或”。
    private void ConsumeParenthesizedAccessibility(List<SyntaxToken> tokens)
    {
        if (Current.Kind == SyntaxKind.IdentifierToken &&
            IsSingleAccessibility(Current.Text))
        {
            var second = NextToken();
            tokens.Add(second);
            if (!IsCompoundAccessibilityPair(
                    tokens[tokens.Count - 2].Text,
                    second.Text))
            {
                ReportUnexpected(second, "compound accessibility");
            }
        }
        else
        {
            ReportUnexpected(Current, "compound accessibility");
        }

        if (Current.Kind == SyntaxKind.CloseParenToken)
        {
            tokens.Add(NextToken());
        }
        else
        {
            ReportUnexpected(Current, "')'");
        }
    }

    // 前瞻区分修饰符位置的 (：形状为 ( 权限词 [权限词] ) 时视为访问性（复合权限可加括号
    // 便于阅读，其后可接 | 交替），否则 ( 属于返回类型模式（元组/括号类型），保持原有解析
    // 路径。权限词不是合法类型名，该形状不会与合法类型模式冲突。
    private bool IsParenthesizedAccessibilityStart()
    {
        var tokens = _parser.PeekSelectorTokens(4);
        return tokens.Count == 4 &&
               tokens[0].Kind == SyntaxKind.OpenParenToken &&
               tokens[1].Kind == SyntaxKind.IdentifierToken &&
               IsSingleAccessibility(tokens[1].Text) &&
               (tokens[2].Kind == SyntaxKind.CloseParenToken ||
                (tokens[2].Kind == SyntaxKind.IdentifierToken &&
                 IsSingleAccessibility(tokens[2].Text) &&
                 tokens[3].Kind == SyntaxKind.CloseParenToken));
    }

    private static bool IsSingleAccessibility(string text)
    {
        return text is "public" or "protected" or "internal" or "private";
    }

    private static bool IsCompoundAccessibilityPair(string first, string second)
    {
        return (first == "private" && second == "protected") ||
               (first == "protected" && second == "internal");
    }

    // "-0" 的数值仍是 0，负索引（含 -0）必须按文本判断。
    private static bool IsNegativeIndex(SyntaxToken index)
    {
        return index.Kind == SyntaxKind.IntegerToken &&
               index.Text.Length > 0 &&
               index.Text[0] == '-';
    }

    private TypeUseSyntax ParseTypeUse()
    {
        // !ref（仅 Standard）：显式要求非 by-ref；与具体 ref kind 连写属于矛盾，交由诊断报告。
        var negatedRefTokens = new List<SyntaxToken>();
        if (Current.Kind == SyntaxKind.BangToken)
        {
            var following = _parser.PeekSelectorTokens(2)[1];
            if (following.Kind == SyntaxKind.IdentifierToken &&
                EqualsKeyword(following.Text, "ref"))
            {
                negatedRefTokens.Add(NextToken());
                negatedRefTokens.Add(NextToken());
            }
        }

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

        if (negatedRefTokens.Count > 0 && refKind.Count > 0)
        {
            _parser.AddSelectorDiagnostic(
                DiagnosticCodes.InvalidRefKind,
                "'!ref' cannot be combined with an explicit ref kind.",
                refKind[0]);
        }

        return new TypeUseSyntax(refKind, ParseTypePattern(), negatedRefTokens);
    }

    // 使用优先级爬升解析类型表达式，以保持一元、二元和后缀构造的结合顺序。
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
        if (Current.Kind == SyntaxKind.StarToken)
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

        if (!TypePatternSyntaxFacts.IsNamePart(Current))
        {
            var position = Current.Span.Start;
            ReportUnexpected(Current, "type pattern");
            if (Current.Kind != SyntaxKind.EndOfFileToken) NextToken();
            return CreateMissingType(position);
        }

        var first = NextToken();
        if (first.Text == "tasklike")
        {
            if (Current.Kind != SyntaxKind.LessThanToken)
            {
                return new TaskLikeTypePatternSyntax(first, null, null, null);
            }

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
            // 分隔符必须与前一段名字相邻（§3.4）：跨空格即槽位边界。
            // 不消费该分隔符，留给成员路径诊断与恢复。
            if (Current.Span.Start != segments[segments.Count - 1].FullSpan.End)
            {
                break;
            }

            var separator = NextToken();
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

    private NamePatternSegmentSyntax ParseNameSegment(SyntaxToken? first = null)
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
                SyntaxToken.Missing(
                    SyntaxKind.IdentifierToken,
                    Current.Span.Start));
        }

        return new NamePatternSegmentSyntax(
            parts,
            Current.Kind == SyntaxKind.LessThanToken &&
            Current.Span.Start == parts[parts.Count - 1].Span.End
                ? ParseGenericPatternList()
                : null);
    }

    private GenericPatternListSyntax ParseGenericPatternList()
    {
        var lessThan = NextToken();

        // <!>：显式零泛型。
        if (Current.Kind == SyntaxKind.BangToken)
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

        // 混合空槽：空槽 = Any 占位，arity = 槽数；
        // 全空槽（<,>、<,,>）等价于纯逗号计数形式。
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
                arguments.Add(ParseTypePattern());
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

    private TypePatternSyntax ParseParenthesizedOrTupleType()
    {
        var openParen = NextToken();
        var first = ParseTypePattern();
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
        while (TypePatternSyntaxFacts.IsNamePart(Current) || Current.Kind is
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
                else if (Current.Span.Start != parts[parts.Count - 1].FullSpan.End)
                {
                    ReportUnexpected(Current, "generic pattern (must be adjacent to the member name)");
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
                if (parts.Count == 0)
                {
                    // 省略声明类型就是不写（§7）；成员路径必须以名字开头，
                    // 前导分隔符没有对应语义。
                    ReportUnexpected(
                        Current,
                        "member name (a member path cannot start with a separator)");
                }
                else if (Current.Span.Start != parts[parts.Count - 1].FullSpan.End)
                {
                    // 分隔符必须与前一部件相邻（§3.4）：跨空格的 '.' 属于槽位边界。
                    ReportUnexpected(
                        Current,
                        "member path separator (must be adjacent)");
                }

                parts.Add(NextToken());
                hasName = false;
                hasGeneric = false;
                continue;
            }

            if (Current.Kind == SyntaxKind.PlusToken && !hasName)
            {
                ReportUnexpected(Current, "declaring type name before '+'");
            }

            if (hasGeneric && Current.Kind != SyntaxKind.PlusToken)
            {
                // 泛型子句后仅允许 + 后缀（声明类型的可赋值目标）与分隔符（§7、§12.4）。
                ReportUnexpected(Current, "member path separator");
            }
            else if (parts.Count > 0 &&
                     Current.Span.Start != parts[parts.Count - 1].FullSpan.End)
            {
                // 名字（含 + 后缀）必须与前一部件相邻：跨空格的延续属于另一个槽位（§3.4、§7）。
                ReportUnexpected(Current, "member path separator");
            }

            parts.Add(NextToken());
            hasName = true;
        }

        if (parts.Count == 0)
        {
            ReportUnexpected(Current, "member path");
        }
        else if (parts[parts.Count - 1] is SyntaxToken
        {
            Kind: SyntaxKind.DotToken or
                    SyntaxKind.DotDotToken or
                    SyntaxKind.SlashToken
        })
        {
            // 分隔符后缺少名称会绑定出永不匹配的空成员名。
            ReportUnexpected(Current, "member name");
        }
        else
        {
            // Standard 的名称模型要求 .. 只出现在命名空间中间：紧邻成员名的分隔符
            // 若是 ..，声明类型就缺了类型名段（少一段，如 *..M、System..*）。
            for (var index = parts.Count - 1; index >= 0; index--)
            {
                if (parts[index] is not SyntaxToken
                    {
                        Kind: SyntaxKind.DotToken or
                              SyntaxKind.DotDotToken or
                              SyntaxKind.SlashToken
                    } separator)
                {
                    continue;
                }

                if (separator.Kind == SyntaxKind.DotDotToken)
                {
                    ReportUnexpected(
                        separator,
                        "'.' (a member name must be joined to its declaring type with '.')");
                }

                break;
            }
        }

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
        else
        {
            // 参数空槽：逗号之间/末尾的空位 = 隐式 * 参数，
            // (int,) = 两个参数（int 与任意），(,int) 同理。
            ParseParameterSlots(parts);
        }

        parts.Add(MatchToken(SyntaxKind.CloseParenToken));
        return parts;
    }

    private void ParseParameterSlots(List<SyntaxNode> parts)
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
                    parts.Add(CreateWildcardParameter(Current.Span.Start));
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
            parts.Add(CreateWildcardParameter(Current.Span.Start));
        }
    }

    private static ParameterSyntax CreateWildcardParameter(int position)
    {
        return new ParameterSyntax(
            new TypeUseSyntax(
                Array.Empty<SyntaxToken>(),
                new WildcardTypePatternSyntax(
                    SyntaxToken.Missing(SyntaxKind.StarToken, position))));
    }

    // 连写复合访问性提供迁移诊断，匹配只接受空格短语。
    private static bool TryGetCompoundAccessibilityReplacement(
        string text,
        out string replacement)
    {
        if (text == "privateprotected")
        {
            replacement = "private protected";
            return true;
        }

        if (text == "protectedinternal")
        {
            replacement = "protected internal";
            return true;
        }

        replacement = string.Empty;
        return false;
    }

    private void AddCompoundAccessibilityMigration(
        SyntaxToken token,
        string replacement)
    {
        _parser.AddSelectorDiagnostic(
            DiagnosticCodes.CompositeAccessibilityMigration,
            $"Use '{replacement}' instead of '{token.Text}'.",
            token);
    }

    private static SelectorFamily GetSelectorFamily(string text)
    {
        if (EqualsKeyword(text, "callable")) return SelectorFamily.Callable;
        if (EqualsKeyword(text, "execution")) return SelectorFamily.Callable;
        if (EqualsKeyword(text, "method")) return SelectorFamily.Method;
        if (EqualsKeyword(text, "property")) return SelectorFamily.Property;
        if (EqualsKeyword(text, "getter")) return SelectorFamily.Getter;
        if (EqualsKeyword(text, "setter")) return SelectorFamily.Setter;
        if (EqualsKeyword(text, "ctor")) return SelectorFamily.Constructor;
        if (EqualsKeyword(text, "cctor")) return SelectorFamily.StaticConstructor;
        if (EqualsKeyword(text, "attr")) return SelectorFamily.Attribute;
        if (EqualsKeyword(text, "regex")) return SelectorFamily.Regex;
        return SelectorFamily.Extension;
    }

    private static bool IsModifier(string text)
    {
        return EqualsKeyword(text, "public") ||
               EqualsKeyword(text, "protected") ||
               EqualsKeyword(text, "internal") ||
               EqualsKeyword(text, "private") ||
               EqualsKeyword(text, "static");
    }

    private static bool IsAttributePosition(string text)
    {
        // 长形式 callable/property/parameter/return 仍被识别以给出迁移诊断。
        return text is
            "type" or
            "call" or
            "prop" or
            "para" or
            "ret" or
            "any" or
            "exec" or
            "*" or
            "callable" or
            "property" or
            "parameter" or
            "return";
    }

    private bool IsIdentifier(string text)
    {
        return Current.Kind == SyntaxKind.IdentifierToken &&
               EqualsKeyword(Current.Text, text);
    }

    private static bool EqualsKeyword(string actual, string expected)
    {
        return string.Equals(
            actual,
            expected,
            StringComparison.Ordinal);
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
        Property,
        Getter,
        Setter,
        Constructor,
        StaticConstructor,
        Attribute,
        Regex
    }
}
