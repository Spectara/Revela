using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Plugins.Calendar;
using Spectara.Revela.Plugins.Calendar.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Tests.Calendar;

[TestClass]
[TestCategory("Unit")]
public sealed class CalendarPluginTests
{
    [TestMethod]
    public void ConfigureServices_RegistersCalendarDataInvalidator()
    {
        var services = new ServiceCollection();

        new CalendarPlugin().ConfigureServices(services);

        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IArtifactInvalidator));
        Assert.IsNotNull(descriptor);
        Assert.AreEqual(typeof(CalendarDataInvalidator), descriptor.ImplementationType);
    }

    [TestMethod]
    public void ConfigureServices_RegistersCalendarCheck()
    {
        var plugin = new CalendarPlugin();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        plugin.ConfigureServices(services);

        // The plugin contributes a single ICheck (surfaced by the host as `check calendar`);
        // it must never hand-write a command wrapper.
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ICheck));
        Assert.IsNotNull(descriptor, "CalendarPlugin should register an ICheck.");
        Assert.AreEqual(typeof(CalendarDataCheck), descriptor.ImplementationType);
    }
}
