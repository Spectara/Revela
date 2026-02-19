using System.Linq.Expressions;
using System.Reflection;

using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Sdk.Models;
using Spectara.Revela.Sdk.Models.Manifest;

namespace Spectara.Revela.Commands.Generate.Filtering;

/// <summary>
/// Service for filtering images using filter expressions.
/// </summary>
/// <remarks>
/// <para>
/// Filter expressions use a simple syntax for querying image metadata:
/// </para>
/// <code>
/// // Simple comparison
/// exif.make == 'Canon'
/// exif.iso >= 800
///
/// // Logical operators
/// exif.make == 'Canon' and exif.iso >= 800
/// exif.make == 'Canon' or exif.make == 'Sony'
///
/// // Functions
/// year(dateTaken) == 2024
/// contains(filename, 'portrait')
///
/// // Sort and limit (pipe syntax)
/// all | sort dateTaken desc | limit 5
/// exif.make == 'Canon' | sort exif.iso desc | limit 10
/// </code>
/// </remarks>
internal sealed class FilterService
{
    /// <summary>
    /// Compiles a filter expression into a predicate.
    /// </summary>
    /// <param name="filterExpression">The filter expression string.</param>
    /// <returns>A compiled predicate for filtering images.</returns>
    /// <exception cref="FilterParseException">Thrown when the filter expression is invalid.</exception>
    public static Func<ImageContent, bool> Compile(string filterExpression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filterExpression);

        var expression = CompileToExpression(filterExpression);
        return expression.Compile();
    }

    /// <summary>
    /// Compiles a filter expression into a LINQ expression tree.
    /// </summary>
    /// <param name="filterExpression">The filter expression string.</param>
    /// <returns>A LINQ expression tree.</returns>
    /// <exception cref="FilterParseException">Thrown when the filter expression is invalid.</exception>
    public static Expression<Func<ImageContent, bool>> CompileToExpression(string filterExpression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filterExpression);

        var query = ParseQuery(filterExpression);

        // If "all" was specified, return a predicate that always returns true
        if (query.Predicate is null)
        {
            return _ => true;
        }

        // Build expression from AST
        var builder = new FilterExpressionBuilder(filterExpression);
        return builder.Build(query.Predicate);
    }

    /// <summary>
    /// Parses a filter expression into a query object.
    /// </summary>
    /// <param name="filterExpression">The filter expression string.</param>
    /// <returns>The parsed filter query.</returns>
    /// <exception cref="FilterParseException">Thrown when the filter expression is invalid.</exception>
    public static FilterQuery ParseQuery(string filterExpression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filterExpression);

        var lexer = new FilterLexer(filterExpression);
        var tokens = lexer.Tokenize();
        var parser = new FilterParser(tokens, filterExpression);
        return parser.Parse();
    }

    /// <summary>
    /// Filters images using the specified filter expression.
    /// </summary>
    /// <param name="images">The images to filter.</param>
    /// <param name="filterExpression">The filter expression string.</param>
    /// <returns>Images matching the filter.</returns>
    /// <exception cref="FilterParseException">Thrown when the filter expression is invalid.</exception>
    public static IEnumerable<ImageContent> Apply(IEnumerable<ImageContent> images, string filterExpression)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentException.ThrowIfNullOrWhiteSpace(filterExpression);

        var predicate = Compile(filterExpression);
        return images.Where(predicate);
    }

    /// <summary>
    /// Applies a complete filter query including filter, sort, and limit.
    /// </summary>
    /// <param name="images">The images to process.</param>
    /// <param name="filterExpression">The filter expression string (may include sort and limit).</param>
    /// <returns>Filtered, sorted, and limited images.</returns>
    /// <exception cref="FilterParseException">Thrown when the filter expression is invalid.</exception>
    /// <example>
    /// <code>
    /// // Get 5 newest Canon images
    /// var result = FilterService.ApplyQuery(images, "exif.make == 'Canon' | sort dateTaken desc | limit 5");
    ///
    /// // Get all images sorted by filename
    /// var result = FilterService.ApplyQuery(images, "all | sort filename");
    /// </code>
    /// </example>
    public static IEnumerable<ImageContent> ApplyQuery(IEnumerable<ImageContent> images, string filterExpression)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentException.ThrowIfNullOrWhiteSpace(filterExpression);

        var query = ParseQuery(filterExpression);

        var result = images;

        // Step 1: Filter (or pass all through)
        if (query.Predicate is not null)
        {
            var builder = new FilterExpressionBuilder(filterExpression);
            var predicate = builder.Build(query.Predicate).Compile();
            result = result.Where(predicate);
        }

        // Step 2: Sort (if specified)
        if (query.Sort is not null)
        {
            result = ApplySort(result, query.Sort);
        }

        // Step 3: Limit (if specified)
        if (query.Limit is not null)
        {
            result = result.Take(query.Limit.Value);
        }

        return result;
    }

    /// <summary>
    /// Applies sorting to images based on a sort clause.
    /// </summary>
    private static IEnumerable<ImageContent> ApplySort(IEnumerable<ImageContent> images, SortClause sort)
    {
        // Convert to list for multiple enumerations if needed
        var imageList = images as IList<ImageContent> ?? [.. images];

        if (imageList.Count == 0)
        {
            return imageList;
        }

        // Create sort key selector
        var keySelector = CreateSortKeySelector(sort.PropertyPath);

        // Apply sort with null handling (nulls go to end)
        return sort.Direction == SortDirection.Asc
            ? imageList.OrderBy(img => keySelector(img) ?? GetMaxValue(sort.PropertyPath), NullSafeComparer.Instance)
            : imageList.OrderByDescending(img => keySelector(img) ?? GetMinValue(sort.PropertyPath), NullSafeComparer.Instance);
    }

    /// <summary>
    /// Creates a function that extracts the sort key from an image.
    /// </summary>
    private static Func<ImageContent, object?> CreateSortKeySelector(IReadOnlyList<string> propertyPath)
    {
        return image =>
        {
            object? current = image;

            foreach (var segment in propertyPath)
            {
                if (current is null)
                {
                    return null;
                }

                var type = current.GetType();
                var property = type.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (property is null)
                {
                    // Special handling for EXIF raw dictionary
                    if (current is ExifData exif && segment.Equals("raw", StringComparison.OrdinalIgnoreCase))
                    {
                        current = exif.Raw;
                        continue;
                    }

                    if (current is IReadOnlyDictionary<string, string> dict)
                    {
                        return dict.TryGetValue(segment, out var value) ? value : null;
                    }

                    return null;
                }

                current = property.GetValue(current);
            }

            return current;
        };
    }

    /// <summary>
    /// Gets a maximum value for sorting nulls to end in ascending order.
    /// </summary>
    private static object GetMaxValue(IReadOnlyList<string> propertyPath)
    {
        // Determine type based on property path
        var lastSegment = propertyPath[^1].ToUpperInvariant();

        return lastSegment switch
        {
            "DATETAKEN" => DateTime.MaxValue,
            "ISO" or "FNUMBER" or "FOCALLENGTH" or "EXPOSURETIME" => double.MaxValue,
            "WIDTH" or "HEIGHT" or "FILESIZE" => long.MaxValue,
            _ => "\uFFFF" // High unicode character for strings
        };
    }

    /// <summary>
    /// Gets a minimum value for sorting nulls to end in descending order.
    /// </summary>
    private static object GetMinValue(IReadOnlyList<string> propertyPath)
    {
        var lastSegment = propertyPath[^1].ToUpperInvariant();

        return lastSegment switch
        {
            "DATETAKEN" => DateTime.MinValue,
            "ISO" or "FNUMBER" or "FOCALLENGTH" or "EXPOSURETIME" => double.MinValue,
            "WIDTH" or "HEIGHT" or "FILESIZE" => long.MinValue,
            _ => string.Empty
        };
    }

    /// <summary>
    /// Validates a filter expression without executing it.
    /// </summary>
    /// <param name="filterExpression">The filter expression to validate.</param>
    /// <returns>True if the expression is valid.</returns>
    /// <exception cref="FilterParseException">Thrown when the filter expression is invalid.</exception>
    public static bool Validate(string filterExpression)
    {
        if (string.IsNullOrWhiteSpace(filterExpression))
        {
            return false;
        }

        // This will throw if invalid
        _ = ParseQuery(filterExpression);
        return true;
    }

    /// <summary>
    /// Tries to validate a filter expression and returns the error if invalid.
    /// </summary>
    /// <param name="filterExpression">The filter expression to validate.</param>
    /// <param name="error">The error message if validation fails.</param>
    /// <returns>True if the expression is valid, false otherwise.</returns>
    public static bool TryValidate(string filterExpression, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(filterExpression))
        {
            error = "Filter expression cannot be empty";
            return false;
        }

        try
        {
            _ = ParseQuery(filterExpression);
            return true;
        }
        catch (FilterParseException ex)
        {
            error = ex.GetDetailedMessage();
            return false;
        }
    }

    /// <summary>
    /// Comparer that handles mixed types safely for sorting.
    /// </summary>
    private sealed class NullSafeComparer : IComparer<object>
    {
        public static NullSafeComparer Instance { get; } = new();

        public int Compare(object? x, object? y)
        {
            if (x is null && y is null)
            {
                return 0;
            }

            if (x is null)
            {
                return 1; // Nulls go to end
            }

            if (y is null)
            {
                return -1;
            }

            // Handle IComparable
            if (x is IComparable comparableX)
            {
                // Try to convert y to same type
                if (x.GetType() != y.GetType())
                {
                    try
                    {
                        y = Convert.ChangeType(y, x.GetType(), System.Globalization.CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        // Fall back to string comparison
                        return string.Compare(x.ToString(), y.ToString(), StringComparison.OrdinalIgnoreCase);
                    }
                }

                return comparableX.CompareTo(y);
            }

            // Fallback to string comparison
            return string.Compare(x.ToString(), y.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
