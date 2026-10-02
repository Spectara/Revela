using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;

using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Models;
using Spectara.Revela.Sdk.Models.Manifest;

namespace Spectara.Revela.Features.Generate.Filtering;

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
/// contains(keywords, 'Startseite')   // whole keyword, case-insensitive
///
/// // Sort and limit (pipe syntax)
/// all | sort dateTaken desc | limit 5
/// rating >= 4 | sort random | limit 15
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
    /// <param name="pageSort">Optional page sort override in <c>field[:direction]</c> format.</param>
    /// <param name="globalSort">Optional global image sort configuration.</param>
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
    public static IEnumerable<ImageContent> ApplyQuery(
        IEnumerable<ImageContent> images,
        string filterExpression,
        string? pageSort = null,
        ImageSortConfig? globalSort = null)
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

        // Step 2: Select one effective sort and apply a stable filename tie-breaker
        if (query.Sort is { IsRandom: true })
        {
            // Shuffle from a stable filename order; the secure generator only avoids CA5394, the order is not security-relevant.
            var shuffled = result.OrderBy(image => image.Filename, StringComparer.OrdinalIgnoreCase).ToArray();
            RandomNumberGenerator.Shuffle(shuffled.AsSpan());
            result = shuffled;
        }
        else if (query.Sort is not null)
        {
            result = ApplySort(result, query.Sort);
        }
        else if (globalSort is not null)
        {
            result = Sort(result, pageSort, globalSort);
        }
        else
        {
            result = result.OrderBy(image => image.Filename, StringComparer.OrdinalIgnoreCase);
        }

        // Step 3: Limit (if specified)
        if (query.Limit is not null)
        {
            result = result.Take(query.Limit.Value);
        }

        return result;
    }

    /// <summary>
    /// Sorts images by the configured image sort, optionally overridden by a page's <c>sort</c>.
    /// </summary>
    /// <remarks>
    /// The one sort used for folder galleries and for filter galleries and <c>[[gallery]]</c>
    /// blocks without their own <c>| sort</c>.
    /// </remarks>
    /// <param name="images">The images to sort.</param>
    /// <param name="pageSort">Optional page sort override in <c>field[:direction]</c> format.</param>
    /// <param name="globalSort">The configured image sort (field, direction, fallback).</param>
    /// <returns>The sorted images.</returns>
    public static IReadOnlyList<ImageContent> Sort(
        IEnumerable<ImageContent> images,
        string? pageSort,
        ImageSortConfig globalSort)
    {
        var (sort, fallbackPropertyPath) = CreateConfiguredSort(pageSort, globalSort);
        return [.. ApplySort(images, sort, fallbackPropertyPath)];
    }

    /// <summary>
    /// Applies sorting to images based on a sort clause.
    /// </summary>
    /// <remarks>
    /// Images without a value for the sort field come last in both directions, ordered among
    /// themselves by the fallback field (same direction). Ties are broken by filename.
    /// </remarks>
    private static IEnumerable<ImageContent> ApplySort(
        IEnumerable<ImageContent> images,
        SortClause sort,
        IReadOnlyList<string>? fallbackPropertyPath = null)
    {
        var keySelector = CreateSortKeySelector(sort.PropertyPath);
        var fallbackSelector = fallbackPropertyPath is null
            ? null
            : CreateSortKeySelector(fallbackPropertyPath);

        var keyed = images
            .Select(image =>
            {
                var key = NullIfEmpty(keySelector(image));
                return (Image: image, Key: key, Fallback: key is null ? NullIfEmpty(fallbackSelector?.Invoke(image)) : null);
            })
            .ToList();

        var sorted = keyed.OrderBy(entry => entry.Key is null);
        sorted = sort.Direction == SortDirection.Asc
            ? sorted
                .ThenBy(entry => entry.Key, SortKeyComparer.Instance)
                .ThenBy(entry => entry.Fallback is null)
                .ThenBy(entry => entry.Fallback, SortKeyComparer.Instance)
            : sorted
                .ThenByDescending(entry => entry.Key, SortKeyComparer.Instance)
                .ThenBy(entry => entry.Fallback is null)
                .ThenByDescending(entry => entry.Fallback, SortKeyComparer.Instance);

        return sorted
            .ThenBy(entry => entry.Image.Filename, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.Image);
    }

    private static object? NullIfEmpty(object? value) => value is "" ? null : value;

    private static (SortClause Sort, IReadOnlyList<string> FallbackPropertyPath) CreateConfiguredSort(
        string? pageSort,
        ImageSortConfig globalSort)
    {
        var field = globalSort.Field;
        var direction = globalSort.Direction;

        if (!string.IsNullOrEmpty(pageSort))
        {
            var parts = pageSort.Split(':', 2);
            field = parts[0];

            if (parts.Length > 1)
            {
                direction = parts[1].ToUpperInvariant() switch
                {
                    "ASC" => SortDirection.Asc,
                    "DESC" => SortDirection.Desc,
                    _ => direction
                };
            }
        }

        return (
            new SortClause(SplitPropertyPath(field), direction),
            SplitPropertyPath(globalSort.Fallback));
    }

    private static IReadOnlyList<string> SplitPropertyPath(string propertyPath) =>
        propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries);

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
                var property = LookupProperty(type, segment);

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
    /// Looks up a public instance property on <paramref name="type"/> by name (case-insensitive).
    /// </summary>
    /// <remarks>
    /// Trim-safe: the only types ever passed in are <see cref="ImageContent"/>,
    /// <see cref="ExifData"/>, primitive leaf values, or <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
    /// Both <c>ImageContent</c> and <c>ExifData</c> carry a class-level
    /// <see cref="DynamicallyAccessedMembersAttribute"/> annotation that
    /// preserves their public properties at trim time. The analyzer cannot see
    /// this through the <c>object?</c> static type of the navigating local, so
    /// the warning is suppressed here at minimum scope.
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The parameter of method does not have matching annotations.",
        Justification = "ImageContent and ExifData carry class-level [DynamicallyAccessedMembers(PublicProperties)]; navigation only walks these types and primitive leaves.")]
    private static PropertyInfo? LookupProperty(Type type, string segment)
        => type.GetProperty(
            segment,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

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
    /// Orders the values of one sort field: same-typed values by their natural order (text
    /// ordinal case-insensitive, like the filename tie-breaker), mixed types by their invariant text.
    /// </summary>
    private sealed class SortKeyComparer : IComparer<object?>
    {
        public static SortKeyComparer Instance { get; } = new();

        public int Compare(object? x, object? y)
        {
            if (x is null || y is null)
            {
                return (x is null).CompareTo(y is null);
            }

            if (x is string textX && y is string textY)
            {
                return StringComparer.OrdinalIgnoreCase.Compare(textX, textY);
            }

            if (x.GetType() == y.GetType() && x is IComparable comparable)
            {
                return comparable.CompareTo(y);
            }

            return StringComparer.OrdinalIgnoreCase.Compare(
                Convert.ToString(x, CultureInfo.InvariantCulture),
                Convert.ToString(y, CultureInfo.InvariantCulture));
        }
    }
}
