using System.Globalization;

using Spectara.Revela.Commands.Generate.Filtering.Ast;
using Spectara.Revela.Core.Configuration;

namespace Spectara.Revela.Commands.Generate.Filtering;

/// <summary>
/// Parses a list of tokens into an AST using recursive descent.
/// </summary>
/// <remarks>
/// Grammar (simplified EBNF):
/// <code>
/// query          → filter_expr ("|" sort_clause)? ("|" limit_clause)?
///                | "all" ("|" sort_clause)? ("|" limit_clause)?
/// filter_expr    → or_expr
/// sort_clause    → "sort" property ("asc" | "desc")?
/// limit_clause   → "limit" INTEGER
/// or_expr        → and_expr ("or" and_expr)*
/// and_expr       → unary_expr ("and" unary_expr)*
/// unary_expr     → "not" unary_expr | comparison
/// comparison     → primary (comp_op primary)?
/// comp_op        → "==" | "!=" | "&lt;" | "&lt;=" | "&gt;" | "&gt;="
/// primary        → call | property | constant | "(" expression ")"
/// call           → IDENTIFIER "(" arguments? ")"
/// arguments      → expression ("," expression)*
/// property       → IDENTIFIER ("." IDENTIFIER)*
/// constant       → STRING | INTEGER | DECIMAL | "true" | "false" | "null"
/// </code>
/// </remarks>
public sealed class FilterParser
{
    private readonly IReadOnlyList<Token> tokens;
    private readonly string source;
    private int current;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterParser"/> class.
    /// </summary>
    /// <param name="tokens">The tokens to parse.</param>
    /// <param name="source">The original source string (for error messages).</param>
    public FilterParser(IReadOnlyList<Token> tokens, string source)
    {
        this.tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        this.source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>
    /// Parses the tokens into a filter query with optional sort and limit clauses.
    /// </summary>
    /// <returns>The complete filter query.</returns>
    /// <exception cref="FilterParseException">Thrown when the expression is invalid.</exception>
    public FilterQuery Parse()
    {
        if (tokens.Count == 0 || (tokens.Count == 1 && tokens[0].Type == TokenType.Eof))
        {
            throw CreateError("Empty filter expression", 0);
        }

        FilterNode? predicate;

        // Check for "all" keyword - selects all images
        if (Match(TokenType.All))
        {
            predicate = null;
        }
        else
        {
            predicate = ParseOrExpression();
        }

        // Parse optional pipe clauses
        var sort = ParseSortClause();
        var limit = ParseLimitClause();

        if (!IsAtEnd())
        {
            throw CreateError($"Unexpected token '{Current()}'", Current().Position);
        }

        return new FilterQuery(predicate, sort, limit);
    }

    /// <summary>
    /// Parses the tokens into an AST (legacy method for backward compatibility).
    /// </summary>
    /// <returns>The root node of the AST.</returns>
    /// <exception cref="FilterParseException">Thrown when the expression is invalid.</exception>
    /// <remarks>
    /// Use <see cref="Parse"/> instead to get the full query including sort and limit.
    /// This method throws if the expression contains pipe clauses.
    /// </remarks>
    public FilterNode ParseExpression()
    {
        var query = Parse();

        if (query.HasSort || query.HasLimit)
        {
            throw CreateError("Use Parse() method for expressions with sort or limit clauses", 0);
        }

        return query.Predicate ?? throw CreateError("'all' keyword requires sort or limit clause, or use 'true' instead", 0);
    }

    private SortClause? ParseSortClause()
    {
        if (!Match(TokenType.Pipe))
        {
            return null;
        }

        if (!Match(TokenType.Sort))
        {
            // Put back the pipe - might be for limit
            current--;
            return null;
        }

        // Parse property path for sort field
        if (!Check(TokenType.Identifier))
        {
            throw CreateError("Expected property name after 'sort'", Current().Position);
        }

        var firstSegment = Advance();
        var path = new List<string> { firstSegment.Value };

        while (Match(TokenType.Dot))
        {
            var segment = Consume(TokenType.Identifier, "Expected property name after '.'");
            path.Add(segment.Value);
        }

        // Parse optional direction (default: ascending)
        var direction = SortDirection.Asc;
        if (Match(TokenType.Desc))
        {
            direction = SortDirection.Desc;
        }
        else
        {
            Match(TokenType.Asc); // consume optional "asc"
        }

        return new SortClause(path, direction);
    }

    private int? ParseLimitClause()
    {
        if (!Match(TokenType.Pipe))
        {
            return null;
        }

        if (!Match(TokenType.Limit))
        {
            throw CreateError("Expected 'sort' or 'limit' after '|'", Current().Position);
        }

        if (!Check(TokenType.IntegerLiteral))
        {
            throw CreateError("Expected number after 'limit'", Current().Position);
        }

        var token = Advance();
        var limit = int.Parse(token.Value, CultureInfo.InvariantCulture);

        if (limit <= 0)
        {
            throw CreateError("Limit must be a positive number", token.Position);
        }

        return limit;
    }

    private FilterNode ParseOrExpression()
    {
        var left = ParseAndExpression();

        while (Match(TokenType.Or))
        {
            var op = Previous();
            var right = ParseAndExpression();
            left = new BinaryNode
            {
                Left = left,
                Operator = BinaryOperator.Or,
                Right = right,
                Position = op.Position
            };
        }

        return left;
    }

    private FilterNode ParseAndExpression()
    {
        var left = ParseUnaryExpression();

        while (Match(TokenType.And))
        {
            var op = Previous();
            var right = ParseUnaryExpression();
            left = new BinaryNode
            {
                Left = left,
                Operator = BinaryOperator.And,
                Right = right,
                Position = op.Position
            };
        }

        return left;
    }

    private FilterNode ParseUnaryExpression()
    {
        if (Match(TokenType.Not))
        {
            var op = Previous();
            var operand = ParseUnaryExpression();
            return new UnaryNode
            {
                Operator = UnaryOperator.Not,
                Operand = operand,
                Position = op.Position
            };
        }

        return ParseComparison();
    }

    private FilterNode ParseComparison()
    {
        var left = ParsePrimary();

        if (MatchAny(TokenType.Equal, TokenType.NotEqual, TokenType.LessThan,
                     TokenType.LessThanOrEqual, TokenType.GreaterThan, TokenType.GreaterThanOrEqual))
        {
            var op = Previous();
            var right = ParsePrimary();

#pragma warning disable IDE0072 // Populate switch - intentional, handled by default case
            var binaryOp = op.Type switch
            {
                TokenType.Equal => BinaryOperator.Equal,
                TokenType.NotEqual => BinaryOperator.NotEqual,
                TokenType.LessThan => BinaryOperator.LessThan,
                TokenType.LessThanOrEqual => BinaryOperator.LessThanOrEqual,
                TokenType.GreaterThan => BinaryOperator.GreaterThan,
                TokenType.GreaterThanOrEqual => BinaryOperator.GreaterThanOrEqual,
                _ => throw CreateError($"Unknown operator '{op.Value}'", op.Position)
            };
#pragma warning restore IDE0072

            return new BinaryNode
            {
                Left = left,
                Operator = binaryOp,
                Right = right,
                Position = op.Position
            };
        }

        return left;
    }

    private FilterNode ParsePrimary()
    {
        var token = Current();

        // Grouped expression
        if (Match(TokenType.LeftParen))
        {
            var expr = ParseOrExpression();
            Consume(TokenType.RightParen, "Expected ')' after expression");
            return expr;
        }

        // Constants
        if (Match(TokenType.StringLiteral))
        {
            return new ConstantNode { Value = Previous().Value, Position = Previous().Position };
        }

        if (Match(TokenType.IntegerLiteral))
        {
            var value = int.Parse(Previous().Value, CultureInfo.InvariantCulture);
            return new ConstantNode { Value = value, Position = Previous().Position };
        }

        if (Match(TokenType.DecimalLiteral))
        {
            var value = double.Parse(Previous().Value, CultureInfo.InvariantCulture);
            return new ConstantNode { Value = value, Position = Previous().Position };
        }

        if (Match(TokenType.True))
        {
            return new ConstantNode { Value = true, Position = Previous().Position };
        }

        if (Match(TokenType.False))
        {
            return new ConstantNode { Value = false, Position = Previous().Position };
        }

        if (Match(TokenType.Null))
        {
            return new ConstantNode { Value = null, Position = Previous().Position };
        }

        // Identifier - could be function call or property
        if (Match(TokenType.Identifier))
        {
            var identifier = Previous();

            // Function call
            if (Match(TokenType.LeftParen))
            {
                return ParseFunctionCall(identifier);
            }

            // Property access (possibly chained)
            return ParsePropertyAccess(identifier);
        }

        throw CreateError($"Expected expression, got '{token}'", token.Position);
    }

    private CallNode ParseFunctionCall(Token functionName)
    {
        var arguments = new List<FilterNode>();

        if (!Check(TokenType.RightParen))
        {
            do
            {
                arguments.Add(ParseOrExpression());
            }
            while (Match(TokenType.Comma));
        }

        Consume(TokenType.RightParen, $"Expected ')' after arguments to '{functionName.Value}'");

        return new CallNode
        {
            FunctionName = functionName.Value,
            Arguments = arguments,
            Position = functionName.Position
        };
    }

    private PropertyNode ParsePropertyAccess(Token firstSegment)
    {
        var path = new List<string> { firstSegment.Value };

        while (Match(TokenType.Dot))
        {
            var segment = Consume(TokenType.Identifier, "Expected property name after '.'");
            path.Add(segment.Value);
        }

        return new PropertyNode
        {
            Path = path,
            Position = firstSegment.Position
        };
    }

    private bool Match(TokenType type)
    {
        if (Check(type))
        {
            Advance();
            return true;
        }

        return false;
    }

    private bool MatchAny(params TokenType[] types)
    {
        foreach (var type in types)
        {
            if (Check(type))
            {
                Advance();
                return true;
            }
        }

        return false;
    }

    private bool Check(TokenType type) => !IsAtEnd() && Current().Type == type;

    private Token Advance()
    {
        if (!IsAtEnd())
        {
            current++;
        }

        return Previous();
    }

    private Token Current() => tokens[current];

    private Token Previous() => tokens[current - 1];

    private bool IsAtEnd() => Current().Type == TokenType.Eof;

    private Token Consume(TokenType type, string message)
    {
        if (Check(type))
        {
            return Advance();
        }

        throw CreateError(message, Current().Position);
    }

    private FilterParseException CreateError(string message, int position) =>
        new(message, position) { FilterExpression = source };
}
