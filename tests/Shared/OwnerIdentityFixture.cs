using System.Reflection;

namespace AspectN.Tests.Shared;

internal static class OwnerIdentityFixture
{
    public static string FrameworkType(string name) => Qualify(name, typeof(object).Assembly.GetName());

    public static string RuntimeType(string name, Type type) => Qualify(name, type.Assembly.GetName());

    public static string SourceType(string name, string assemblyName) =>
        Qualify(name, new AssemblyName { Name = assemblyName, Version = new Version(0, 0, 0, 0) });

    private static string Qualify(string name, AssemblyName assembly)
    {
        var assemblyName = assembly.Name!;
        var version = assembly.Version!.ToString();
        var culture = assembly.CultureName ?? string.Empty;
        var token = Convert.ToHexString(assembly.GetPublicKeyToken() ?? []).ToLowerInvariant();
        return $"[{assemblyName.Length}:{assemblyName};{version.Length}:{version};{culture.Length}:{culture};{token.Length}:{token}]{name}";
    }
}
