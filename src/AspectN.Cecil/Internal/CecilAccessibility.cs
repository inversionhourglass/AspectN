using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil.Internal;

internal static class CecilAccessibility
{
    private const int WITHIN_TYPE = 1 << 0;
    private const int DERIVED_SAME_ASSEMBLY = 1 << 1;
    private const int NON_DERIVED_SAME_ASSEMBLY = 1 << 2;
    private const int DERIVED_OTHER_ASSEMBLY = 1 << 3;
    private const int NON_DERIVED_OTHER_ASSEMBLY = 1 << 4;

    private const int PRIVATE_MASK = WITHIN_TYPE;
    private const int PRIVATE_PROTECTED_MASK =
        WITHIN_TYPE | DERIVED_SAME_ASSEMBLY;
    private const int INTERNAL_MASK =
        PRIVATE_PROTECTED_MASK | NON_DERIVED_SAME_ASSEMBLY;
    private const int PROTECTED_MASK =
        PRIVATE_PROTECTED_MASK | DERIVED_OTHER_ASSEMBLY;
    private const int PROTECTED_INTERNAL_MASK =
        INTERNAL_MASK | DERIVED_OTHER_ASSEMBLY;
    private const int PUBLIC_MASK =
        PROTECTED_INTERNAL_MASK | NON_DERIVED_OTHER_ASSEMBLY;

    public static Accessibility ForMethod(
        MethodDefinition? method,
        bool composite)
    {
        if (method is null) return Accessibility.Unknown;

        var mask = MethodMask(method);
        if (composite)
        {
            for (var type = method.DeclaringType;
                 type is not null;
                 type = type.DeclaringType)
            {
                mask &= TypeMask(type);
            }
        }

        return FromMask(mask);
    }

    public static Accessibility ForProperty(
        PropertyDefinition? property,
        bool composite)
    {
        if (property is null) return Accessibility.Unknown;

        var mask = 0;
        if (property.GetMethod is not null)
        {
            mask |= MethodMask(property.GetMethod);
        }

        if (property.SetMethod is not null)
        {
            mask |= MethodMask(property.SetMethod);
        }

        foreach (var method in property.OtherMethods)
        {
            mask |= MethodMask(method);
        }

        if (composite)
        {
            for (var type = property.DeclaringType;
                 type is not null;
                 type = type.DeclaringType)
            {
                mask &= TypeMask(type);
            }
        }

        return FromMask(mask);
    }

    private static int MethodMask(MethodDefinition method)
    {
        if (method.IsPublic) return PUBLIC_MASK;
        if (method.IsFamilyOrAssembly) return PROTECTED_INTERNAL_MASK;
        if (method.IsFamily) return PROTECTED_MASK;
        if (method.IsAssembly) return INTERNAL_MASK;
        if (method.IsFamilyAndAssembly) return PRIVATE_PROTECTED_MASK;
        if (method.IsPrivate) return PRIVATE_MASK;
        return 0;
    }

    private static int TypeMask(TypeDefinition type)
    {
        if (!type.IsNested)
        {
            return type.IsPublic ? PUBLIC_MASK : INTERNAL_MASK;
        }

        if (type.IsNestedPublic) return PUBLIC_MASK;
        if (type.IsNestedFamilyOrAssembly) return PROTECTED_INTERNAL_MASK;
        if (type.IsNestedFamily) return PROTECTED_MASK;
        if (type.IsNestedAssembly) return INTERNAL_MASK;
        if (type.IsNestedFamilyAndAssembly) return PRIVATE_PROTECTED_MASK;
        if (type.IsNestedPrivate) return PRIVATE_MASK;
        return 0;
    }

    private static Accessibility FromMask(int mask)
    {
        return mask switch
        {
            PUBLIC_MASK => Accessibility.Public,
            PROTECTED_INTERNAL_MASK => Accessibility.ProtectedInternal,
            PROTECTED_MASK => Accessibility.Protected,
            INTERNAL_MASK => Accessibility.Internal,
            PRIVATE_PROTECTED_MASK => Accessibility.PrivateProtected,
            PRIVATE_MASK => Accessibility.Private,
            _ => Accessibility.Unknown
        };
    }
}
