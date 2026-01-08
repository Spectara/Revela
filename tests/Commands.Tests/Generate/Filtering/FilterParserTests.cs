using Spectara.Revela.Commands.Generate.Filtering;
using Spectara.Revela.Commands.Generate.Filtering.Ast;

namespace Spectara.Revela.Commands.Tests.Generate.Filtering;

/// <summary>
/// Tests for the <see cref="FilterParser"/> class.
/// </summary>
[TestClass]
public sealed class FilterParserTests
{
    [TestMethod]
    public void Parse_SimpleComparison_ReturnsCorrectAst()
    {
        // Arrange
        var filter = "filename == 'test.jpg'";
        var tokens = new FilterLexer(filter).Tokenize();
        var parser = new FilterParser(tokens, filter);

        // Act
        var result = parser.Parse();

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
        // Arrange
        var filter = "exif.make == 'Canon'";
        var tokens = new FilterLexer(filter).Tokenize();
        var parser = new FilterParser(tokens, filter);

        // Act
        var result = parser.Parse();

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
        // Arrange
        var filter = "exif.raw.key == 'value'";
        var tokens = new FilterLexer(filter).Tokenize();
        var parser = new FilterParser(tokens, filter);

        // Act
        var result = parser.Parse();

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
        // Arrange
        var tokens = new FilterLexer(filter).Tokenize();
        var parser = new FilterParser(tokens, filter);

        // Act
        var result = parser.Parse();

        // Assert
        var binary = (BinaryNode)result;
        Assert.AreEqual(expected, binary.Operator);
    }

    [TestMethod]
    public void Parse_AndExpression_ReturnsCorrectAst()
    {
        // Arrange
        var filter = "a == 1 and b == 2";
        var tokens = new FilterLexer(filter).Tokenize();
        var parser = new FilterParser(tokens, filter);

        // Act
        var result = parser.Parse();

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
        // Arrange
        var filter = "a == 1 or b == 2";
        var tokens = new FilterLexer(filter).Tokenize();
        var parser = new FilterParser(tokens, filter);

        // Act
        var result = parser.Parse();

        // Assert
        var binary = (BinaryNode)result;
        Assert.AreEqual(BinaryOperator.Or, binary.Operator);
    }

    [TestMethod]
    public void Parse_AndPrecedenceOverOr_ReturnsCorrectTree()
    {
        // Arrange: a == 1 or b == 2 and c == 3 should be: a == 1 or (b == 2 and c == 3)
        var filter = "a == 1 or b == 2 and c == 3";
        var tokens = new FilterLexer(filter).Tokenize();
        var parser = new FilterParser(tokens, filter);

        // Act
        var result = parser.Parse();

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
        // Arrange
        var filter = "not a == 1";
        var tokens = new FilterLexer(filter).Tokenize();
        var parser = new FilterParser(tokens, filter);

        // Act
        var result = parser.Parse();

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
        var filter = "(a == 1 or b == 2) and c == 3";
        var tokens = new FilterLexer(filter).Tokenize();
        var parser = new FilterParser(tokens, filter);

        // Act
        var result = parser.Parse();

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
        // Arrange
        var filter = "year(dateTaken) == 2024";
        var tokens = new FilterLexer(filter).Tokenize();
        var parser = new FilterParser(tokens, filter);

        // Act
        var result = parser.Parse();

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
        // Arrange
        var filter = "contains(filename, 'test')";
        var tokens = new FilterLexer(filter).Tokenize();
        var parser = new FilterParser(tokens, filter);

        // Act
        var result = parser.Parse();

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
        // Arrange
        var filter = "true";
        var tokens = new FilterLexer(filter).Tokenize();
        var parser = new FilterParser(tokens, filter);

        // Act
        var result = parser.Parse();

        // Assert
        Assert.IsInstanceOfType<ConstantNode>(result);
        var constant = (ConstantNode)result;
        Assert.IsTrue((bool)constant.Value!);
    }

    [TestMethod]
    public void Parse_NullLiteral_ReturnsConstantNode()
    {
        // Arrange
        var filter = "filename == null";
        var tokens = new FilterLexer(filter).Tokenize();
        var parser = new FilterParser(tokens, filter);

        // Act
        var result = parser.Parse();

        // Assert
        var binary = (BinaryNode)result;
        Assert.IsInstanceOfType<ConstantNode>(binary.Right);
        var constant = (ConstantNode)binary.Right;
        Assert.IsNull(constant.Value);
    }

    [TestMethod]
    public void Parse_IntegerLiteral_ReturnsConstantNode()
    {
        // Arrange
        var filter = "exif.iso == 800";
        var tokens = new FilterLexer(filter).Tokenize();
        var parser = new FilterParser(tokens, filter);

        // Act
        var result = parser.Parse();

        // Assert
        var binary = (BinaryNode)result;
        var constant = (ConstantNode)binary.Right;
        Assert.AreEqual(800, constant.Value);
    }

    [TestMethod]
    public void Parse_DecimalLiteral_ReturnsConstantNode()
    {
        // Arrange
        var filter = "value == 3.14";
        var tokens = new FilterLexer(filter).Tokenize();
        var parser = new FilterParser(tokens, filter);

        // Act
        var result = parser.Parse();

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
}
