using Microsoft.Extensions.Configuration;
using Spectara.Revela.Core.Configuration;

namespace Spectara.Revela.Tests.Core.Configuration;

[TestClass]
[TestCategory("Unit")]
public sealed class PluginConfigOwnershipTests
{
    [TestMethod]
    public void FromClaims_TwoPackagesClaimSameKey_ThrowsNamingBothPackagesAndKey()
    {
        PluginConfigClaim[] claims =
        [
            new("serve", "Spectara.Revela.Plugins.Serve"),
            new("serve", "Contoso.Revela.Plugins.Serve"),
        ];

        var exception = Assert.ThrowsExactly<PluginConfigConflictException>(() => PluginConfigOwnership.FromClaims(claims));

        Assert.AreEqual("serve", exception.Key);
        Assert.AreEqual("Spectara.Revela.Plugins.Serve", exception.FirstPackageId);
        Assert.AreEqual("Contoso.Revela.Plugins.Serve", exception.SecondPackageId);
        Assert.Contains("'Spectara.Revela.Plugins.Serve'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'Contoso.Revela.Plugins.Serve'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'plugins:serve'", exception.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void FromClaims_KeysDifferOnlyInCase_ThrowsConflict()
    {
        // IConfiguration is case-insensitive, so "Serve" and "serve" are the same node.
        PluginConfigClaim[] claims = [new("serve", "A.Package"), new("Serve", "B.Package")];

        Assert.ThrowsExactly<PluginConfigConflictException>(() => PluginConfigOwnership.FromClaims(claims));
    }

    [TestMethod]
    public void FromClaims_SamePackageClaimsKeyTwice_DoesNotThrow()
    {
        PluginConfigClaim[] claims = [new("serve", "Spectara.Revela.Plugins.Serve"), new("serve", "Spectara.Revela.Plugins.Serve")];

        var ownership = PluginConfigOwnership.FromClaims(claims);

        Assert.AreEqual("Spectara.Revela.Plugins.Serve", Assert.ContainsSingle(ownership.Owners).Value);
    }

    [TestMethod]
    public void FindUnclaimedKeys_TypoCloseToClaimedKey_SuggestsClaimedKey()
    {
        var ownership = Ownership("serve", "statistics");
        var configuration = Configuration(("plugins:serv:port", "3000"));

        var unclaimed = Assert.ContainsSingle(ownership.FindUnclaimedKeys(configuration));

        Assert.AreEqual("serv", unclaimed.Key);
        Assert.AreEqual("serve", unclaimed.Suggestion);
    }

    [TestMethod]
    public void FindUnclaimedKeys_KeyFarFromAnyClaimedKey_HasNoSuggestion()
    {
        var ownership = Ownership("serve", "statistics");
        var configuration = Configuration(("plugins:analytics:enabled", "true"));

        var unclaimed = Assert.ContainsSingle(ownership.FindUnclaimedKeys(configuration));

        Assert.AreEqual("analytics", unclaimed.Key);
        Assert.IsNull(unclaimed.Suggestion);
    }

    [TestMethod]
    public void FindUnclaimedKeys_ClaimedKeysInAnyCase_AreNotReported()
    {
        var ownership = Ownership("serve", "oneDrive");
        var configuration = Configuration(
            ("plugins:serve:port", "3000"),
            ("plugins:ONEDRIVE:SHAREURL", "https://1drv.ms/f/x"));

        Assert.IsEmpty(ownership.FindUnclaimedKeys(configuration));
    }

    [TestMethod]
    public void FindUnclaimedKeys_PackageIdShapedKeys_AreIgnored()
    {
        // The plugins node also holds the dependency map (package ID → version).
        var ownership = Ownership("serve");
        var configuration = Configuration(
            ("plugins:Spectara.Revela.Plugins.Serve", "1.0.0"),
            ("plugins:my-plugin", "1.0.0"),
            ("plugins:my_plugin:x", "1"));

        Assert.IsEmpty(ownership.FindUnclaimedKeys(configuration));
    }

    [TestMethod]
    public void FindUnclaimedKeys_UpperCaseEnvironmentVariableTypo_IsReported()
    {
        var ownership = Ownership("serve");
        var configuration = Configuration(("plugins:SERV:PORT", "3000"));

        var unclaimed = Assert.ContainsSingle(ownership.FindUnclaimedKeys(configuration));

        Assert.AreEqual("SERV", unclaimed.Key);
        Assert.AreEqual("serve", unclaimed.Suggestion);
    }

    [TestMethod]
    public void Report_UnclaimedKeys_LogsOneWarningPerKey()
    {
        var ownership = Ownership("serve");
        var configuration = Configuration(
            ("plugins:serv:port", "3000"),
            ("plugins:analytics:enabled", "true"),
            ("plugins:Spectara.Revela.Plugins.Serve", "1.0.0"));
        var logger = new CapturingLogger<UnclaimedPluginConfigReporter>();

        new UnclaimedPluginConfigReporter(configuration, ownership, logger).Report();

        Assert.HasCount(2, logger.Entries);
        Assert.IsTrue(logger.Entries.All(entry => entry.Level == LogLevel.Warning));
        Assert.Contains(
            "Configuration 'plugins:serv' is not used by any installed plugin. Did you mean 'serve'?",
            logger.Entries.Select(entry => entry.Message));
        Assert.Contains(
            "Configuration 'plugins:analytics' is not used by any installed plugin.",
            logger.Entries.Select(entry => entry.Message));
    }

    private static PluginConfigOwnership Ownership(params string[] keys) =>
        PluginConfigOwnership.FromClaims(keys.Select(key => new PluginConfigClaim(key, $"Package.{key}")));

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
