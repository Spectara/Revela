namespace Spectara.Revela.Commands.Generate.Filtering;

/// <summary>
/// Token types for the filter expression lexer.
/// </summary>
public enum TokenType
{
    /// <summary>End of input.</summary>
    Eof,

    /// <summary>Identifier (property name, function name, keyword).</summary>
    Identifier,

    /// <summary>String literal ('value' or "value").</summary>
    StringLiteral,

    /// <summary>Integer literal.</summary>
    IntegerLiteral,

    /// <summary>Decimal literal.</summary>
    DecimalLiteral,

    /// <summary>Dot operator for property access.</summary>
    Dot,

    /// <summary>Opening parenthesis.</summary>
    LeftParen,

    /// <summary>Closing parenthesis.</summary>
    RightParen,

    /// <summary>Comma for function arguments.</summary>
    Comma,

    /// <summary>Equal operator (==).</summary>
    Equal,

    /// <summary>Not equal operator (!=).</summary>
    NotEqual,

    /// <summary>Less than operator (&lt;).</summary>
    LessThan,

    /// <summary>Less than or equal operator (&lt;=).</summary>
    LessThanOrEqual,

    /// <summary>Greater than operator (&gt;).</summary>
    GreaterThan,

    /// <summary>Greater than or equal operator (&gt;=).</summary>
    GreaterThanOrEqual,

    /// <summary>Logical AND keyword.</summary>
    And,

    /// <summary>Logical OR keyword.</summary>
    Or,

    /// <summary>Logical NOT keyword.</summary>
    Not,

    /// <summary>Boolean true literal.</summary>
    True,

    /// <summary>Boolean false literal.</summary>
    False,

    /// <summary>Null literal.</summary>
    Null,

    /// <summary>Pipe operator for chaining (|).</summary>
    Pipe,

    /// <summary>All keyword (selects all images).</summary>
    All,

    /// <summary>Sort keyword for ordering results.</summary>
    Sort,

    /// <summary>Limit keyword for restricting result count.</summary>
    Limit,

    /// <summary>Ascending sort direction.</summary>
    Asc,

    /// <summary>Descending sort direction.</summary>
    Desc
}

/// <summary>
/// Represents a token in the filter expression.
/// </summary>
/// <param name="Type">The type of the token.</param>
/// <param name="Value">The literal value of the token.</param>
/// <param name="Position">The position in the source string where the token starts.</param>
internal readonly record struct Token(TokenType Type, string Value, int Position)
{
    /// <summary>
    /// Creates an EOF token at the specified position.
    /// </summary>
    public static Token Eof(int position) => new(TokenType.Eof, string.Empty, position);

    /// <inheritdoc />
    public override string ToString() => Type switch
    {
        TokenType.Eof => "end of expression",
        TokenType.StringLiteral => $"'{Value}'",
        TokenType.IntegerLiteral or TokenType.DecimalLiteral => Value,
        TokenType.Identifier => Value,
        TokenType.Dot => ".",
        TokenType.LeftParen => "(",
        TokenType.RightParen => ")",
        TokenType.Comma => ",",
        TokenType.Equal => "==",
        TokenType.NotEqual => "!=",
        TokenType.LessThan => "<",
        TokenType.LessThanOrEqual => "<=",
        TokenType.GreaterThan => ">",
        TokenType.GreaterThanOrEqual => ">=",
        TokenType.And => "and",
        TokenType.Or => "or",
        TokenType.Not => "not",
        TokenType.True => "true",
        TokenType.False => "false",
        TokenType.Null => "null",
        TokenType.Pipe => "|",
        TokenType.All => "all",
        TokenType.Sort => "sort",
        TokenType.Limit => "limit",
        TokenType.Asc => "asc",
        TokenType.Desc => "desc",
        _ => $"<{Type}>"
    };
}
