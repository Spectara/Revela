using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Plugins.Calendar;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Tests.Calendar;

[TestClass]
[TestCategory("Unit")]
public sealed class CalendarPluginTests
{
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
