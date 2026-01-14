using Spectara.Revela.Core.Configuration;

namespace Spectara.Revela.Core.Tests;

[TestClass]
public sealed class GenerateConfigTests
{
    [TestMethod]
    public void Render_Defaults_ShouldDisableParallel()
    {
        var config = new GenerateConfig();
        Assert.IsFalse(config.Render.Parallel);
        Assert.IsNull(config.Render.MaxDegreeOfParallelism);
    }
}
