using System.Globalization;
using System.Reflection;
using AspectN.Cecil;
using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil.Fody.Tests;

public sealed class FodyCecilIdentityTests
{
    private const string FODY_CECIL_PUBLIC_KEY_TOKEN = "1ca091877d12ca03";

    [Fact]
    public void AdapterAssembly_MonoCecilReference_UsesFodyIdentity()
    {
        var adapterReference = typeof(CecilTypeSystem)
            .Assembly
            .GetReferencedAssemblies()
            .Single(reference => reference.Name == "Mono.Cecil");

        Assert.Equal(
            FODY_CECIL_PUBLIC_KEY_TOKEN,
            GetPublicKeyToken(adapterReference));
        Assert.Equal(
            FODY_CECIL_PUBLIC_KEY_TOKEN,
            GetPublicKeyToken(typeof(ModuleDefinition).Assembly.GetName()));
    }

    [Fact]
    public void MapType_FodyCecilModule_MapsType()
    {
        var module = ModuleDefinition.CreateModule(
            "FodyCecilIdentityTests",
            ModuleKind.Dll);
        using var typeSystem = new CecilTypeSystem(module);

        var mapped = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(module.TypeSystem.String));

        Assert.Equal("System.String", mapped.Identity.FullMetadataName);
    }

    private static string GetPublicKeyToken(AssemblyName assemblyName)
    {
        return string.Concat(
            (assemblyName.GetPublicKeyToken() ?? Array.Empty<byte>())
                .Select(value =>
                    value.ToString("x2", CultureInfo.InvariantCulture)));
    }
}
