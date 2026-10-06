using AspectN.Metadata;

namespace AspectN.AdapterInternals;

internal static class GenericOwnerIdentity
{
    public static string Qualify(string metadataName, AssemblyIdentity assembly)
    {
        // 长度前缀避免程序集名或文化名中的分隔符造成身份碰撞。
        var fields = new[] { assembly.Name, assembly.Version?.ToString() ?? string.Empty,
            assembly.CultureName, assembly.PublicKeyToken.ToLowerInvariant() };
        return "[" + string.Join(";", fields.Select(field => field.Length + ":" + field)) + "]" + metadataName;
    }
}
