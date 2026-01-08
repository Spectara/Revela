namespace Spectara.Revela.Commands.Generate.Filtering;

/// <summary>
/// Exception thrown when a filter expression cannot be parsed or evaluated.
/// </summary>
public sealed class FilterParseException : Exception
{
    /// <summary>
    /// Gets the position in the filter expression where the error occurred.
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// Gets the filter expression that caused the error.
    /// </summary>
    public string? FilterExpression { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterParseException"/> class.
    /// </summary>
    public FilterParseException()
        : base("Filter parse error")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterParseException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public FilterParseException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterParseException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public FilterParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterParseException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="position">The position where the error occurred.</param>
    public FilterParseException(string message, int position)
        : base(FormatMessage(message, position))
    {
        Position = position;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterParseException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="position">The position where the error occurred.</param>
    /// <param name="innerException">The inner exception.</param>
    public FilterParseException(string message, int position, Exception innerException)
        : base(FormatMessage(message, position), innerException)
    {
        Position = position;
    }

    private static string FormatMessage(string message, int position) =>
        $"{message} at position {position}";

    /// <summary>
    /// Creates a formatted error message showing the error location in the filter.
    /// </summary>
    /// <returns>A formatted error message with context.</returns>
    public string GetDetailedMessage()
    {
        if (string.IsNullOrEmpty(FilterExpression))
        {
            return Message;
        }

        var pointer = new string(' ', Position) + "^";
        return $"""
            Filter error: {Message}

              {FilterExpression}
              {pointer}
            """;
    }
}
