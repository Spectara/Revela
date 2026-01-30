using System.Globalization;

using Spectara.Revela.Commands.Generate.Filtering;

namespace Spectara.Revela.Commands.Tests.Generate.Filtering;

/// <summary>
/// Tests for the <see cref="FilterLexer"/> class.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class FilterLexerTests
{
    [TestMethod]
    public void Tokenize_EmptyString_ReturnsOnlyEof()
    {
        // Arrange
        var lexer = new FilterLexer("");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        Assert.HasCount(1, tokens);
        Assert.AreEqual(TokenType.Eof, tokens[0].Type);
    }

    [TestMethod]
    public void Tokenize_Whitespace_ReturnsOnlyEof()
    {
        // Arrange
        var lexer = new FilterLexer("   \t\n  ");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        Assert.HasCount(1, tokens);
        Assert.AreEqual(TokenType.Eof, tokens[0].Type);
    }

    [TestMethod]
    public void Tokenize_Identifier_ReturnsIdentifierToken()
    {
        // Arrange
        var lexer = new FilterLexer("filename");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        Assert.HasCount(2, tokens); // identifier + EOF
        Assert.AreEqual(TokenType.Identifier, tokens[0].Type);
        Assert.AreEqual("filename", tokens[0].Value);
    }

    [TestMethod]
    public void Tokenize_PropertyPath_ReturnsDotsAndIdentifiers()
    {
        // Arrange
        var lexer = new FilterLexer("exif.make");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        Assert.HasCount(4, tokens); // exif, ., make, EOF
        Assert.AreEqual(TokenType.Identifier, tokens[0].Type);
        Assert.AreEqual("exif", tokens[0].Value);
        Assert.AreEqual(TokenType.Dot, tokens[1].Type);
        Assert.AreEqual(TokenType.Identifier, tokens[2].Type);
        Assert.AreEqual("make", tokens[2].Value);
    }

    [TestMethod]
    [DataRow("'hello'", "hello")]
    [DataRow("\"hello\"", "hello")]
    [DataRow("'with spaces'", "with spaces")]
    [DataRow("\"with spaces\"", "with spaces")]
    public void Tokenize_StringLiteral_ReturnsStringToken(string input, string expected)
    {
        // Arrange
        var lexer = new FilterLexer(input);

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        Assert.HasCount(2, tokens);
        Assert.AreEqual(TokenType.StringLiteral, tokens[0].Type);
        Assert.AreEqual(expected, tokens[0].Value);
    }

    [TestMethod]
    public void Tokenize_EscapedQuotes_ReturnsCorrectString()
    {
        // Arrange
        var lexer = new FilterLexer(@"'It\'s escaped'");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        Assert.HasCount(2, tokens);
        Assert.AreEqual(TokenType.StringLiteral, tokens[0].Type);
        Assert.AreEqual("It's escaped", tokens[0].Value);
    }

    [TestMethod]
    [DataRow("123", 123)]
    [DataRow("0", 0)]
    [DataRow("42", 42)]
    public void Tokenize_IntegerLiteral_ReturnsIntegerToken(string input, int expected)
    {
        // Arrange
        var lexer = new FilterLexer(input);

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        Assert.HasCount(2, tokens);
        Assert.AreEqual(TokenType.IntegerLiteral, tokens[0].Type);
        Assert.AreEqual(expected.ToString(CultureInfo.InvariantCulture), tokens[0].Value);
    }

    [TestMethod]
    [DataRow("3.14")]
    [DataRow("0.5")]
    [DataRow("100.0")]
    public void Tokenize_DecimalLiteral_ReturnsDecimalToken(string input)
    {
        // Arrange
        var lexer = new FilterLexer(input);

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        Assert.HasCount(2, tokens);
        Assert.AreEqual(TokenType.DecimalLiteral, tokens[0].Type);
    }

    [TestMethod]
    [DataRow("==", TokenType.Equal)]
    [DataRow("!=", TokenType.NotEqual)]
    [DataRow("<", TokenType.LessThan)]
    [DataRow("<=", TokenType.LessThanOrEqual)]
    [DataRow(">", TokenType.GreaterThan)]
    [DataRow(">=", TokenType.GreaterThanOrEqual)]
    public void Tokenize_ComparisonOperators_ReturnsCorrectTokens(string input, TokenType expected)
    {
        // Arrange
        var lexer = new FilterLexer(input);

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        Assert.HasCount(2, tokens);
        Assert.AreEqual(expected, tokens[0].Type);
    }

    [TestMethod]
    [DataRow("and", TokenType.And)]
    [DataRow("AND", TokenType.And)]
    [DataRow("And", TokenType.And)]
    [DataRow("or", TokenType.Or)]
    [DataRow("OR", TokenType.Or)]
    [DataRow("not", TokenType.Not)]
    [DataRow("NOT", TokenType.Not)]
    public void Tokenize_LogicalKeywords_ReturnsCorrectTokens(string input, TokenType expected)
    {
        // Arrange
        var lexer = new FilterLexer(input);

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        Assert.HasCount(2, tokens);
        Assert.AreEqual(expected, tokens[0].Type);
    }

    [TestMethod]
    [DataRow("true", TokenType.True)]
    [DataRow("TRUE", TokenType.True)]
    [DataRow("false", TokenType.False)]
    [DataRow("FALSE", TokenType.False)]
    [DataRow("null", TokenType.Null)]
    [DataRow("NULL", TokenType.Null)]
    public void Tokenize_LiteralKeywords_ReturnsCorrectTokens(string input, TokenType expected)
    {
        // Arrange
        var lexer = new FilterLexer(input);

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        Assert.HasCount(2, tokens);
        Assert.AreEqual(expected, tokens[0].Type);
    }

    [TestMethod]
    public void Tokenize_Parentheses_ReturnsCorrectTokens()
    {
        // Arrange
        var lexer = new FilterLexer("()");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        Assert.HasCount(3, tokens);
        Assert.AreEqual(TokenType.LeftParen, tokens[0].Type);
        Assert.AreEqual(TokenType.RightParen, tokens[1].Type);
    }

    [TestMethod]
    public void Tokenize_Comma_ReturnsCommaToken()
    {
        // Arrange
        var lexer = new FilterLexer(",");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        Assert.HasCount(2, tokens);
        Assert.AreEqual(TokenType.Comma, tokens[0].Type);
    }

    [TestMethod]
    public void Tokenize_ComplexExpression_ReturnsAllTokens()
    {
        // Arrange
        var lexer = new FilterLexer("exif.make == 'Canon' and year(dateTaken) >= 2024");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        // exif . make == 'Canon' and year ( dateTaken ) >= 2024 EOF
        Assert.HasCount(13, tokens);
        Assert.AreEqual(TokenType.Identifier, tokens[0].Type);
        Assert.AreEqual("exif", tokens[0].Value);
        Assert.AreEqual(TokenType.Dot, tokens[1].Type);
        Assert.AreEqual(TokenType.Identifier, tokens[2].Type);
        Assert.AreEqual("make", tokens[2].Value);
        Assert.AreEqual(TokenType.Equal, tokens[3].Type);
        Assert.AreEqual(TokenType.StringLiteral, tokens[4].Type);
        Assert.AreEqual("Canon", tokens[4].Value);
        Assert.AreEqual(TokenType.And, tokens[5].Type);
        Assert.AreEqual(TokenType.Identifier, tokens[6].Type);
        Assert.AreEqual("year", tokens[6].Value);
        Assert.AreEqual(TokenType.LeftParen, tokens[7].Type);
        Assert.AreEqual(TokenType.Identifier, tokens[8].Type);
        Assert.AreEqual("dateTaken", tokens[8].Value);
        Assert.AreEqual(TokenType.RightParen, tokens[9].Type);
        Assert.AreEqual(TokenType.GreaterThanOrEqual, tokens[10].Type);
        Assert.AreEqual(TokenType.IntegerLiteral, tokens[11].Type);
        Assert.AreEqual("2024", tokens[11].Value);
        Assert.AreEqual(TokenType.Eof, tokens[12].Type);
    }

    [TestMethod]
    public void Tokenize_PositionsAreCorrect()
    {
        // Arrange
        var lexer = new FilterLexer("a == b");

        // Act
        var tokens = lexer.Tokenize();

        // Assert
        Assert.AreEqual(0, tokens[0].Position); // a
        Assert.AreEqual(2, tokens[1].Position); // ==
        Assert.AreEqual(5, tokens[2].Position); // b
    }

    [TestMethod]
    public void Tokenize_UnterminatedString_ThrowsException()
    {
        // Arrange
        var lexer = new FilterLexer("'unterminated");

        // Act & Assert
        var ex = Assert.ThrowsExactly<FilterParseException>(() => lexer.Tokenize());
        Assert.Contains("Unterminated string", ex.Message);
    }

    [TestMethod]
    public void Tokenize_UnknownCharacter_ThrowsException()
    {
        // Arrange
        var lexer = new FilterLexer("@invalid");

        // Act & Assert
        var ex = Assert.ThrowsExactly<FilterParseException>(() => lexer.Tokenize());
        Assert.Contains("Unexpected character", ex.Message);
    }
}
