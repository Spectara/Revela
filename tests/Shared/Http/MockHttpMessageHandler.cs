using System.Net;

namespace Spectara.Revela.Tests.Shared.Http;

/// <summary>
/// Simple HTTP message handler for mocking HTTP responses in tests.
/// </summary>
/// <remarks>
/// <para>Supports exact URI matching and pattern-based matching.</para>
/// <para>Responses are cloned to allow reuse across multiple requests.</para>
/// </remarks>
/// <example>
/// <code>
/// var handler = new MockHttpMessageHandler();
/// handler.AddResponse(new Uri("https://api.example.com/data"),
///     new HttpResponseMessage(HttpStatusCode.OK)
///     {
///         Content = new StringContent("{}", Encoding.UTF8, "application/json")
///     });
///
/// var httpClient = new HttpClient(handler);
/// </code>
/// </example>
public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<Uri, HttpResponseMessage> responses = [];
    private readonly List<(Func<string, bool> Matcher, HttpResponseMessage Response)> patternResponses = [];
    private readonly List<HttpRequestMessage> recordedRequests = [];

    /// <summary>
    /// Gets all requests that were sent through this handler.
    /// </summary>
    public IReadOnlyList<HttpRequestMessage> RecordedRequests => recordedRequests.AsReadOnly();

    /// <summary>
    /// Adds a response for an exact URI match.
    /// </summary>
    /// <param name="uri">The exact URI to match.</param>
    /// <param name="response">The response to return.</param>
    public void AddResponse(Uri uri, HttpResponseMessage response) =>
        responses[uri] = response;

    /// <summary>
    /// Adds a response for URLs matching a pattern.
    /// </summary>
    /// <param name="urlMatcher">A predicate to match URL strings.</param>
    /// <param name="response">The response to return.</param>
    public void AddPatternResponse(Func<string, bool> urlMatcher, HttpResponseMessage response) =>
        patternResponses.Add((urlMatcher, response));

    /// <summary>
    /// Clears all configured responses and recorded requests.
    /// </summary>
    public void Clear()
    {
        responses.Clear();
        patternResponses.Clear();
        recordedRequests.Clear();
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        recordedRequests.Add(request);
        var uri = request.RequestUri!;

        // Check exact match first
        if (responses.TryGetValue(uri, out var response))
        {
            return Task.FromResult(CloneResponse(response));
        }

        // Check pattern matches
        foreach (var (matcher, patternResponse) in patternResponses)
        {
            if (matcher(uri.ToString()))
            {
                return Task.FromResult(CloneResponse(patternResponse));
            }
        }

        // Default: not found
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static HttpResponseMessage CloneResponse(HttpResponseMessage original)
    {
        // Clone to allow reuse
        var clone = new HttpResponseMessage(original.StatusCode);

        if (original.Content != null)
        {
            var contentBytes = original.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            clone.Content = new ByteArrayContent(contentBytes);

            foreach (var header in original.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
