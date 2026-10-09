using AspectN.Cecil;
using AspectN.Metadata;
using Mono.Cecil;
using Xunit;

namespace AspectN.Cecil.Tests;

public sealed class CecilTypeTextTests
{
    [Fact]
    public void MapType_GenericPointerWithoutAritySuffix_PreservesArgument()
    {
        using var module = ModuleDefinition.CreateModule("TypeText", ModuleKind.Dll);
        var definition = new TypeDefinition("Example", "Box", TypeAttributes.Public, module.TypeSystem.Object);
        definition.GenericParameters.Add(new GenericParameter("T", definition));
        module.Types.Add(definition);
        var constructed = new GenericInstanceType(definition);
        constructed.GenericArguments.Add(module.TypeSystem.Int32);
        using var system = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var mapped = Assert.IsType<UnsupportedTypeSignature>(system.Mapper.MapType(new PointerType(constructed)));
        Assert.Equal("Example.Box<System.Int32>*", mapped.DisplayName);
    }

    [Theory]
    [InlineData("definition")]
    [InlineData("no-arity")]
    [InlineData("array")]
    [InlineData("bounds")]
    [InlineData("formal")]
    [InlineData("modifier")]
    [InlineData("function")]
    public void MapType_GenericParameterOwner_PreservesNativeFullName(string shape)
    {
        using var module = ModuleDefinition.CreateModule("OwnerText", ModuleKind.Dll);
        TypeReference owner = new TypeReference("Example", shape == "no-arity" ? "Box" : "Box`1", module, module);
        if (shape is not ("definition" or "no-arity"))
        {
            var constructed = new GenericInstanceType(owner);
            TypeReference argument = shape switch
            {
                "array" => new ArrayType(module.TypeSystem.Int32),
                "bounds" => new ArrayType(module.TypeSystem.Int32, 2),
                "formal" => new GenericParameter("U", owner),
                "modifier" => new OptionalModifierType(module.TypeSystem.Object, module.TypeSystem.Int32),
                _ => new FunctionPointerType { ReturnType = module.TypeSystem.Void }
            };
            if (argument is ArrayType { Rank: 2 } array) array.Dimensions[0] = new ArrayDimension(1, 5);
            if (argument is FunctionPointerType function)
            {
                function.Parameters.Add(new ParameterDefinition(module.TypeSystem.Int32));
                function.Parameters.Add(new ParameterDefinition(new SentinelType(module.TypeSystem.Int32)));
            }
            constructed.GenericArguments.Add(argument);
            owner = constructed;
        }
        var parameter = new GenericParameter("T", owner);
        owner.GenericParameters.Add(parameter);
        var expected = owner.FullName;
        using var system = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var mapped = Assert.IsType<GenericParameterTypeSignature>(system.Mapper.MapType(parameter));
        Assert.Equal(expected, mapped.Parameter.OwnerId);
    }

    [Fact]
    public void MapType_MixedModuleReferenceOwner_PreservesNativeModuleFallback()
    {
        using var ownerAssembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("Owner", new Version(1, 0)), "Owner", ModuleKind.Dll);
        using var callerAssembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("Caller", new Version(1, 0)), "Caller", ModuleKind.Dll);
        var outer = new TypeReference("Example", "Outer", ownerAssembly.MainModule, new ModuleReference("Target.netmodule"));
        using var system = new CecilTypeSystem(callerAssembly.MainModule, new CecilMappingOptions(leaveOpen: true));
        GenericParameterTypeSignature? previous = null;
        foreach (var module in new[] { callerAssembly.MainModule, ownerAssembly.MainModule })
        {
            var owner = new TypeReference(string.Empty, "Inner`1", module, outer.Scope) { DeclaringType = outer };
            Assert.Same(module, owner.Module);
            var parameter = new GenericParameter("T", owner);
            owner.GenericParameters.Add(parameter);
            var mapped = Assert.IsType<GenericParameterTypeSignature>(system.Mapper.MapType(parameter));
            Assert.Equal(module.Assembly.Name.Name, mapped.Parameter.OwnerAssembly.Name);
            Assert.Equal("Example.Outer/Inner`1", mapped.Parameter.OwnerId);
            if (previous is not null) Assert.NotSame(previous, mapped);
            previous = mapped;
        }
    }
}
