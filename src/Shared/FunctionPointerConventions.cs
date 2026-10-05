namespace AspectN.AdapterInternals;

internal static class FunctionPointerConventions
{
    private const string PREFIX = "System.Runtime.CompilerServices.CallConv";

    internal static string Format(string convention, IEnumerable<string> modifierNames)
    {
        var names = modifierNames.Select(name => name.StartsWith(PREFIX, StringComparison.Ordinal)
                ? name.Substring(PREFIX.Length).ToLowerInvariant() : name)
            .OrderBy(name => name, StringComparer.Ordinal).ToArray();
        if (names.Length == 0) return convention;
        if (convention == "unmanaged" && names.Length == 1 &&
            names[0] is "cdecl" or "stdcall" or "thiscall" or "fastcall") return names[0];
        return convention + ":" + string.Join(",", names);
    }
}
