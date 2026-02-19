using System.Text;

namespace Spectara.Revela.Commands.Generate.Filtering;

/// <summary>
/// Tokenizes filter expressions into a sequence of tokens.
/// </summary>
/// <remarks>
/// Supported syntax:
/// <list type="bullet">
/// <item>Identifiers: property names, function names (alphanumeric + underscore)</item>
/// <item>String literals: 'value' or "value"</item>
/// <item>Numbers: integers and decimals</item>
/// <item>Operators: ==, !=, &lt;, &lt;=, &gt;, &gt;=</item>
/// <item>Keywords: and, or, not, true, false, null</item>
/// <item>Punctuation: (, ), ., ,</item>
/// </list>
/// </remarks>
internal sealed class FilterLexer
{
    private readonly string source;
    private int position;
    private readonly List<Token> tokens = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterLexer"/> class.
    /// </summary>
    /// <param name="source">The filter expression to tokenize.</param>
    public FilterLexer(string source) => this.source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Tokenizes the source string into a list of tokens.
    /// </summary>
    /// <returns>The list of tokens.</returns>
    /// <exception cref="FilterParseException">Thrown when an invalid character or token is encountered.</exception>
    public IReadOnlyList<Token> Tokenize()
    {
        tokens.Clear();
        position = 0;

        while (!IsAtEnd())
        {
            SkipWhitespace();
            if (IsAtEnd())
            {
                break;
            }

            var token = ScanToken();
            tokens.Add(token);
        }

        tokens.Add(Token.Eof(position));
        return tokens;
    }

    private Token ScanToken()
    {
        var startPos = position;
        var c = Advance();

        return c switch
        {
            '(' => new Token(TokenType.LeftParen, "(", startPos),
            ')' => new Token(TokenType.RightParen, ")", startPos),
            '.' => new Token(TokenType.Dot, ".", startPos),
            ',' => new Token(TokenType.Comma, ",", startPos),
            '|' => new Token(TokenType.Pipe, "|", startPos),
            '=' when Match('=') => new Token(TokenType.Equal, "==", startPos),
            '!' when Match('=') => new Token(TokenType.NotEqual, "!=", startPos),
            '<' when Match('=') => new Token(TokenType.LessThanOrEqual, "<=", startPos),
            '<' => new Token(TokenType.LessThan, "<", startPos),
            '>' when Match('=') => new Token(TokenType.GreaterThanOrEqual, ">=", startPos),
            '>' => new Token(TokenType.GreaterThan, ">", startPos),
            '\'' or '"' => ScanString(c, startPos),
            _ when char.IsDigit(c) => ScanNumber(startPos),
            _ when IsIdentifierStart(c) => ScanIdentifier(startPos),
            _ => throw new FilterParseException($"Unexpected character '{c}'", startPos)
        };
    }

    private Token ScanString(char quote, int startPos)
    {
        var sb = new StringBuilder();

        while (!IsAtEnd() && Peek() != quote)
        {
            var c = Advance();
            if (c == '\\' && !IsAtEnd())
            {
                // Handle escape sequences
                var escaped = Advance();
                sb.Append(escaped switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '\\' => '\\',
                    '\'' => '\'',
                    '"' => '"',
                    _ => escaped
                });
            }
            else
            {
                sb.Append(c);
            }
        }

        if (IsAtEnd())
        {
            throw new FilterParseException($"Unterminated string starting at position {startPos}", startPos);
        }

        Advance(); // consume closing quote
        return new Token(TokenType.StringLiteral, sb.ToString(), startPos);
    }

    private Token ScanNumber(int startPos)
    {
        // Back up to include the first digit
        position--;

        while (!IsAtEnd() && char.IsDigit(Peek()))
        {
            Advance();
        }

        var isDecimal = false;
        if (!IsAtEnd() && Peek() == '.' && position + 1 < source.Length && char.IsDigit(source[position + 1]))
        {
            isDecimal = true;
            Advance(); // consume '.'
            while (!IsAtEnd() && char.IsDigit(Peek()))
            {
                Advance();
            }
        }

        var value = source[startPos..position];
        return new Token(isDecimal ? TokenType.DecimalLiteral : TokenType.IntegerLiteral, value, startPos);
    }

    private Token ScanIdentifier(int startPos)
    {
        // Back up to include the first character
        position--;

        while (!IsAtEnd() && IsIdentifierChar(Peek()))
        {
            Advance();
        }

        var value = source[startPos..position];

        // Check for keywords (case-insensitive)
        var tokenType = value.ToUpperInvariant() switch
        {
            "AND" => TokenType.And,
            "OR" => TokenType.Or,
            "NOT" => TokenType.Not,
            "TRUE" => TokenType.True,
            "FALSE" => TokenType.False,
            "NULL" => TokenType.Null,
            "ALL" => TokenType.All,
            "SORT" => TokenType.Sort,
            "LIMIT" => TokenType.Limit,
            "ASC" => TokenType.Asc,
            "DESC" => TokenType.Desc,
            _ => TokenType.Identifier
        };

        return new Token(tokenType, value, startPos);
    }

    private void SkipWhitespace()
    {
        while (!IsAtEnd() && char.IsWhiteSpace(Peek()))
        {
            position++;
        }
    }

    private bool IsAtEnd() => position >= source.Length;

    private char Peek() => source[position];

    private char Advance() => source[position++];

    private bool Match(char expected)
    {
        if (IsAtEnd() || source[position] != expected)
        {
            return false;
        }

        position++;
        return true;
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
