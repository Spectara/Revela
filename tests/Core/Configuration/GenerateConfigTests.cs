using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk.Configuration;

namespace Spectara.Revela.Tests.Core.Configuration;

[TestClass]
[TestCategory("Unit")]
public sealed class GenerateConfigTests
{
    [TestMethod]
    public void Render_Defaults_ShouldDisableParallel()
    {
        var config = new GenerateConfig();
        Assert.IsFalse(config.Render.Parallel);
        Assert.IsNull(config.Render.MaxDegreeOfParallelism);
    }

    [TestMethod]
    public void Images_Defaults_UseFastAvifAndTheWebpEncoderDefault()
    {
        // AVIF effort 2 encodes about 7× faster than libvips' default 4 at no measured size cost.
        // WebP keeps libvips' default, so WebP libraries are not re-encoded by an update.
        var images = new GenerateConfig().Images;

        Assert.AreEqual(2, images.AvifEffort);
        Assert.AreEqual(4, images.WebpEffort);
        Assert.AreEqual(ImageConfig.DefaultAvifEffort, images.AvifEffort);
        Assert.AreEqual(ImageConfig.DefaultWebpEffort, images.WebpEffort);
        Assert.AreEqual(0, images.MaxSize, "No cap: the largest variant has the photo's full resolution.");
    }

    [TestMethod]
    public void Images_Defaults_PublishWideGamutPhotosInDisplayP3() =>
        Assert.IsTrue(new GenerateConfig().Images.WideGamut, "Wide-gamut photos are published in Display P3 unless switched off.");

    [TestMethod]
    public void Images_WideGamutFalse_Binds() =>
        Assert.IsFalse(Bind("wideGamut", "false").Images.WideGamut);

    [TestMethod]
    public void Images_NegativeMaxSize_FailsValidation()
    {
        var exception = Assert.ThrowsExactly<OptionsValidationException>(() => Bind("maxSize", "-1"));

        Assert.IsTrue(exception.Failures.Any(f => f.Contains("MaxSize", StringComparison.Ordinal)), string.Join("; ", exception.Failures));
    }

    [TestMethod]
    [DataRow("avifEffort", "0")]
    [DataRow("avifEffort", "9")]
    [DataRow("webpEffort", "0")]
    [DataRow("webpEffort", "6")]
    public void Images_EffortWithinEncoderRange_Binds(string key, string value)
    {
        var images = Bind(key, value).Images;

        Assert.AreEqual(int.Parse(value, System.Globalization.CultureInfo.InvariantCulture), key == "avifEffort" ? images.AvifEffort : images.WebpEffort);
    }

    [TestMethod]
    [DataRow("avifEffort", "-1", "AvifEffort")]
    [DataRow("avifEffort", "10", "AvifEffort")]
    [DataRow("webpEffort", "7", "WebpEffort")]
    public void Images_EffortOutsideEncoderRange_FailsValidation(string key, string value, string property)
    {
        var exception = Assert.ThrowsExactly<OptionsValidationException>(() => Bind(key, value));

        Assert.IsTrue(exception.Failures.Any(f => f.Contains(property, StringComparison.Ordinal)), string.Join("; ", exception.Failures));
    }

    private static GenerateConfig Bind(string imageKey, string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"generate:images:{imageKey}"] = value })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddRevelaConfigSections();
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptionsMonitor<GenerateConfig>>().CurrentValue;
    }
}
