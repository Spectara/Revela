using System.Linq.Expressions;
using System.Reflection;

using Spectara.Revela.Commands.Generate.Filtering.Ast;
using Spectara.Revela.Sdk.Models;
using Spectara.Revela.Sdk.Models.Manifest;

namespace Spectara.Revela.Commands.Generate.Filtering;

/// <summary>
/// Builds LINQ expressions from filter AST nodes.
/// </summary>
/// <remarks>
/// Supports filtering <see cref="ImageContent"/> with properties:
/// <list type="bullet">
/// <item><c>filename</c> - Image filename</item>
/// <item><c>width</c>, <c>height</c> - Dimensions</item>
/// <item><c>dateTaken</c> - Date the photo was taken</item>
/// <item><c>exif.*</c> - EXIF properties (make, model, iso, fNumber, etc.)</item>
/// <item><c>exif.raw.*</c> - Raw EXIF dictionary values</item>
/// </list>
/// </remarks>
public sealed class FilterExpressionBuilder : IFilterNodeVisitor<Expression>
{
    private readonly ParameterExpression parameter;
    private readonly string source;

    /// <summary>
    /// Supported functions and their implementations.
    /// </summary>
    private static readonly Dictionary<string, Func<FilterExpressionBuilder, CallNode, Expression>> Functions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["year"] = (b, n) => b.BuildDatePartFunction(n, "Year"),
        ["month"] = (b, n) => b.BuildDatePartFunction(n, "Month"),
        ["day"] = (b, n) => b.BuildDatePartFunction(n, "Day"),
        ["contains"] = (b, n) => b.BuildStringFunction(n, "Contains"),
        ["starts_with"] = (b, n) => b.BuildStringFunction(n, "StartsWith"),
        ["ends_with"] = (b, n) => b.BuildStringFunction(n, "EndsWith"),
        ["lower"] = (b, n) => b.BuildToLowerFunction(n),
        ["upper"] = (b, n) => b.BuildToUpperFunction(n)
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterExpressionBuilder"/> class.
    /// </summary>
    /// <param name="source">The original filter string for error messages.</param>
    public FilterExpressionBuilder(string source)
    {
        this.source = source;
        parameter = Expression.Parameter(typeof(ImageContent), "img");
    }

    /// <summary>
    /// Builds a filter expression from the AST.
    /// </summary>
    /// <param name="ast">The root AST node.</param>
    /// <returns>A compiled predicate for filtering images.</returns>
    public Expression<Func<ImageContent, bool>> Build(FilterNode ast)
    {
        var body = ast.Accept(this);

        // Ensure result is boolean
        if (body.Type != typeof(bool))
        {
            throw CreateError("Filter expression must evaluate to a boolean", ast.Position);
        }

        return Expression.Lambda<Func<ImageContent, bool>>(body, parameter);
    }

    /// <inheritdoc />
    public Expression Visit(BinaryNode node)
    {
        var left = node.Left.Accept(this);
        var right = node.Right.Accept(this);

        // Handle logical operators
        if (node.Operator is BinaryOperator.And or BinaryOperator.Or)
        {
            // Ensure both sides are boolean
            if (left.Type != typeof(bool))
            {
                throw CreateError("Left side of logical operator must be boolean", node.Left.Position);
            }

            if (right.Type != typeof(bool))
            {
                throw CreateError("Right side of logical operator must be boolean", node.Right.Position);
            }

            return node.Operator == BinaryOperator.And
                ? Expression.AndAlso(left, right)
                : Expression.OrElse(left, right);
        }

        // Handle comparison operators - need to unify types
        (left, right) = UnifyTypes(left, right, node);

        return node.Operator switch
        {
            BinaryOperator.Equal => BuildNullSafeEqual(left, right),
            BinaryOperator.NotEqual => Expression.Not(BuildNullSafeEqual(left, right)),
            BinaryOperator.LessThan => BuildNullSafeComparison(left, right, Expression.LessThan),
            BinaryOperator.LessThanOrEqual => BuildNullSafeComparison(left, right, Expression.LessThanOrEqual),
            BinaryOperator.GreaterThan => BuildNullSafeComparison(left, right, Expression.GreaterThan),
            BinaryOperator.GreaterThanOrEqual => BuildNullSafeComparison(left, right, Expression.GreaterThanOrEqual),
            BinaryOperator.And => Expression.AndAlso(left, right),
            BinaryOperator.Or => Expression.OrElse(left, right),
            _ => throw new InvalidOperationException($"Unhandled operator: {node.Operator}")
        };
    }

    /// <inheritdoc />
    public Expression Visit(UnaryNode node)
    {
        var operand = node.Operand.Accept(this);

        if (operand.Type != typeof(bool))
        {
            throw CreateError("'not' operator requires a boolean operand", node.Operand.Position);
        }

        return Expression.Not(operand);
    }

    /// <inheritdoc />
    public Expression Visit(CallNode node)
    {
        if (!Functions.TryGetValue(node.FunctionName, out var builder))
        {
            throw CreateError($"Unknown function '{node.FunctionName}'", node.Position);
        }

        return builder(this, node);
    }

    /// <inheritdoc />
    public Expression Visit(PropertyNode node) => BuildPropertyAccess(node.Path, node.Position);

    /// <inheritdoc />
    public Expression Visit(ConstantNode node) => Expression.Constant(node.Value, node.Value?.GetType() ?? typeof(object));

    private Expression BuildPropertyAccess(IReadOnlyList<string> path, int position)
    {
        Expression current = parameter;
        var currentType = typeof(ImageContent);
        var nullChecks = new List<Expression>();

        for (var i = 0; i < path.Count; i++)
        {
            var segment = path[i];

            // Special case: exif.raw.* dictionary access
            if (i >= 1 && path[i - 1].Equals("raw", StringComparison.OrdinalIgnoreCase) &&
                path.Count > 1 && path[i - 2].Equals("exif", StringComparison.OrdinalIgnoreCase))
            {
                // Build dictionary access: exif.Raw["key"]
                var dictKey = segment;
                var getValueMethod = typeof(IReadOnlyDictionary<string, string>)
                    .GetMethod("get_Item")!;

                // Build: img.Exif != null && img.Exif.Raw != null && img.Exif.Raw.ContainsKey(key) ? img.Exif.Raw[key] : null
                var exifProp = Expression.Property(parameter, "Exif");
                var rawProp = Expression.Property(exifProp, "Raw");
                var keyConst = Expression.Constant(dictKey);

                var containsMethod = typeof(IReadOnlyDictionary<string, string>)
                    .GetMethod("ContainsKey")!;

                var containsCall = Expression.Call(rawProp, containsMethod, keyConst);
                var getValue = Expression.Call(rawProp, getValueMethod, keyConst);

                // Null-safe access
                return Expression.Condition(
                    Expression.AndAlso(
                        Expression.AndAlso(
                            Expression.NotEqual(exifProp, Expression.Constant(null, typeof(ExifData))),
                            Expression.NotEqual(rawProp, Expression.Constant(null, typeof(IReadOnlyDictionary<string, string>)))
                        ),
                        containsCall
                    ),
                    getValue,
                    Expression.Constant(null, typeof(string))
                );
            }

            // Find property (case-insensitive)
            var property = currentType.GetProperty(segment,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?? throw CreateError($"Unknown property '{string.Join(".", path.Take(i + 1))}'", position);

            current = Expression.Property(current, property);
            currentType = property.PropertyType;

            // Track null checks for reference types and nullable value types in the path
            // (but not for the final property - we want to return null/default if parent is null)
            if (i < path.Count - 1 && !currentType.IsValueType)
            {
                nullChecks.Add(Expression.NotEqual(current, Expression.Constant(null, currentType)));
            }
        }

        // If we have null checks (e.g., exif.make), wrap in a conditional
        if (nullChecks.Count > 0)
        {
            // Combine all null checks with AndAlso
            var combinedCheck = nullChecks.Aggregate(Expression.AndAlso);

            // Determine the default value based on the property type
            var defaultValue = GetDefaultValueExpression(currentType);

            return Expression.Condition(combinedCheck, current, defaultValue);
        }

        return current;
    }

    /// <summary>
    /// Gets a default value expression for the given type.
    /// </summary>
    private static Expression GetDefaultValueExpression(Type type)
    {
        // For nullable types, return null
        if (!type.IsValueType || Nullable.GetUnderlyingType(type) != null)
        {
            return Expression.Constant(null, type);
        }

        // For value types, return default
        return Expression.Default(type);
    }

    private Expression BuildDatePartFunction(CallNode node, string partName)
    {
        if (node.Arguments.Count != 1)
        {
            throw CreateError($"'{node.FunctionName}' function requires exactly 1 argument", node.Position);
        }

        var dateExpr = node.Arguments[0].Accept(this);

        // Handle DateTime? by accessing .Value
        if (dateExpr.Type == typeof(DateTime?))
        {
            var hasValue = Expression.Property(dateExpr, "HasValue");
            var value = Expression.Property(dateExpr, "Value");
            var partProperty = typeof(DateTime).GetProperty(partName)!;
            var part = Expression.Property(value, partProperty);

            // Return null if date is null, otherwise return the part
            return Expression.Condition(
                hasValue,
                Expression.Convert(part, typeof(int?)),
                Expression.Constant(null, typeof(int?))
            );
        }

        if (dateExpr.Type == typeof(DateTime))
        {
            var partProperty = typeof(DateTime).GetProperty(partName)!;
            return Expression.Property(dateExpr, partProperty);
        }

        throw CreateError($"'{node.FunctionName}' function requires a date argument", node.Position);
    }

    private ConditionalExpression BuildStringFunction(CallNode node, string methodName)
    {
        if (node.Arguments.Count != 2)
        {
            throw CreateError($"'{node.FunctionName}' function requires exactly 2 arguments", node.Position);
        }

        var stringExpr = node.Arguments[0].Accept(this);
        var searchExpr = node.Arguments[1].Accept(this);

        if (stringExpr.Type != typeof(string))
        {
            throw CreateError($"First argument to '{node.FunctionName}' must be a string", node.Position);
        }

        if (searchExpr.Type != typeof(string))
        {
            throw CreateError($"Second argument to '{node.FunctionName}' must be a string", node.Position);
        }

        var method = typeof(string).GetMethod(methodName, [typeof(string), typeof(StringComparison)])!;
        var comparison = Expression.Constant(StringComparison.OrdinalIgnoreCase);

        // Null-safe: if string is null, return false
        return Expression.Condition(
            Expression.Equal(stringExpr, Expression.Constant(null, typeof(string))),
            Expression.Constant(false),
            Expression.Call(stringExpr, method, searchExpr, comparison)
        );
    }

    private ConditionalExpression BuildToLowerFunction(CallNode node)
    {
        if (node.Arguments.Count != 1)
        {
            throw CreateError($"'{node.FunctionName}' function requires exactly 1 argument", node.Position);
        }

        var stringExpr = node.Arguments[0].Accept(this);

        if (stringExpr.Type != typeof(string))
        {
            throw CreateError($"Argument to '{node.FunctionName}' must be a string", node.Position);
        }

        var method = typeof(string).GetMethod("ToLowerInvariant", Type.EmptyTypes)!;

        // Null-safe
        return Expression.Condition(
            Expression.Equal(stringExpr, Expression.Constant(null, typeof(string))),
            Expression.Constant(null, typeof(string)),
            Expression.Call(stringExpr, method)
        );
    }

    private ConditionalExpression BuildToUpperFunction(CallNode node)
    {
        if (node.Arguments.Count != 1)
        {
            throw CreateError($"'{node.FunctionName}' function requires exactly 1 argument", node.Position);
        }

        var stringExpr = node.Arguments[0].Accept(this);

        if (stringExpr.Type != typeof(string))
        {
            throw CreateError($"Argument to '{node.FunctionName}' must be a string", node.Position);
        }

        var method = typeof(string).GetMethod("ToUpperInvariant", Type.EmptyTypes)!;

        // Null-safe
        return Expression.Condition(
            Expression.Equal(stringExpr, Expression.Constant(null, typeof(string))),
            Expression.Constant(null, typeof(string)),
            Expression.Call(stringExpr, method)
        );
    }

    private (Expression left, Expression right) UnifyTypes(Expression left, Expression right, BinaryNode node)
    {
        // If types already match, no conversion needed
        if (left.Type == right.Type)
        {
            return (left, right);
        }

        // Handle nullable types
        var leftType = Nullable.GetUnderlyingType(left.Type) ?? left.Type;
        var rightType = Nullable.GetUnderlyingType(right.Type) ?? right.Type;

        // Convert int to double if comparing with double
        if (leftType == typeof(int) && rightType == typeof(double))
        {
            left = ConvertToNullable(left, typeof(double?));
            right = ConvertToNullable(right, typeof(double?));
            return (left, right);
        }

        if (leftType == typeof(double) && rightType == typeof(int))
        {
            left = ConvertToNullable(left, typeof(double?));
            right = ConvertToNullable(right, typeof(double?));
            return (left, right);
        }

        // Handle int comparison with int? or similar
        if (leftType == rightType)
        {
            var nullableType = typeof(Nullable<>).MakeGenericType(leftType);
            return (ConvertToNullable(left, nullableType), ConvertToNullable(right, nullableType));
        }

        // String comparison with raw dictionary value (already string)
        if ((leftType == typeof(string) && rightType == typeof(string)) ||
            (left.Type == typeof(string) && right.Type == typeof(string)))
        {
            return (left, right);
        }

        throw CreateError($"Cannot compare '{left.Type.Name}' with '{right.Type.Name}'", node.Position);
    }

    private static Expression ConvertToNullable(Expression expr, Type targetType)
    {
        if (expr.Type == targetType)
        {
            return expr;
        }

        return Expression.Convert(expr, targetType);
    }

    private static BinaryExpression BuildNullSafeEqual(Expression left, Expression right)
    {
        // For reference types or nullable types, use null-safe comparison
        if (!left.Type.IsValueType || Nullable.GetUnderlyingType(left.Type) is not null)
        {
            return Expression.Equal(left, right);
        }

        return Expression.Equal(left, right);
    }

    private static Expression BuildNullSafeComparison(
        Expression left,
        Expression right,
        Func<Expression, Expression, Expression> comparison)
    {
        // For nullable types, return false if either side is null
        var leftNullable = Nullable.GetUnderlyingType(left.Type) is not null;
        var rightNullable = Nullable.GetUnderlyingType(right.Type) is not null;

        if (!leftNullable && !rightNullable)
        {
            return comparison(left, right);
        }

        // Build: left.HasValue && right.HasValue && comparison(left.Value, right.Value)
        var conditions = new List<Expression>();

        if (leftNullable)
        {
            conditions.Add(Expression.Property(left, "HasValue"));
        }

        if (rightNullable)
        {
            conditions.Add(Expression.Property(right, "HasValue"));
        }

        var leftValue = leftNullable ? Expression.Property(left, "Value") : left;
        var rightValue = rightNullable ? Expression.Property(right, "Value") : right;

        var compareExpr = comparison(leftValue, rightValue);

        // Combine all conditions with AndAlso
        var combined = conditions.Aggregate(Expression.AndAlso);
        return Expression.AndAlso(combined, compareExpr);
    }

    private FilterParseException CreateError(string message, int position) =>
        new(message, position) { FilterExpression = source };
}
