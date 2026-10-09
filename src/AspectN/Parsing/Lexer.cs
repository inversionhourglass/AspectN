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
    // 前瞻使用独立 Lexer 实例；Reset 只移动游标，保留本实例已经产生的诊断。
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
        if (IsAsciiDigit(Current) || (Current == '-' && IsAsciiDigit(Peek(1))))
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
        var inClass = false;
        var classContentStart = -1;
        var foundDelimiter = false;

        // 子集的边界只取决于转义、普通字符类和分组深度；禁止改变词法规则的扩展。
        while (!IsAtEnd)
        {
            var character = Current;
            if (character == '\\')
            {
                if (!inClass && (Peek(1) is >= '1' and <= '9' || Peek(1) == 'k'))
                    ReportUnsupportedRegexFeature("Backreferences are not supported.", 2);
                var escapeLength = Peek(1) == 'c' ? 3 : 2;
                _position += Math.Min(escapeLength, _source.Length - _position);
                continue;
            }

            if (inClass)
            {
                if (character == '[')
                    ReportUnsupportedRegexFeature("Nested and subtracted character classes are not supported; escape literal '['.", 1);
                if (character == ']')
                {
                    if (_position == classContentStart)
                        ReportUnsupportedRegexFeature("A literal ']' in a character class must be escaped.", 1);
                    inClass = false;
                }

                _position++;
                continue;
            }

            if (character == '[')
            {
                inClass = true;
                classContentStart = _position + (Peek(1) == '^' ? 2 : 1);
                _position++;
                continue;
            }

            if (character == '(')
            {
                if (Peek(1) == '?' && Peek(2) != ':')
                    ReportUnsupportedRegexFeature("Only ordinary groups and '(?:...)' groups are supported.", 2);
                groupDepth++;
            }
            else if (character == ')')
            {
                if (groupDepth == 0)
                {
                    foundDelimiter = true;
                    break;
                }

                groupDepth--;
            }

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

    private void ReportUnsupportedRegexFeature(string message, int length)
    {
        _diagnostics.Add(
            DiagnosticCodes.UnsupportedRegexFeature,
            DiagnosticSeverity.Error,
            message,
            new TextSpan(_position, length));
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
        while (!IsAtEnd && IsAsciiDigit(Current)) _position++;

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
            if (character is '\r' or '\n')
            {
                _diagnostics.Add(DiagnosticCodes.InvalidEscapeSequence, DiagnosticSeverity.Error,
                    "A string escape cannot cross a physical line break.", new TextSpan(escapeStart, 1));
                break;
            }
            _position++;
            switch (character)
            {
                case 'u':
                case 'U':
                case 'x':
                    LexUnicodeEscape(value, character, escapeStart);
                    break;
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

    private static bool IsAsciiDigit(char character) => character is >= '0' and <= '9';

    private void LexUnicodeEscape(StringBuilder value, char escape, int escapeStart)
    {
        var maximum = escape == 'U' ? 8 : 4;
        var minimum = escape == 'x' ? 1 : maximum;
        var count = 0;
        uint scalar = 0;
        while (!IsAtEnd && count < maximum)
        {
            var digit = Current switch
            {
                >= '0' and <= '9' => Current - '0',
                >= 'a' and <= 'f' => Current - 'a' + 10,
                >= 'A' and <= 'F' => Current - 'A' + 10,
                _ => -1
            };
            if (digit < 0) break;
            scalar = (scalar << 4) | (uint)digit;
            _position++;
            count++;
        }
        if (count < minimum || scalar > 0x10FFFF)
        {
            _diagnostics.Add(DiagnosticCodes.InvalidEscapeSequence, DiagnosticSeverity.Error,
                "Invalid Unicode string escape sequence.", new TextSpan(escapeStart, _position - escapeStart));
            return;
        }
        // BMP 转义保留 UTF-16 代码单元；补充平面码点按代理对写入字符串。
        if (scalar <= 0xFFFF) value.Append((char)scalar);
        else value.Append(char.ConvertFromUtf32((int)scalar));
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
        return character == '_' || char.IsLetter(character) ||
               char.GetUnicodeCategory(character) == UnicodeCategory.LetterNumber;
    }

    private static bool IsIdentifierPart(char character)
    {
        if (IsIdentifierStart(character) || char.IsDigit(character)) return true;

        return char.GetUnicodeCategory(character) is
            UnicodeCategory.ConnectorPunctuation or
            UnicodeCategory.NonSpacingMark or
            UnicodeCategory.SpacingCombiningMark or
            UnicodeCategory.Format;
    }
}
