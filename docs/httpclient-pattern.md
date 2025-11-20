# HttpClient Pattern for Revela Plugins

## Typed Client Pattern (Recommended)

Revela uses the **Typed Client pattern** for HttpClient management - the Microsoft-recommended approach for .NET applications.

---

## Why Typed Client?

✅ **Type-safe** - HttpClient is automatically injected  
✅ **Configured per service** - Each plugin can have its own timeout/headers  
✅ **Testable** - Easy to mock HttpClient in tests  
✅ **Connection pooling** - Automatic handler reuse  
✅ **DNS-aware** - Handlers rotate every 2 minutes (configurable)  

---

## Implementation Example

### 1. Register in Program.cs

```csharp
// Program.cs - Register Typed Client
services.AddHttpClient<MyPlugin.MyHttpService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
    client.DefaultRequestHeaders.Add("User-Agent", "Revela/1.0");
    client.BaseAddress = new Uri("https://api.example.com");
});
```

### 2. Service Constructor

```csharp
// MyHttpService.cs - Direct HttpClient injection
public sealed class MyHttpService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MyHttpService> _logger;
    
    public MyHttpService(HttpClient httpClient, ILogger<MyHttpService> logger)
    {
        _httpClient = httpClient;  // ✅ Typed Client - already configured!
        _logger = logger;
    }
    
    public async Task<string> GetDataAsync()
    {
        var response = await _httpClient.GetAsync("/api/endpoint");
        return await response.Content.ReadAsStringAsync();
    }
}
```

### 3. Use in Commands

```csharp
// MyCommand.cs - Get service from DI
private static async Task ExecuteAsync(IServiceProvider services)
{
    var myService = (MyHttpService?)services.GetService(typeof(MyHttpService))
        ?? throw new InvalidOperationException("MyHttpService not available");
    
    await myService.GetDataAsync();
}
```

---

## Real Example: OneDrive Plugin

### Program.cs Registration

```csharp
services.AddHttpClient<Spectara.Revela.Plugin.Source.OneDrive.Providers.SharedLinkProvider>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5); // Large file downloads
    client.DefaultRequestHeaders.Add("User-Agent", "Revela/1.0 (Static Site Generator)");
});
```

### SharedLinkProvider

```csharp
public sealed class SharedLinkProvider : IOneDriveProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SharedLinkProvider> _logger;
    
    public SharedLinkProvider(HttpClient httpClient, ILogger<SharedLinkProvider> logger)
    {
        _httpClient = httpClient;  // ✅ Pre-configured by Program.cs
        _logger = logger;
    }
}
```

### OneDriveSourceCommand

```csharp
var provider = (SharedLinkProvider?)services.GetService(typeof(SharedLinkProvider))
    ?? throw new InvalidOperationException("SharedLinkProvider not available");
```

---

## Advanced Configuration

### Handler Lifetime

```csharp
services.AddHttpClient<MyService>(client => { /* ... */ })
    .SetHandlerLifetime(TimeSpan.FromMinutes(10));  // Default: 2 minutes
```

### Retry Policies (Polly)

```csharp
services.AddHttpClient<MyService>(client => { /* ... */ })
    .AddPolicyHandler(GetRetryPolicy());

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}
```

### Custom Message Handler

```csharp
services.AddHttpClient<MyService>(client => { /* ... */ })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
        MaxConnectionsPerServer = 20
    });
```

---

## Testing

### Mock HttpClient in Tests

```csharp
[TestMethod]
public async Task GetDataAsync_ShouldReturnData()
{
    // Arrange
    var mockHandler = new MockHttpMessageHandler();
    mockHandler.When("https://api.example.com/*")
        .Respond("application/json", "{\"data\":\"test\"}");
    
    var httpClient = mockHandler.ToHttpClient();
    var logger = Substitute.For<ILogger<MyService>>();
    var service = new MyService(httpClient, logger);
    
    // Act
    var result = await service.GetDataAsync();
    
    // Assert
    result.Should().Contain("test");
}
```

---

## Common Mistakes

### ❌ DON'T: Create HttpClient manually

```csharp
// ❌ BAD - Socket exhaustion!
using var client = new HttpClient();
```

### ❌ DON'T: Use IHttpClientFactory in Typed Client

```csharp
// ❌ BAD - Defeats purpose of Typed Client
public MyService(IHttpClientFactory factory)
{
    _httpClient = factory.CreateClient();
}
```

### ❌ DON'T: Cache HttpClient in Singleton

```csharp
// ❌ BAD - Captive dependency, DNS issues
[Singleton]
public class MyService
{
    private readonly HttpClient _client;
    
    public MyService(HttpClient client)
    {
        _client = client;  // ❌ Captured for entire app lifetime!
    }
}
```

### ✅ DO: Request Typed Client per operation

```csharp
// ✅ GOOD - Transient service, fresh HttpClient each time
[Transient]
public class MyService
{
    private readonly HttpClient _httpClient;
    
    public MyService(HttpClient httpClient)
    {
        _httpClient = httpClient;  // ✅ Short-lived, automatically managed
    }
}
```

---

## Plugin Checklist

When creating a new plugin with HTTP calls:

- [ ] Register Typed Client in `Program.cs`
- [ ] Inject `HttpClient` directly (not `IHttpClientFactory`)
- [ ] Configure timeout appropriate for your API
- [ ] Add User-Agent header
- [ ] Service is Transient (not Singleton)
- [ ] Get service from DI in command
- [ ] Add tests with mock HttpClient

---

## References

- [Microsoft Docs: IHttpClientFactory](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory)
- [HttpClient Guidelines](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines)
- [Typed Clients](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests#typed-clients)

---

**Last Updated:** 2025-01-20  
**Pattern Used By:** OneDrive Plugin v1.0.0
