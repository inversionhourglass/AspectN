using AspectN.Metadata;
using AspectN.TypeSystem;
using Mono.Cecil;

namespace AspectN.Cecil.Internal;

internal static class CecilRefKindDetector
{
    private const string IS_READ_ONLY_ATTRIBUTE =
        "System.Runtime.CompilerServices.IsReadOnlyAttribute";
    private const string REQUIRES_LOCATION_ATTRIBUTE =
        "System.Runtime.CompilerServices.RequiresLocationAttribute";

    public static RefKind ForParameter(ParameterDefinition parameter, MappingContext resources)
    {
        if (!ContainsByReference(parameter.ParameterType, resources))
        {
            return RefKind.None;
        }

        // In/Out 同时出现表示互操作方向，不改变 ref 的语言分类。
        if (parameter.IsOut && !parameter.IsIn) return RefKind.Out;
        if (ContainsModifier(
                parameter.ParameterType,
                REQUIRES_LOCATION_ATTRIBUTE, resources) ||
            HasAttribute(parameter, REQUIRES_LOCATION_ATTRIBUTE, resources))
        {
            return RefKind.RefReadOnly;
        }

        if (ContainsModifier(parameter.ParameterType, IS_READ_ONLY_ATTRIBUTE, resources) ||
            HasAttribute(parameter, IS_READ_ONLY_ATTRIBUTE, resources))
        {
            return RefKind.In;
        }

        return RefKind.Ref;
    }

    public static RefKind ForReturn(MethodDefinition? method, MappingContext resources)
    {
        if (method is null || !ContainsByReference(method.ReturnType, resources))
        {
            return RefKind.None;
        }

        if (ContainsModifier(method.ReturnType, IS_READ_ONLY_ATTRIBUTE, resources) ||
            ContainsModifier(
                method.ReturnType,
                REQUIRES_LOCATION_ATTRIBUTE, resources) ||
            HasAttribute(method.MethodReturnType, IS_READ_ONLY_ATTRIBUTE, resources) ||
            HasAttribute(
                method.MethodReturnType,
                REQUIRES_LOCATION_ATTRIBUTE, resources))
        {
            return RefKind.RefReadOnly;
        }

        return RefKind.Ref;
    }

    internal static bool ContainsByReference(TypeReference type, MappingContext resources)
    {
        while (true)
        {
            resources.SpendWork();
            if (type is ByReferenceType) return true;
            if (type is not TypeSpecification specification || specification.ElementType is null) return false;
            type = specification.ElementType;
        }
    }

    private static bool ContainsModifier(
        TypeReference type,
        string modifierFullName, MappingContext resources)
    {
        while (true)
        {
            resources.SpendWork();
            if (type is IModifierType modifier && CecilNativeTypeMetadata.FullName(modifier.ModifierType, resources) == modifierFullName) return true;
            if (type is not TypeSpecification specification || specification.ElementType is null) return false;
            type = specification.ElementType;
        }
    }

    private static bool HasAttribute(
        ICustomAttributeProvider provider,
        string attributeFullName, MappingContext resources)
    {
        return provider.HasCustomAttributes && resources.Attributes(provider.CustomAttributes).Any(
            attribute => CecilNativeTypeMetadata.FullName(attribute.AttributeType, resources) == attributeFullName);
    }
}
