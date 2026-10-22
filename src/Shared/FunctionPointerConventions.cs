using AspectN.TypeSystem;

namespace AspectN.AdapterInternals;

internal static class FunctionPointerConventions
{
    private const string PREFIX = "System.Runtime.CompilerServices.CallConv";

    internal static string Format(string convention, IEnumerable<string> modifierNames, MappingContext resources)
    {
        var names = new List<string>();
        long length = 0;
        foreach (var name in modifierNames)
        {
            resources.SpendWork(Math.Max(1, name.Length));
            var prefix = name.StartsWith(PREFIX, StringComparison.Ordinal);
            var count = prefix ? name.Length - PREFIX.Length : name.Length;
            resources.CheckTextLength(length, count + (names.Count > 0 ? 1L : 0));
            length += count + (names.Count > 0 ? 1L : 0);
            names.Add(prefix ? name.Substring(PREFIX.Length).ToLowerInvariant() : name);
        }
        if (names.Count == 0) return resources.Text(convention);
        if (convention == "unmanaged" && names.Count == 1 &&
            names[0] is "cdecl" or "stdcall" or "thiscall" or "fastcall") return names[0];
        resources.CheckTextLength(length, convention.Length + 1L);
        names.Sort(Comparer<string>.Create((left, right) =>
        {
            resources.SpendWork(Math.Max(1L, left.Length + (long)right.Length));
            return StringComparer.Ordinal.Compare(left, right);
        }));
        return resources.Concat(convention, ":", resources.Join(",", names));
    }
}
