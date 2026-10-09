using System.Reflection;
using System.Reflection.Emit;
using AspectN.Metadata;
using Xunit;

namespace AspectN.Reflection.Tests;

public sealed class ReflectionTypeUseTests
{
    [Fact]
    public void MapTypeUse_BareByReference_PreservesMarkerWithoutGuessingRefKind()
    {
        var use = new ReflectionTypeSystem().Mapper.MapTypeUse(typeof(int).MakeByRefType(), TypeUseKind.Parameter);

        Assert.True(use.IsByReference);
        Assert.True(use.Availability.IsKnown(MetadataFacts.ByReference));
        Assert.False(use.Availability.IsKnown(MetadataFacts.RefKind));
        Assert.IsType<NamedTypeSignature>(use.Type);
    }

    [Fact]
    public void MapType_MetadataNameWithoutAritySuffix_UsesOwnGenericParameterCount()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("NoArityReview"), AssemblyBuilderAccess.RunAndCollect);
        var module = assembly.DefineDynamicModule("NoArityReview");
        var outer = module.DefineType("Example.Outer", TypeAttributes.Public);
        outer.DefineGenericParameters("T1", "T2");
        var inner = outer.DefineNestedType("Inner", TypeAttributes.NestedPublic);
        inner.DefineGenericParameters("T1", "T2", "U");
        var innerType = inner.CreateType()!;
        outer.CreateType();

        var signature = Assert.IsType<NamedTypeSignature>(new ReflectionTypeSystem().Mapper.MapType(innerType));

        Assert.Equal(1, signature.Identity.GenericArity);
        Assert.Equal(2, signature.Identity.ContainingType!.GenericArity);
    }
}
