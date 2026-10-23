using AspectN.Execution;
using AspectN.Diagnostics;
using AspectN.Syntax;

namespace AspectN.Parsing;

internal sealed partial class SelectorParser(Parser parser) : TypeSyntaxParser
{
    private readonly Parser _parser = parser;
    protected override ParseContext Context => _parser.Context;
    protected override ParserDepthGuard DepthGuard => _parser.DepthGuard;
    protected override int? MemberSeparatorStart => _memberSeparatorStart;
    protected override SyntaxToken PeekNextToken(int offset = 1) => _parser.PeekSelectorTokens(offset + 1)[offset];
    protected override void ReportConsecutiveTypeNegation() => _parser.ReportConsecutiveNegation();

    private MemberNamePatternSyntax ParseMemberName(int parentPrecedence = 0) =>
        WorkStack.Run(ParseMemberNameWork(parentPrecedence), Context.CancellationToken);

    protected override SyntaxToken Current => _parser.SelectorCurrent;

    // 选择器解析先识别族类型，再把后续签名解析委托给对应分支。
    public SelectorSyntax ParseSelector()
    {
        var nameTokens = ParseQualifiedName();
        if (nameTokens.Count != 1)
        {
            return ParseUnknownSelector(nameTokens);
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
            _ => ParseUnknownSelector(nameTokens)
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
                ReportUnexpected(Current, "selector name segment");
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
            ? Context.Create(() => new CallableSelectorSyntax(
                keyword,
                openParen,
                signature,
                closeParen))
            : Context.Create(() => new MethodSelectorSyntax(
                keyword,
                openParen,
                signature,
                closeParen));
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
            SelectorFamily.Property => Context.Create(() => new PropertySelectorSyntax(
                keyword,
                openParen,
                signature,
                closeParen)),
            SelectorFamily.Getter => Context.Create(() => new GetterSelectorSyntax(
                keyword,
                openParen,
                signature,
                closeParen)),
            _ => Context.Create(() => new SetterSelectorSyntax(
                keyword,
                openParen,
                signature,
                closeParen))
        };
    }

    private ConstructorSelectorSyntax ParseConstructor(SyntaxToken keyword)
    {
        var openParen = MatchToken(SyntaxKind.OpenParenToken);
        var parts = new List<SyntaxNode> { ParseTypePattern() };
        parts.AddRange(ParseParameterPatternList());
        var signature = Context.Create(() => new ConstructorSignatureSyntax(parts));
        return Context.Create(() => new ConstructorSelectorSyntax(
            keyword,
            openParen,
            signature,
            MatchToken(SyntaxKind.CloseParenToken)));
    }

    private StaticConstructorSelectorSyntax ParseStaticConstructor(
        SyntaxToken keyword)
    {
        var openParen = MatchToken(SyntaxKind.OpenParenToken);
        var typePattern = ParseTypePattern();
        return Context.Create(() => new StaticConstructorSelectorSyntax(
            keyword,
            openParen,
            typePattern,
            MatchToken(SyntaxKind.CloseParenToken)));
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
        var signature = Context.Create(() => new AttributeSignatureSyntax(parts));
        return Context.Create(() => new AttributeSelectorSyntax(
            keyword,
            openParen,
            signature,
            MatchToken(SyntaxKind.CloseParenToken)));
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
            : Context.Create(() => SyntaxToken.Missing(SyntaxKind.RegexBodyToken, Current.Span.Start));
        return Context.Create(() => new RegexSelectorSyntax(
            keyword,
            openParen,
            body,
            MatchToken(SyntaxKind.CloseParenToken)));
    }

    private InvalidSelectorSyntax ParseUnknownSelector(List<SyntaxToken> nameTokens)
    {
        _parser.AddSelectorDiagnostic(
            DiagnosticCodes.UnknownSelector,
            $"Selector '{string.Concat(nameTokens.Select(token => token.Text))}' is not recognized.",
            nameTokens[0]);
        var tokens = new List<SyntaxNode>(nameTokens);
        var openParen = MatchToken(SyntaxKind.OpenParenToken);
        tokens.Add(openParen);
        var hasClosingParen = _parser.HasSelectorClosingParen(openParen);
        var depth = 1;
        while (Current.Kind != SyntaxKind.EndOfFileToken && depth > 0)
        {
            // 未知选择器只恢复括号边界，不解释参数；外层 Boolean 运算符保留给主解析器。
            if (!hasClosingParen && depth == 1 &&
                Current.Kind is SyntaxKind.AmpersandAmpersandToken or SyntaxKind.PipePipeToken)
                break;
            var token = NextToken();
            tokens.Add(token);
            if (token.Kind == SyntaxKind.OpenParenToken) depth++;
            else if (token.Kind == SyntaxKind.CloseParenToken) depth--;
        }

        var closeParen = depth > 0
            ? MatchToken(SyntaxKind.CloseParenToken)
            : (SyntaxToken)tokens[tokens.Count - 1];
        if (depth > 0) tokens.Add(closeParen);
        return Context.Create(() => new InvalidSelectorSyntax(tokens, openParen, closeParen));
    }

    private MethodSignatureSyntax ParseMethodSignature()
    {
        var parts = new List<SyntaxNode>();
        parts.AddRange(ParseModifiers());
        ParseUnsupportedAsyncModifier(parts);
        parts.Add(ParseTypeUse());
        parts.Add(ParseMemberPath());
        parts.AddRange(ParseParameterPatternList());
        return Context.Create(() => new MethodSignatureSyntax(parts));
    }

    private void ParseUnsupportedAsyncModifier(List<SyntaxNode> parts)
    {
        // 识别不支持的 async 修饰符，避免静默当作返回类型名。
        if (Current.Kind == SyntaxKind.IdentifierToken &&
            Current.Text == "async")
        {
            if (HasTypePatternContinuation()) return;
            ReportUnexpected(
                Current,
                "type ('async' is not supported in Standard)");

            parts.Add(NextToken());
        }

    }

    private PropertySignatureSyntax ParsePropertySignature()
    {
        var parts = new List<SyntaxNode>();
        parts.AddRange(ParseModifiers());
        ParseUnsupportedAsyncModifier(parts);
        parts.Add(ParseTypeUse());
        parts.Add(ParseMemberPath());
        return Context.Create(() => new PropertySignatureSyntax(parts));
    }

    // 连续名称、限定/嵌套路径、泛型及类型后缀优先属于完整类型模式，不能截成修饰符。
    private bool HasTypePatternContinuation(int offset = 0)
    {
        var tokens = _parser.PeekSelectorTokens(offset + 2);
        var keyword = tokens[offset];
        var following = tokens[offset + 1];
        return following.Span.Start == keyword.Span.End &&
               (TypePatternSyntaxFacts.IsNamePart(following) || following.Kind is
                   SyntaxKind.DotToken or SyntaxKind.DotDotToken or SyntaxKind.SlashToken or
                   SyntaxKind.LessThanToken or SyntaxKind.OpenBracketToken or SyntaxKind.QuestionToken or SyntaxKind.PlusToken);
    }

    private List<ModifierSyntax> ParseModifiers()
    {
        var modifiers = new List<ModifierSyntax>();
        while (true)
        {
            if (Current.Kind == SyntaxKind.BangToken)
            {
                var following = _parser.PeekSelectorTokens(2)[1];
                if (HasTypePatternContinuation(1) ||
                    !IsModifier(following.Text) && !TryGetCompoundAccessibilityReplacement(following.Text, out _))
                {
                    // 否则 ! 属于返回类型模式，例如 !int、!static*。
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
                        modifiers.Add(Context.Create(() => new ModifierSyntax(tokens)));
                        continue;
                    }

                    ReportUnexpected(Current, "accessibility or static modifier");
                    modifiers.Add(Context.Create(() => new ModifierSyntax(tokens)));
                    break;
                }

                tokens.Add(NextToken());
                // 否定作用于完整 accessibility 短语，复合访问修饰符必须整体消费，不能拆成两条约束。
                if (Current.Kind == SyntaxKind.IdentifierToken &&
                    IsCompoundAccessibilityPair(tokens[1].Text, Current.Text) && !HasTypePatternContinuation())
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

                modifiers.Add(Context.Create(() => new ModifierSyntax(tokens)));
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
                    modifiers.Add(Context.Create(() => new ModifierSyntax(parenthesizedTokens)));
                    continue;
                }
            }

            if (Current.Kind != SyntaxKind.IdentifierToken || HasTypePatternContinuation())
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
                        Context.Create(() => new ModifierSyntax(new List<SyntaxToken> { misspelled })));
                    continue;
                }

                break;
            }

            var modifierTokens = new List<SyntaxToken> { NextToken() };
            if (Current.Kind == SyntaxKind.IdentifierToken &&
                IsCompoundAccessibilityPair(modifierTokens[0].Text, Current.Text) && !HasTypePatternContinuation())
            {
                modifierTokens.Add(NextToken());
            }

            if (Current.Kind == SyntaxKind.PipeToken)
            {
                ParseAccessibilityAlternatives(modifierTokens);
            }

            modifiers.Add(Context.Create(() => new ModifierSyntax(modifierTokens)));
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
                IsSingleAccessibility(Current.Text) && !HasTypePatternContinuation())
            {
                modifierTokens.Add(NextToken());
                if (Current.Kind == SyntaxKind.IdentifierToken &&
                    IsSingleAccessibility(Current.Text) && !HasTypePatternContinuation())
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
                if (following.Span.Start != Current.Span.End)
                    _parser.AddSelectorDiagnostic(DiagnosticCodes.InvalidRefKind,
                        "'!ref' requires adjacent '!' and 'ref' tokens.", Current);
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

        return Context.Create(() => new TypeUseSyntax(refKind, ParseTypePattern(), negatedRefTokens));
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

            parts.Add(Context.Create(() => new ParameterSyntax(ParseTypeUse())));
            expectsParameter = false;
            if (Current.Kind != SyntaxKind.CommaToken) break;
        }

        if (sawComma && expectsParameter)
        {
            parts.Add(CreateWildcardParameter(Current.Span.Start));
        }
    }

    private ParameterSyntax CreateWildcardParameter(int position)
    {
        return Context.Create(() => new ParameterSyntax(
            Context.Create(() => new TypeUseSyntax(
                Array.Empty<SyntaxToken>(),
                Context.Create(() => new WildcardTypePatternSyntax(
                    Context.Create(() => SyntaxToken.Missing(SyntaxKind.StarToken, position))))))));
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
            DiagnosticCodes.CompoundAccessibilityMigration,
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
        return SelectorFamily.Unknown;
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

    private static bool EqualsKeyword(string actual, string expected)
    {
        return string.Equals(
            actual,
            expected,
            StringComparison.Ordinal);
    }

    protected override SyntaxToken NextToken()
    {
        return _parser.NextSelectorToken();
    }

    protected override SyntaxToken MatchToken(SyntaxKind expectedKind)
    {
        return _parser.MatchSelectorToken(expectedKind);
    }

    protected override void ReportUnexpected(SyntaxToken token, string expected)
    {
        _parser.ReportSelectorUnexpected(token, expected);
    }

    private enum SelectorFamily
    {
        Unknown,
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
