using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AspectN.Tests.Shared;

internal static class CustomModifierOwnerFixture
{
    public static byte[] CreateImage(string location = "return", bool optional = false, bool otherModifier = false)
    {
        using var module = ModuleDefinition.CreateModule("CustomModifierIdentity", ModuleKind.Dll);
        var owner = new TypeDefinition("Example", "Owner", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        module.Types.Add(owner);
        foreach (var modified in new[] { false, true })
        {
            var method = new MethodDefinition("M", MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
            owner.Methods.Add(method);
            var parameter = new GenericParameter("T", method);
            method.GenericParameters.Add(parameter);
            parameter.Constraints.Add(new GenericParameterConstraint(module.ImportReference(
                modified ? typeof(IComparable) : typeof(IDisposable))));
            var modifier = module.ImportReference(otherModifier
                ? typeof(System.Runtime.InteropServices.OutAttribute)
                : typeof(System.Runtime.InteropServices.InAttribute));
            TypeReference Modify(TypeReference type) => optional
                ? new OptionalModifierType(modifier, type) : new RequiredModifierType(modifier, type);
            method.ReturnType = new ByReferenceType(parameter);
            method.Parameters.Add(new ParameterDefinition(new ByReferenceType(parameter)));
            if (modified)
            {
                if (location == "parameter") method.Parameters[0].ParameterType = Modify(method.Parameters[0].ParameterType);
                else if (location == "element") method.ReturnType = new ByReferenceType(Modify(parameter));
                else method.ReturnType = Modify(method.ReturnType);
#if !NETFRAMEWORK
                if (location == "return" && !optional && !otherModifier)
                    method.MethodReturnType.CustomAttributes.Add(new CustomAttribute(module.ImportReference(
                        typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute).GetConstructor(Type.EmptyTypes)!)));
#endif
            }
            if (location is "mixed" or "required-order")
            {
                var input = module.ImportReference(typeof(System.Runtime.InteropServices.InAttribute));
                var output = module.ImportReference(typeof(System.Runtime.InteropServices.OutAttribute));
                TypeReference Outer(TypeReference element) => location == "mixed"
                    ? new OptionalModifierType(output, element) : new RequiredModifierType(output, element);
                method.ReturnType = modified
                    ? Outer(new RequiredModifierType(input, new ByReferenceType(parameter)))
                    : new RequiredModifierType(input, Outer(new ByReferenceType(parameter)));
            }
            if (location is "array" or "generic")
            {
                TypeReference Element(bool withModifier) => withModifier ? Modify(parameter) : parameter;
                TypeReference Shape(TypeReference element)
                {
                    if (location == "array") return new ArrayType(element);
                    var generic = new GenericInstanceType(module.ImportReference(typeof(List<>)));
                    generic.GenericArguments.Add(element);
                    return generic;
                }
                method.ReturnType = Shape(Element(modified));
                method.Parameters[0].ParameterType = Shape(Element(false));
            }
            if (location is "pointer" or "function-return" or "function-parameter")
            {
                TypeReference Shape(bool withModifier)
                {
                    var element = withModifier ? Modify(module.TypeSystem.Int32) : module.TypeSystem.Int32;
                    if (location == "pointer") return new PointerType(element);
                    var function = new FunctionPointerType
                    {
                        ReturnType = location == "function-return" ? element : module.TypeSystem.Int32
                    };
                    function.Parameters.Add(new ParameterDefinition(location == "function-parameter" ? element : module.TypeSystem.Int32));
                    return function;
                }
                method.ReturnType = Shape(modified);
                method.Parameters[0].ParameterType = Shape(false);
            }
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        }
        using var image = new MemoryStream();
        module.Write(image);
        return image.ToArray();
    }
}
