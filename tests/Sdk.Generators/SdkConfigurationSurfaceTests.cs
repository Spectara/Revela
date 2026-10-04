using System.Reflection;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Tests.Sdk.Generators;

/// <summary>
/// Plugins are banned from the configuration APIs, so the SDK must not hand them one.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class SdkConfigurationSurfaceTests
{
    private const string ConfigurationNamespace = "Microsoft.Extensions.Configuration";

    private const BindingFlags DeclaredMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [TestMethod]
    public void PublicSdkSurface_ExposesNoConfigurationTypes()
    {
        var offenders = typeof(IPlugin).Assembly.GetExportedTypes()
            .SelectMany(FindConfigurationExposures)
            .ToList();

        Assert.IsEmpty(offenders, string.Join(Environment.NewLine, offenders));
    }

    private static IEnumerable<string> FindConfigurationExposures(Type type)
    {
        foreach (var inherited in type.GetInterfaces().Append(type.BaseType).OfType<Type>())
        {
            if (IsConfigurationType(inherited))
            {
                yield return $"{type.FullName} : {inherited.FullName}";
            }
        }

        foreach (var member in type.GetMembers(DeclaredMembers).Where(IsVisibleToPlugins))
        {
            var exposed = member switch
            {
                MethodBase method => method.GetParameters().Select(p => p.ParameterType)
                    .Concat(method is MethodInfo info ? [info.ReturnType] : []),
                PropertyInfo property => [property.PropertyType],
                FieldInfo field => [field.FieldType],
                EventInfo e when e.EventHandlerType is not null => [e.EventHandlerType],
                _ => [],
            };

            if (exposed.Any(IsConfigurationType))
            {
                yield return $"{type.FullName}.{member.Name}";
            }
        }
    }

    private static bool IsVisibleToPlugins(MemberInfo member) => member switch
    {
        MethodBase method => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly,
        FieldInfo field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly,
        PropertyInfo property => property.GetAccessors(nonPublic: true).Any(a => a.IsPublic || a.IsFamily || a.IsFamilyOrAssembly),
        EventInfo e => e.AddMethod is { } add && (add.IsPublic || add.IsFamily || add.IsFamilyOrAssembly),
        _ => false,
    };

    private static bool IsConfigurationType(Type type)
    {
        if (type.HasElementType)
        {
            return IsConfigurationType(type.GetElementType()!);
        }

        if (string.Equals(type.Namespace, ConfigurationNamespace, StringComparison.Ordinal))
        {
            return true;
        }

        return type.IsGenericType && type.GetGenericArguments().Any(IsConfigurationType);
    }
}
