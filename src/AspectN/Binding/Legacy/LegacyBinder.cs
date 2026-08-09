using AspectN.Syntax;

namespace AspectN.Binding.Legacy;

internal static class LegacyBinder
{
    public static bool EqualsKeyword(string actual, string expected)
    {
        return string.Equals(
            actual,
            expected,
            StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryGetImplicitVariableName(
        NamedTypePatternSyntax syntax,
        bool declaration,
        Func<string, bool> isAlreadyDeclared,
        out string? name)
    {
        return LegacyGenericRules.TryGetImplicitVariableName(
            syntax,
            declaration,
            isAlreadyDeclared,
            out name);
    }

    public static BoundGenericPattern CreateOmittedGenericPattern()
    {
        return LegacyGenericRules.CreateOmittedGenericPattern();
    }

    public static BoundAttributeTargetKind GetAttributeTarget(string text)
    {
        return LegacyAttributeRules.GetTarget(text);
    }

    public static string NormalizeModifier(string content)
    {
        return LegacyModifierRules.Normalize(content);
    }
}
