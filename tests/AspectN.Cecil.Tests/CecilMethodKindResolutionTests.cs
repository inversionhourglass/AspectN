using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilMethodKindResolutionTests
{
    [Fact]
    public void MapMethod_RuntimeSpecialNameDefinition_RemainsOrdinary()
    {
        using var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var owner = new TypeDefinition("Example", "Number", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(owner);
        var method = new MethodDefinition("op_Addition",
            MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            module.TypeSystem.Int32);
        method.Parameters.Add(new ParameterDefinition(module.TypeSystem.Int32));
        method.Parameters.Add(new ParameterDefinition(module.TypeSystem.Int32));
        owner.Methods.Add(method);
        Assert.Equal(MethodKind.OrdinaryMethod, typeSystem.Mapper.MapMethod(method).Kind);
    }

    [Fact]
    public void MapMethod_UnresolvedOperatorReference_UsesExistingOrdinaryFallback()
    {
        using var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var scope = new AssemblyNameReference("MissingOperatorAssembly", new Version(1, 0));
        var owner = new TypeReference("External", "Number", module, scope);
        var reference = new MethodReference("op_Addition", owner, owner) { HasThis = false };
        reference.Parameters.Add(new ParameterDefinition(owner));
        reference.Parameters.Add(new ParameterDefinition(owner));

        // 无定义时没有 SpecialName/semantics，当前模型不能将此默认类别当作真实元数据事实。
        Assert.Equal(MethodKind.OrdinaryMethod, typeSystem.Mapper.MapMethod(reference).Kind);
    }
}
