using Spectara.Revela.Commands.Generate.Filtering;
using Spectara.Revela.Commands.Generate.Filtering.Ast;
using Spectara.Revela.Core.Configuration;

namespace Spectara.Revela.Commands.Tests.Generate.Filtering;

/// <summary>
/// Tests for the <see cref="FilterParser"/> class.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class FilterParserTests
{
    /// <summary>
    /// Helper to parse a filter and get the predicate (for tests that don't use sort/limit).
    /// </summary>
    private static FilterNode ParsePredicate(string filter)
    {
        var tokens = new FilterLexer(filter).Tokenize();
        var parser = new FilterParser(tokens, filter);
        var query = parser.Parse();
        return query.Predicate ?? throw new InvalidOperationException("Expected predicate");
    }

    /// <summary>
    /// Helper to parse a filter and get the full query.
    /// </summary>
    private static FilterQuery ParseQuery(string filter)
    {
        var tokens = new FilterLexer(filter).Tokenize();
        var parser = new FilterParser(tokens, filter);
        return parser.Parse();
    }

    [TestMethod]
    public void Parse_SimpleComparison_ReturnsCorrectAst()
    {
        // Arrange & Act
        var result = ParsePredicate("filename == 'test.jpg'");

        // Assert
        Assert.IsInstanceOfType<BinaryNode>(result);
        var binary = (BinaryNode)result;
        Assert.AreEqual(BinaryOperator.Equal, binary.Operator);
        Assert.IsInstanceOfType<PropertyNode>(binary.Left);
        Assert.IsInstanceOfType<ConstantNode>(binary.Right);

        var left = (PropertyNode)binary.Left;
        Assert.HasCount(1, left.Path);
        Assert.AreEqual("filename", left.Path[0]);

        var right = (ConstantNode)binary.Right;
        Assert.AreEqual("test.jpg", right.Value);
    }

    [TestMethod]
    public void Parse_PropertyPath_ReturnsPropertyNode()
    {
        // Arrange & Act
        var result = ParsePredicate("exif.make == 'Canon'");

        // Assert
        var binary = (BinaryNode)result;
        var left = (PropertyNode)binary.Left;
        Assert.HasCount(2, left.Path);
        Assert.AreEqual("exif", left.Path[0]);
        Assert.AreEqual("make", left.Path[1]);
    }

    [TestMethod]
    public void Parse_NestedPropertyPath_ReturnsPropertyNode()
    {
        // Arrange & Act
        var result = ParsePredicate("exif.raw.key == 'value'");

        // Assert
        var binary = (BinaryNode)result;
        var left = (PropertyNode)binary.Left;
        Assert.HasCount(3, left.Path);
        Assert.AreEqual("exif", left.Path[0]);
        Assert.AreEqual("raw", left.Path[1]);
        Assert.AreEqual("key", left.Path[2]);
    }

    [TestMethod]
    [DataRow("a == b", BinaryOperator.Equal)]
    [DataRow("a != b", BinaryOperator.NotEqual)]
    [DataRow("a < b", BinaryOperator.LessThan)]
    [DataRow("a <= b", BinaryOperator.LessThanOrEqual)]
    [DataRow("a > b", BinaryOperator.GreaterThan)]
    [DataRow("a >= b", BinaryOperator.GreaterThanOrEqual)]
    public void Parse_ComparisonOperators_ReturnsCorrectOperator(string filter, BinaryOperator expected)
    {
        // Arrange & Act
        var result = ParsePredicate(filter);

        // Assert
        var binary = (BinaryNode)result;
        Assert.AreEqual(expected, binary.Operator);
    }

    [TestMethod]
    public void Parse_AndExpression_ReturnsCorrectAst()
    {
        // Arrange & Act
        var result = ParsePredicate("a == 1 and b == 2");

        // Assert
        Assert.IsInstanceOfType<BinaryNode>(result);
        var binary = (BinaryNode)result;
        Assert.AreEqual(BinaryOperator.And, binary.Operator);
        Assert.IsInstanceOfType<BinaryNode>(binary.Left);
        Assert.IsInstanceOfType<BinaryNode>(binary.Right);
    }

    [TestMethod]
    public void Parse_OrExpression_ReturnsCorrectAst()
    {
        // Arrange & Act
        var result = ParsePredicate("a == 1 or b == 2");

        // Assert
        var binary = (BinaryNode)result;
        Assert.AreEqual(BinaryOperator.Or, binary.Operator);
    }

    [TestMethod]
    public void Parse_AndPrecedenceOverOr_ReturnsCorrectTree()
    {
        // Arrange: a == 1 or b == 2 and c == 3 should be: a == 1 or (b == 2 and c == 3)
        var result = ParsePredicate("a == 1 or b == 2 and c == 3");

        // Assert
        var orNode = (BinaryNode)result;
        Assert.AreEqual(BinaryOperator.Or, orNode.Operator);
        // Left should be simple comparison: a == 1
        Assert.IsInstanceOfType<BinaryNode>(orNode.Left);
        var leftComparison = (BinaryNode)orNode.Left;
        Assert.AreEqual(BinaryOperator.Equal, leftComparison.Operator);
        // Right should be AND: b == 2 and c == 3
        Assert.IsInstanceOfType<BinaryNode>(orNode.Right);
        var andNode = (BinaryNode)orNode.Right;
        Assert.AreEqual(BinaryOperator.And, andNode.Operator);
    }

    [TestMethod]
    public void Parse_NotExpression_ReturnsUnaryNode()
    {
        // Arrange & Act
        var result = ParsePredicate("not a == 1");

        // Assert
        Assert.IsInstanceOfType<UnaryNode>(result);
        var unary = (UnaryNode)result;
        Assert.AreEqual(UnaryOperator.Not, unary.Operator);
        Assert.IsInstanceOfType<BinaryNode>(unary.Operand);
    }

    [TestMethod]
    public void Parse_ParenthesizedExpression_ReturnsCorrectTree()
    {
        // Arrange: (a == 1 or b == 2) and c == 3 should be: (a == 1 or b == 2) and c == 3
        var result = ParsePredicate("(a == 1 or b == 2) and c == 3");

        // Assert
        var andNode = (BinaryNode)result;
        Assert.AreEqual(BinaryOperator.And, andNode.Operator);
        // Left should be OR: a == 1 or b == 2
        Assert.IsInstanceOfType<BinaryNode>(andNode.Left);
        var orNode = (BinaryNode)andNode.Left;
        Assert.AreEqual(BinaryOperator.Or, orNode.Operator);
        // Right should be simple comparison: c == 3
        Assert.IsInstanceOfType<BinaryNode>(andNode.Right);
    }

    [TestMethod]
    public void Parse_FunctionCall_ReturnsCallNode()
    {
        // Arrange & Act
        var result = ParsePredicate("year(dateTaken) == 2024");

        // Assert
        var binary = (BinaryNode)result;
        Assert.IsInstanceOfType<CallNode>(binary.Left);
        var call = (CallNode)binary.Left;
        Assert.AreEqual("year", call.FunctionName);
        Assert.HasCount(1, call.Arguments);
        Assert.IsInstanceOfType<PropertyNode>(call.Arguments[0]);
    }

    [TestMethod]
    public void Parse_FunctionWithMultipleArguments_ReturnsCallNode()
    {
        // Arrange & Act
        var result = ParsePredicate("contains(filename, 'test')");

        // Assert
        Assert.IsInstanceOfType<CallNode>(result);
        var call = (CallNode)result;
        Assert.AreEqual("contains", call.FunctionName);
        Assert.HasCount(2, call.Arguments);
        Assert.IsInstanceOfType<PropertyNode>(call.Arguments[0]);
        Assert.IsInstanceOfType<ConstantNode>(call.Arguments[1]);
    }

    [TestMethod]
    public void Parse_BooleanLiteral_ReturnsConstantNode()
    {
        // Arrange & Act
        var result = ParsePredicate("true");

        // Assert
        Assert.IsInstanceOfType<ConstantNode>(result);
        var constant = (ConstantNode)result;
        Assert.IsTrue((bool)constant.Value!);
    }

    [TestMethod]
    public void Parse_NullLiteral_ReturnsConstantNode()
    {
        // Arrange & Act
        var result = ParsePredicate("filename == null");

        // Assert
        var binary = (BinaryNode)result;
        Assert.IsInstanceOfType<ConstantNode>(binary.Right);
        var constant = (ConstantNode)binary.Right;
        Assert.IsNull(constant.Value);
    }

    [TestMethod]
    public void Parse_IntegerLiteral_ReturnsConstantNode()
    {
        // Arrange & Act
        var result = ParsePredicate("exif.iso == 800");

        // Assert
        var binary = (BinaryNode)result;
        var constant = (ConstantNode)binary.Right;
        Assert.AreEqual(800, constant.Value);
    }

    [TestMethod]
    public void Parse_DecimalLiteral_ReturnsConstantNode()
    {
        // Arrange & Act
        var result = ParsePredicate("value == 3.14");

        // Assert
        var binary = (BinaryNode)result;
        var constant = (ConstantNode)binary.Right;
        Assert.AreEqual(3.14, constant.Value);
    }

    [TestMethod]
    public void Parse_UnexpectedToken_ThrowsException()
    {
        // Arrange
        var filter = "filename ==";
        var tokens = new FilterLexer(filter).Tokenize();
        var parser = new FilterParser(tokens, filter);

        // Act & Assert
        var ex = Assert.ThrowsExactly<FilterParseException>(() => parser.Parse());
        Assert.Contains("Expected", ex.Message);
    }

    [TestMethod]
    public void Parse_MissingClosingParen_ThrowsException()
    {
        // Arrange
        var filter = "(a == 1";
        var tokens = new FilterLexer(filter).Tokenize();
        var parser = new FilterParser(tokens, filter);

        // Act & Assert
        var ex = Assert.ThrowsExactly<FilterParseException>(() => parser.Parse());
        Assert.Contains(")", ex.Message);
    }

    [TestMethod]
    public void Parse_UnconsumedTokens_ThrowsException()
    {
        // Arrange
        var filter = "a == 1 b";
        var tokens = new FilterLexer(filter).Tokenize();
        var parser = new FilterParser(tokens, filter);

        // Act & Assert
        var ex = Assert.ThrowsExactly<FilterParseException>(() => parser.Parse());
        Assert.Contains("Unexpected", ex.Message);
    }

    #region Sort and Limit Tests

    [TestMethod]
    public void Parse_AllKeyword_ReturnsNullPredicate()
    {
        // Arrange & Act
        var query = ParseQuery("all");

        // Assert
        Assert.IsNull(query.Predicate);
        Assert.IsTrue(query.SelectsAll);
    }

    [TestMethod]
    public void Parse_AllWithSort_ReturnsSortClause()
    {
        // Arrange & Act
        var query = ParseQuery("all | sort dateTaken desc");

        // Assert
        Assert.IsNull(query.Predicate);
        Assert.IsNotNull(query.Sort);
        Assert.HasCount(1, query.Sort.PropertyPath);
        Assert.AreEqual("dateTaken", query.Sort.PropertyPath[0]);
        Assert.AreEqual(SortDirection.Desc, query.Sort.Direction);
    }

    [TestMethod]
    public void Parse_AllWithSortAsc_ReturnsAscending()
    {
        // Arrange & Act
        var query = ParseQuery("all | sort filename asc");

        // Assert
        Assert.IsNotNull(query.Sort);
        Assert.AreEqual(SortDirection.Asc, query.Sort.Direction);
    }

    [TestMethod]
    public void Parse_SortWithoutDirection_DefaultsToAsc()
    {
        // Arrange & Act
        var query = ParseQuery("all | sort filename");

        // Assert
        Assert.IsNotNull(query.Sort);
        Assert.AreEqual(SortDirection.Asc, query.Sort.Direction);
    }

    [TestMethod]
    public void Parse_SortWithNestedProperty_ReturnsPropertyPath()
    {
        // Arrange & Act
        var query = ParseQuery("all | sort exif.iso desc");

        // Assert
        Assert.IsNotNull(query.Sort);
        Assert.HasCount(2, query.Sort.PropertyPath);
        Assert.AreEqual("exif", query.Sort.PropertyPath[0]);
        Assert.AreEqual("iso", query.Sort.PropertyPath[1]);
        Assert.AreEqual("exif.iso", query.Sort.PropertyPathString);
    }

    [TestMethod]
    public void Parse_Limit_ReturnsLimitValue()
    {
        // Arrange & Act
        var query = ParseQuery("all | limit 5");

        // Assert
        Assert.IsNull(query.Sort);
        Assert.AreEqual(5, query.Limit);
        Assert.IsTrue(query.HasLimit);
    }

    [TestMethod]
    public void Parse_SortAndLimit_ReturnsBoth()
    {
        // Arrange & Act
        var query = ParseQuery("all | sort dateTaken desc | limit 10");

        // Assert
        Assert.IsNotNull(query.Sort);
        Assert.AreEqual("dateTaken", query.Sort.PropertyPath[0]);
        Assert.AreEqual(SortDirection.Desc, query.Sort.Direction);
        Assert.AreEqual(10, query.Limit);
    }

    [TestMethod]
    public void Parse_FilterWithSortAndLimit_ReturnsFull()
    {
        // Arrange & Act
        var query = ParseQuery("exif.make == 'Canon' | sort dateTaken desc | limit 5");

        // Assert
        Assert.IsNotNull(query.Predicate);
        Assert.IsInstanceOfType<BinaryNode>(query.Predicate);
        Assert.IsNotNull(query.Sort);
        Assert.AreEqual(5, query.Limit);
    }

    [TestMethod]
    public void Parse_LimitZero_ThrowsException()
    {
        // Arrange
        var filter = "all | limit 0";
        var tokens = new FilterLexer(filter).Tokenize();
        var parser = new FilterParser(tokens, filter);

        // Act & Assert
        var ex = Assert.ThrowsExactly<FilterParseException>(() => parser.Parse());
        Assert.Contains("positive", ex.Message);
    }

    [TestMethod]
    public void Parse_LimitNegative_ThrowsException()
    {
        // Arrange - negative numbers will fail in lexer because '-' is not a recognized token
        var filter = "all | limit -5";

        // Act & Assert - Lexer throws because '-' is unexpected
        var ex = Assert.ThrowsExactly<FilterParseException>(() =>
        {
            var tokens = new FilterLexer(filter).Tokenize();
            var parser = new FilterParser(tokens, filter);
            parser.Parse();
        });
        Assert.Contains("-", ex.Message);
    }

    #endregion
}
