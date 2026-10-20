using AspectN.Metadata;

namespace AspectN.AdapterInternals;

internal static class GenericOwnerIdentity
{
    public static string CustomModifier(string path, bool optional, string typeForm)
    {
        return path.Length + ":" + path + (optional ? "modopt(" : "modreq(") + typeForm.Length + ":" + typeForm + ")";
    }

    public static string WithCustomModifiers(string typeForm, IEnumerable<string> modifiers)
    {
        var entries = modifiers.ToArray();
        // 独立后缀保留原签名的遍历顺序和位置，不能按 modreq/modopt 分类重排。
        return entries.Length == 0 ? typeForm : typeForm + "#mods[" + string.Concat(entries) + "]";
    }

    public static string Qualify(string metadataName, AssemblyIdentity assembly)
    {
        // 长度前缀避免程序集名或文化名中的分隔符造成身份碰撞。
        var fields = new[] { assembly.Name, assembly.Version?.ToString() ?? string.Empty,
            assembly.CultureName, assembly.PublicKeyToken.ToLowerInvariant() };
        return "[" + string.Join(";", fields.Select(field => field.Length + ":" + field)) + "]" + metadataName;
    }
}
