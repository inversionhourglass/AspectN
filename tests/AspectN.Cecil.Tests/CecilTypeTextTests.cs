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
}
