using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil.Internal;

internal static class CecilRefKindDetector
{
    private const string IS_READ_ONLY_ATTRIBUTE =
        "System.Runtime.CompilerServices.IsReadOnlyAttribute";
    private const string REQUIRES_LOCATION_ATTRIBUTE =
        "System.Runtime.CompilerServices.RequiresLocationAttribute";

    public static RefKind ForParameter(ParameterDefinition parameter)
    {
        if (!ContainsByReference(parameter.ParameterType))
        {
            return RefKind.None;
        }

        if (parameter.IsOut) return RefKind.Out;
        if (ContainsModifier(
                parameter.ParameterType,
                REQUIRES_LOCATION_ATTRIBUTE) ||
            HasAttribute(parameter, REQUIRES_LOCATION_ATTRIBUTE))
        {
            return RefKind.RefReadOnly;
        }

        if (parameter.IsIn ||
            HasAttribute(parameter, IS_READ_ONLY_ATTRIBUTE))
        {
            return RefKind.In;
        }

        return RefKind.Ref;
    }

    public static RefKind ForReturn(MethodDefinition? method)
    {
        if (method is null || !ContainsByReference(method.ReturnType))
        {
            return RefKind.None;
        }

        if (ContainsModifier(method.ReturnType, IS_READ_ONLY_ATTRIBUTE) ||
            ContainsModifier(
                method.ReturnType,
                REQUIRES_LOCATION_ATTRIBUTE) ||
            HasAttribute(method.MethodReturnType, IS_READ_ONLY_ATTRIBUTE) ||
            HasAttribute(
                method.MethodReturnType,
                REQUIRES_LOCATION_ATTRIBUTE))
        {
            return RefKind.RefReadOnly;
        }

        return RefKind.Ref;
    }

    internal static bool ContainsByReference(TypeReference type)
    {
        return type switch
        {
            ByReferenceType => true,
            TypeSpecification specification =>
                ContainsByReference(specification.ElementType),
            _ => false
        };
    }

    private static bool ContainsModifier(
        TypeReference type,
        string modifierFullName)
    {
        return type switch
        {
            OptionalModifierType optional =>
                optional.ModifierType.FullName == modifierFullName ||
                ContainsModifier(optional.ElementType, modifierFullName),
            RequiredModifierType required =>
                required.ModifierType.FullName == modifierFullName ||
                ContainsModifier(required.ElementType, modifierFullName),
            TypeSpecification specification =>
                ContainsModifier(specification.ElementType, modifierFullName),
            _ => false
        };
    }

    private static bool HasAttribute(
        ICustomAttributeProvider provider,
        string attributeFullName)
    {
        return provider.HasCustomAttributes && provider.CustomAttributes.Any(
            attribute => attribute.AttributeType.FullName == attributeFullName);
    }
}
