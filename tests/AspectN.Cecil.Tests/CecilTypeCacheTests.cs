using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilTypeCacheTests
{
    [Fact]
    public void MapType_LessInformativeReference_DoesNotPoisonDefinitionKind()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var reference = new TypeReference(
            "Example",
            "IContract",
            module,
            module);
        var definition = new TypeDefinition(
            "Example",
            "IContract",
            TypeAttributes.Public |
            TypeAttributes.Interface |
            TypeAttributes.Abstract,
            module.TypeSystem.Object);
        module.Types.Add(definition);

        var mappedReference = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(reference));
        var mappedDefinition = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(definition));

        Assert.NotSame(mappedReference, mappedDefinition);
        Assert.Equal(TypeKind.Interface, mappedDefinition.Identity.Kind);
    }
}
