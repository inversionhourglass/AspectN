using System.Reflection;
using AspectN.TypeSystem;

namespace AspectN.Cecil.Tests;

public sealed class CecilContextTests
{
    [Fact]
    public void Constructor_ModuleScope_ExposesServicesAndContracts()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);

        Assert.Same(module, typeSystem.Module);
        Assert.Same(typeSystem.Mapper, typeSystem.Mapper);
        Assert.IsAssignableFrom<ITypeSystem>(typeSystem);
        Assert.IsAssignableFrom<IDisposable>(typeSystem);
        Assert.Equal(
            TypeSystemCapabilities.TypeEquality,
            typeSystem.Capabilities);
        AssertNoStaticDictionary(typeof(CecilTypeSystem));
        AssertNoStaticDictionary(typeof(CecilSignatureMapper));
    }

    [Fact]
    public void Dispose_RepeatedCall_GuardsModuleAndMapper()
    {
        var module = TestModuleFactory.Create();
        var int32 = module.TypeSystem.Int32;
        var typeSystem = new CecilTypeSystem(module);
        var mapper = typeSystem.Mapper;

        typeSystem.Dispose();
        typeSystem.Dispose();

        Assert.Throws<ObjectDisposedException>(() => typeSystem.Module);
        Assert.Throws<ObjectDisposedException>(() => mapper.MapType(int32));
    }

    private static void AssertNoStaticDictionary(Type type)
    {
        Assert.DoesNotContain(
            type.GetFields(
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic),
            field => field.FieldType.Name.Contains(
                "Dictionary",
                StringComparison.Ordinal));
    }
}
