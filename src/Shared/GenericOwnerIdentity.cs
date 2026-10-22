using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.AdapterInternals;

internal static class GenericOwnerIdentity
{
    public static string CustomModifier(MappingContext resources, string path, bool optional, string typeForm)
    {
        return resources.Concat(path.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), ":", path,
            optional ? "modopt(" : "modreq(", typeForm.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), ":", typeForm, ")");
    }

    public static string WithCustomModifiers(MappingContext resources, string typeForm, IEnumerable<string> modifiers)
    {
        var entries = resources.Join(string.Empty, modifiers);
        // 独立后缀保留原签名的遍历顺序和位置，不能按 modreq/modopt 分类重排。
        return entries.Length == 0 ? typeForm : resources.Concat(typeForm, "#mods[", entries, "]");
    }

    public static string Qualify(MappingContext resources, string metadataName, AssemblyIdentity assembly)
    {
        // 长度前缀避免程序集名或文化名中的分隔符造成身份碰撞。
        resources.CheckTextLength(0, assembly.PublicKeyToken.Length);
        var fields = new[] { assembly.Name, assembly.Version?.ToString() ?? string.Empty,
            assembly.CultureName, assembly.PublicKeyToken.ToLowerInvariant() };
        return resources.Concat("[", resources.Join(";", fields.Select(field =>
            resources.Concat(field.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), ":", field))), "]", metadataName);
    }
}
