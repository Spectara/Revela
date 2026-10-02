using Spectara.Revela.Features.Generate.Services;

namespace Spectara.Revela.Tests.Commands.Generate.Services;

/// <summary>
/// How image encoding splits the CPU: images in parallel × libvips threads per image.
/// </summary>
/// <remarks>
/// Measured on real photos: one AVIF encode with 8 libvips threads burns 1.7× the CPU of one
/// thread for the same file, so many images with few threads each finish sooner than a few
/// images with many threads (2 cores: −21 %, 4 cores: −16 %, 32 threads JPEG-only: +62 % images/min).
/// </remarks>
[TestClass]
[TestCategory("Unit")]
public sealed class ImageWorkerPlanTests
{
    private const long GiB = 1024L * 1024 * 1024;

    [TestMethod]
    [DataRow(1, 1, 1, DisplayName = "1 core")]
    [DataRow(2, 2, 1, DisplayName = "2 cores")]
    [DataRow(4, 4, 1, DisplayName = "4 cores")]
    [DataRow(8, 4, 2, DisplayName = "8 threads")]
    [DataRow(32, 16, 2, DisplayName = "32 threads")]
    public void Create_EnoughMemory_UsesManyImagesWithFewThreads(int processors, int workers, int threads)
    {
        var plan = ImageWorkerPlan.Create(processors, 128 * GiB, configuredWorkers: null);

        Assert.AreEqual(new ImageWorkerPlan(workers, threads), plan);
    }

    [TestMethod]
    public void Create_LittleMemory_CapsImagesAndGivesTheirThreadsTheFreedCores()
    {
        var plan = ImageWorkerPlan.Create(32, 4 * ImageWorkerPlan.MemoryPerWorker, configuredWorkers: null);

        Assert.AreEqual(new ImageWorkerPlan(4, 8), plan);
    }

    [TestMethod]
    public void Create_LessMemoryThanOneWorkerBudget_StillProcessesOneImage()
    {
        var plan = ImageWorkerPlan.Create(4, ImageWorkerPlan.MemoryPerWorker / 2, configuredWorkers: null);

        Assert.AreEqual(new ImageWorkerPlan(1, 4), plan);
    }

    [TestMethod]
    public void Create_UnknownMemory_UsesTheCpuSplit()
    {
        var plan = ImageWorkerPlan.Create(4, 0, configuredWorkers: null);

        Assert.AreEqual(new ImageWorkerPlan(4, 1), plan);
    }

    [TestMethod]
    [DataRow(1, 1, 8, DisplayName = "sequential")]
    [DataRow(16, 16, 2, DisplayName = "one per core pair")]
    [DataRow(64, 64, 1, DisplayName = "more than cores")]
    public void Create_ConfiguredWorkers_WinOverCpuAndMemory(int configured, int workers, int threads)
    {
        var plan = ImageWorkerPlan.Create(32, ImageWorkerPlan.MemoryPerWorker, configured);

        Assert.AreEqual(new ImageWorkerPlan(workers, threads), plan);
    }

    [TestMethod]
    public void Create_ConfiguredWorkersBelowOne_ProcessesOneImage()
    {
        var plan = ImageWorkerPlan.Create(4, 128 * GiB, configuredWorkers: 0);

        Assert.AreEqual(1, plan.Workers);
    }
}
