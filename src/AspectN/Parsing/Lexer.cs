using System.Globalization;
using System.Text;
using AspectN.Diagnostics;
using AspectN.Syntax;
using AspectN.Text;

namespace AspectN.Parsing;

internal sealed class Lexer
{
    private readonly DiagnosticBag _diagnostics = new();
    private readonly SourceText _source;
    private int _position;

    public Lexer(SourceText source)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(source);
#else
        if (source is null) throw new ArgumentNullException(nameof(source));
#endif

        _source = source;
    }

    public IReadOnlyList<AspectNDiagnostic> Diagnostics => _diagnostics.ToArray();

    // 供选择器解析做投机性前瞻：将词法位置回退到指定偏移重新扫描。
    // 前瞻使用独立 Lexer 实例，诊断不会混入主实例。
    internal void Reset(int position)
    {
        _position = position;
    }

    public SyntaxToken Lex()
    {
        SkipWhitespace();
        if (IsAtEnd)
        {
            return new SyntaxToken(
                SyntaxKind.EndOfFileToken,
                new TextSpan(_position, 0),
                string.Empty);
        }

        if (IsIdentifierStart(Current)) return LexIdentifier();
        if (char.IsDigit(Current) || (Current == '-' && char.IsDigit(Peek(1))))
        {
            return LexInteger();
        }

        if (Current == '"') return LexString();

        var start = _position;
        var character = Current;
        _position++;

        switch (character)
        {
            case '!':
                return CreateToken(SyntaxKind.BangToken, start);
            case '&' when Current == '&':
                _position++;
                return CreateToken(SyntaxKind.AmpersandAmpersandToken, start);
            case '|'
                when Current == '|':
                _position++;
                return CreateToken(SyntaxKind.PipePipeToken, start);
            case '|':
                return CreateToken(SyntaxKind.PipeToken, start);
            case '(':
                return CreateToken(SyntaxKind.OpenParenToken, start);
            case ')':
                return CreateToken(SyntaxKind.CloseParenToken, start);
            case '<':
                return CreateToken(SyntaxKind.LessThanToken, start);
            case '>':
                return CreateToken(SyntaxKind.GreaterThanToken, start);
            case '[':
                return CreateToken(SyntaxKind.OpenBracketToken, start);
            case ']':
                return CreateToken(SyntaxKind.CloseBracketToken, start);
            case ',':
                return CreateToken(SyntaxKind.CommaToken, start);
            case '.' when Current == '.':
                _position++;
                return CreateToken(SyntaxKind.DotDotToken, start);
            case '.':
                return CreateToken(SyntaxKind.DotToken, start);
            case '/':
                return CreateToken(SyntaxKind.SlashToken, start);
            case '*':
                return CreateToken(SyntaxKind.StarToken, start);
            case '+':
                return CreateToken(SyntaxKind.PlusToken, start);
            case '?':
                return CreateToken(SyntaxKind.QuestionToken, start);
            case '$':
                return CreateToken(SyntaxKind.DollarToken, start);
            default:
                _diagnostics.Add(
                    DiagnosticCodes.BadCharacter,
                    DiagnosticSeverity.Error,
                    $"Bad character input: '{character}'.",
                    new TextSpan(start, 1));
                return CreateToken(SyntaxKind.BadToken, start);
        }
    }

    public SyntaxToken LexRegexBody()
    {
        var start = _position;
        var groupDepth = 0;
        var inComment = false;
        var escaped = false;
        var foundDelimiter = false;

        while (!IsAtEnd)
        {
            var character = Current;

            // .NET 语义：(?# 注释由第一个 ) 结束；注释内容对转义、字符类、
            // 分组全部不透明，因此内容消费必须先于其他一切分支检查。
            if (inComment)
            {
                if (character == ')')
                {
                    inComment = false;
                }

                _position++;
                continue;
            }

            if (escaped)
            {
                escaped = false;
                _position++;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                _position++;
                continue;
            }

            if (character == '[')
            {
                SkipRegexCharacterClass();
                continue;
            }

            // (?# 的识别必须保持在转义与字符类检查之后：\( 与 [( 不能开启注释。
            if (character == '(' && Peek(1) == '?' && Peek(2) == '#')
            {
                inComment = true;
                _position += 3;
                continue;
            }

            if (character == '(')
            {
                groupDepth++;
                _position++;
                continue;
            }

            if (character != ')')
            {
                _position++;
                continue;
            }

            if (groupDepth == 0)
            {
                foundDelimiter = true;
                break;
            }

            groupDepth--;
            _position++;
        }

        if (!foundDelimiter)
        {
            _diagnostics.Add(
                DiagnosticCodes.UnterminatedRegex,
                DiagnosticSeverity.Error,
                "Unterminated regular expression.",
                new TextSpan(start, _position - start));
        }

        return CreateToken(SyntaxKind.RegexBodyToken, start);
    }

    // 按 .NET 字符类的原子、范围、减法层级扫描；每层首位 ]（含 ^ 后）是字面量。
    private void SkipRegexCharacterClass()
    {
        _position++;
        var depth = 1;
        var atStart = true;
        var negationSeen = false;
        var inRange = false;
        while (!IsAtEnd)
        {
            var character = Current;
            if (atStart && character == '^' && !negationSeen)
            {
                negationSeen = true;
                _position++;
                continue;
            }

            if (character == ']' && !atStart)
            {
                _position++;
                depth--;
                if (depth == 0) return;
                // 减法必须位于外层末尾，外层已消费元素，无需恢复首位或范围状态。
                inRange = false;
                continue;
            }

            var translated = character == '\\';
            var canStartRange = true;
            if (translated)
            {
                canStartRange = SkipRegexClassEscape();
            }
            else
            {
                _position++;
            }

            if (inRange)
            {
                inRange = false;
                if (character == '[' && !translated)
                {
                    depth++;
                    atStart = true;
                    negationSeen = false;
                    continue;
                }
                // 范围端点已被消费；即使端点是 '-' 也不能再次解释为减法开头。
            }
            else if (canStartRange && Current == '-' &&
                     _position + 1 < _source.Length && Peek(1) != ']')
            {
                inRange = true;
                _position++;
            }
            else if (character == '-' && !translated && Current == '[' && !atStart)
            {
                _position++;
                depth++;
                atStart = true;
                negationSeen = false;
                continue;
            }

            atStart = false;
        }
    }

    // 转义必须作为完整原子消费，避免把 \xNN、\uNNNN 或 \p{...} 的尾部当作范围起点。
    private bool SkipRegexClassEscape()
    {
        _position++;
        if (IsAtEnd) return false;
        var escape = Current;
        _position++;
        switch (escape)
        {
            case 'd' or 'D' or 's' or 'S' or 'w' or 'W' or '-':
                return false;
            case 'p' or 'P':
                if (Current == '{')
                {
                    _position++;
                    while (!IsAtEnd && Current != '}') _position++;
                    if (!IsAtEnd) _position++;
                }

                return false;
            case 'x' or 'u':
                for (var index = 0; index < 4 && Current is
                         >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'; index++)
                {
                    _position++;
                }

                break;
            case 'c':
                if (!IsAtEnd) _position++;
                break;
            case >= '0' and <= '7':
                for (var index = 0; index < 2 && Current is >= '0' and <= '7'; index++)
                {
                    _position++;
                }

                break;
        }

        return true;
    }

    private bool IsAtEnd => _position >= _source.Length;

    private char Current => Peek(0);

    private SyntaxToken LexIdentifier()
    {
        var start = _position;
        _position++;
        while (!IsAtEnd && IsIdentifierPart(Current)) _position++;

        var text = _source.Substring(new TextSpan(start, _position - start));
        return new SyntaxToken(
            SyntaxKind.IdentifierToken,
            new TextSpan(start, _position - start),
            text,
            text);
    }

    private SyntaxToken LexInteger()
    {
        var start = _position;
        if (Current == '-') _position++;
        while (!IsAtEnd && char.IsDigit(Current)) _position++;

        var span = new TextSpan(start, _position - start);
        var text = _source.Substring(span);
        if (long.TryParse(
            text,
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out var value))
        {
            return new SyntaxToken(SyntaxKind.IntegerToken, span, text, value);
        }

        _diagnostics.Add(
            DiagnosticCodes.InvalidInteger,
            DiagnosticSeverity.Error,
            "Integer literal is outside the supported range.",
            span);
        return new SyntaxToken(SyntaxKind.IntegerToken, span, text);
    }

    private SyntaxToken LexString()
    {
        var start = _position;
        var value = new StringBuilder();
        var terminated = false;
        _position++;

        while (!IsAtEnd)
        {
            var character = Current;
            if (character == '"')
            {
                _position++;
                terminated = true;
                break;
            }

            if (character is '\r' or '\n') break;

            if (character != '\\')
            {
                value.Append(character);
                _position++;
                continue;
            }

            var escapeStart = _position;
            _position++;
            if (IsAtEnd) break;

            character = Current;
            _position++;
            switch (character)
            {
                case '\'':
                    value.Append('\'');
                    break;
                case '"':
                    value.Append('"');
                    break;
                case '\\':
                    value.Append('\\');
                    break;
                case '0':
                    value.Append('\0');
                    break;
                case 'a':
                    value.Append('\a');
                    break;
                case 'b':
                    value.Append('\b');
                    break;
                case 'f':
                    value.Append('\f');
                    break;
                case 'n':
                    value.Append('\n');
                    break;
                case 'r':
                    value.Append('\r');
                    break;
                case 't':
                    value.Append('\t');
                    break;
                case 'v':
                    value.Append('\v');
                    break;
                default:
                    _diagnostics.Add(
                        DiagnosticCodes.InvalidEscapeSequence,
                        DiagnosticSeverity.Error,
                        "Invalid string escape sequence.",
                        new TextSpan(escapeStart, 2));
                    value.Append(character);
                    break;
            }
        }

        var span = new TextSpan(start, _position - start);
        if (!terminated)
        {
            _diagnostics.Add(
                DiagnosticCodes.UnterminatedString,
                DiagnosticSeverity.Error,
                "Unterminated string literal.",
                span);
        }

        return new SyntaxToken(
            SyntaxKind.StringToken,
            span,
            _source.Substring(span),
            value.ToString());
    }

    private SyntaxToken CreateToken(SyntaxKind kind, int start)
    {
        var span = new TextSpan(start, _position - start);
        return new SyntaxToken(kind, span, _source.Substring(span));
    }

    private char Peek(int offset)
    {
        var index = _position + offset;
        return index >= _source.Length ? '\0' : _source[index];
    }

    private void SkipWhitespace()
    {
        while (!IsAtEnd && char.IsWhiteSpace(Current)) _position++;
    }

    private static bool IsIdentifierStart(char character)
    {
        return character == '_' || char.IsLetter(character);
    }

    private static bool IsIdentifierPart(char character)
    {
        if (character == '_' || char.IsLetterOrDigit(character)) return true;

        return char.GetUnicodeCategory(character) is
            UnicodeCategory.ConnectorPunctuation or
            UnicodeCategory.NonSpacingMark or
            UnicodeCategory.SpacingCombiningMark or
            UnicodeCategory.Format;
    }
}
