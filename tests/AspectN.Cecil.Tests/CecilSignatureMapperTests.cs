using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilSignatureMapperTests
{
    [Fact]
    public void MapType_SystemInt32_MapsIdentity()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);

        var signature = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(module.TypeSystem.Int32));

        Assert.Equal("System", signature.Identity.Namespace);
        Assert.Equal("Int32", signature.Identity.MetadataName);
        Assert.Equal(
            module.TypeSystem.Int32.Resolve().Module.Assembly.Name.Name,
            signature.Identity.Assembly.Name);
        Assert.Equal(0, signature.Identity.GenericArity);
    }
}
