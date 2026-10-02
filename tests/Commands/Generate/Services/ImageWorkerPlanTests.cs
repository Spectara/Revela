using Spectara.Revela.Features.Generate.Services;

namespace Spectara.Revela.Tests.Commands.Generate.Services;

/// <summary>
/// How image encoding splits the CPU: images in parallel × libvips threads per image.
/// </summary>
/// <remarks>
/// Measured on the owner's photos: JPEG and WebP encode single-threaded, so many images with one
/// or two threads each finish 25–29 % sooner than a few images with many threads. The AV1
/// encoder instead fills idle time with extra threads; with AVIF the previous split (half the
/// cores as images, up to 8 threads each) was as fast or faster and keeps AVIF files
/// byte-identical, because libaom's output depends on its thread count.
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
    public void Create_WithoutAvif_UsesManyImagesWithFewThreads(int processors, int workers, int threads)
    {
        var plan = ImageWorkerPlan.Create(processors, 128 * GiB, configuredWorkers: null, imageCount: 1000, encodesAvif: false);

        Assert.AreEqual(new ImageWorkerPlan(workers, threads), plan);
    }

    [TestMethod]
    [DataRow(1, 1, 1, DisplayName = "1 core")]
    [DataRow(2, 1, 2, DisplayName = "2 cores")]
    [DataRow(4, 2, 4, DisplayName = "4 cores")]
    [DataRow(32, 16, 8, DisplayName = "32 threads")]
    public void Create_WithAvif_KeepsTheThreadsAvifIsEncodedWith(int processors, int workers, int threads)
    {
        var plan = ImageWorkerPlan.Create(processors, 128 * GiB, configuredWorkers: null, imageCount: 1000, encodesAvif: true);

        Assert.AreEqual(new ImageWorkerPlan(workers, threads), plan);
    }

    [TestMethod]
    public void Create_FewerImagesThanWorkers_GivesTheFreeCoresToTheirThreads()
    {
        var plan = ImageWorkerPlan.Create(32, 128 * GiB, configuredWorkers: null, imageCount: 8, encodesAvif: false);

        Assert.AreEqual(new ImageWorkerPlan(8, 4), plan);
    }

    [TestMethod]
    public void Create_LittleMemory_CapsImagesAndGivesTheirThreadsTheFreedCores()
    {
        var plan = ImageWorkerPlan.Create(32, 4 * ImageWorkerPlan.MemoryPerWorker, configuredWorkers: null, imageCount: 1000, encodesAvif: false);

        Assert.AreEqual(new ImageWorkerPlan(4, 8), plan);
    }

    [TestMethod]
    public void Create_LittleMemoryWithAvif_CapsImages()
    {
        var plan = ImageWorkerPlan.Create(8, 2 * ImageWorkerPlan.MemoryPerWorker, configuredWorkers: null, imageCount: 1000, encodesAvif: true);

        Assert.AreEqual(new ImageWorkerPlan(2, 8), plan);
    }

    [TestMethod]
    public void Create_LessMemoryThanOneWorkerBudget_StillProcessesOneImage()
    {
        var plan = ImageWorkerPlan.Create(4, ImageWorkerPlan.MemoryPerWorker / 2, configuredWorkers: null, imageCount: 1000, encodesAvif: false);

        Assert.AreEqual(new ImageWorkerPlan(1, 4), plan);
    }

    [TestMethod]
    public void Create_UnknownMemory_UsesTheCpuSplit()
    {
        var plan = ImageWorkerPlan.Create(4, 0, configuredWorkers: null, imageCount: 1000, encodesAvif: false);

        Assert.AreEqual(new ImageWorkerPlan(4, 1), plan);
    }

    [TestMethod]
    [DataRow(1, false, 1, 8, DisplayName = "sequential")]
    [DataRow(16, false, 16, 2, DisplayName = "one per core pair")]
    [DataRow(64, false, 64, 1, DisplayName = "more than cores")]
    [DataRow(16, true, 16, 8, DisplayName = "with AVIF")]
    public void Create_ConfiguredWorkers_WinOverCpuMemoryAndBatchSize(int configured, bool avif, int workers, int threads)
    {
        var plan = ImageWorkerPlan.Create(32, ImageWorkerPlan.MemoryPerWorker, configured, imageCount: 2, encodesAvif: avif);

        Assert.AreEqual(new ImageWorkerPlan(workers, threads), plan);
    }

    [TestMethod]
    public void Create_ConfiguredWorkersBelowOne_ProcessesOneImage()
    {
        var plan = ImageWorkerPlan.Create(4, 128 * GiB, configuredWorkers: 0, imageCount: 1000, encodesAvif: false);

        Assert.AreEqual(1, plan.Workers);
    }
}
