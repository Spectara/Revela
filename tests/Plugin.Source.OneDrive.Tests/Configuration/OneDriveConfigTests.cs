using System.ComponentModel.DataAnnotations;
using Spectara.Revela.Plugin.Source.OneDrive.Models;

namespace Spectara.Revela.Plugin.Source.OneDrive.Tests.Configuration;

[TestClass]
public sealed class OneDriveConfigTests
{
    #region ShareUrl Validation

    [TestMethod]
    public void ShareUrl_WhenNull_FailsValidation()
    {
        // Arrange
        var config = new OneDriveConfig { ShareUrl = null! };

        // Act
        var results = ValidateModel(config);

        // Assert
        Assert.IsTrue(results.Any(r => r.MemberNames.Contains("ShareUrl")));
    }

    [TestMethod]
    public void ShareUrl_WhenEmpty_FailsValidation()
    {
        // Arrange
        var config = new OneDriveConfig { ShareUrl = string.Empty };

        // Act
        var results = ValidateModel(config);

        // Assert
        Assert.IsTrue(results.Any(r => r.MemberNames.Contains("ShareUrl")));
    }

    [TestMethod]
    [DataRow("https://1drv.ms/f/s!example")]
    [DataRow("https://1drv.ms/u/s!ABC123")]
    [DataRow("https://onedrive.live.com/redir?resid=123")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Test data")]
    public void ShareUrl_WhenValidOneDriveUrl_PassesValidation(string url)
    {
        // Arrange
        var config = new OneDriveConfig { ShareUrl = url };

        // Act
        var results = ValidateModel(config);

        // Assert
        Assert.IsEmpty(results);
    }

    [TestMethod]
    [DataRow("https://google.com")]
    [DataRow("https://dropbox.com/share")]
    [DataRow("http://1drv.ms/f/s!example")] // HTTP not HTTPS
    [DataRow("not-a-url")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Test data")]
    public void ShareUrl_WhenInvalidUrl_FailsValidation(string url)
    {
        // Arrange
        var config = new OneDriveConfig { ShareUrl = url };

        // Act
        var results = ValidateModel(config);

        // Assert
        Assert.IsNotEmpty(results);
    }

    #endregion

    #region IncludePatterns

    [TestMethod]
    public void IncludePatterns_WhenNull_IsAllowed()
    {
        // Arrange
        var config = new OneDriveConfig
        {
            ShareUrl = "https://1drv.ms/f/s!example",
            IncludePatterns = null
        };

        // Act
        var results = ValidateModel(config);

        // Assert
        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void IncludePatterns_WhenEmpty_IsAllowed()
    {
        // Arrange
        var config = new OneDriveConfig
        {
            ShareUrl = "https://1drv.ms/f/s!example",
            IncludePatterns = []
        };

        // Act
        var results = ValidateModel(config);

        // Assert
        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void IncludePatterns_WithWildcards_IsAllowed()
    {
        // Arrange
        var config = new OneDriveConfig
        {
            ShareUrl = "https://1drv.ms/f/s!example",
            IncludePatterns = ["*.jpg", "*.png", "photo_?.jpeg"]
        };

        // Act
        var results = ValidateModel(config);

        // Assert
        Assert.IsEmpty(results);
    }

    #endregion

    #region ExcludePatterns

    [TestMethod]
    public void ExcludePatterns_WhenNull_IsAllowed()
    {
        // Arrange
        var config = new OneDriveConfig
        {
            ShareUrl = "https://1drv.ms/f/s!example",
            ExcludePatterns = null
        };

        // Act
        var results = ValidateModel(config);

        // Assert
        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void ExcludePatterns_WithPatterns_IsAllowed()
    {
        // Arrange
        var config = new OneDriveConfig
        {
            ShareUrl = "https://1drv.ms/f/s!example",
            ExcludePatterns = ["*.tmp", "thumbs.db", "_*"]
        };

        // Act
        var results = ValidateModel(config);

        // Assert
        Assert.IsEmpty(results);
    }

    #endregion

    #region Helper Methods

    private static List<ValidationResult> ValidateModel(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }

    #endregion
}
