using System.Reflection;
using System.Reflection.Emit;
using AspectN.Evaluation;
using AspectN.Metadata;
using Xunit;

namespace AspectN.Reflection.Tests;

public sealed class ReflectionTypeUseTests
{
    [Theory]
    [InlineData("int", MatchStatus.Matched)]
    [InlineData("!ref int", MatchStatus.NotMatched)]
    [InlineData("ref int", MatchStatus.Unknown)]
    [InlineData("out int", MatchStatus.Unknown)]
    [InlineData("in int", MatchStatus.Unknown)]
    [InlineData("ref readonly int", MatchStatus.Unknown)]
    [InlineData("ref string", MatchStatus.NotMatched)]
    public void Match_BareByReference_ConsumesOnlyKnownFacts(string parameter, MatchStatus expected)
    {
        var system = new ReflectionTypeSystem();
        var use = system.Mapper.MapTypeUse(typeof(int).MakeByRefType(), TypeUseKind.Parameter);
        var candidate = new MethodSignature("Probe", MethodKind.OrdinaryMethod, Accessibility.Public,
            true, false, system.Mapper.MapType(typeof(ReflectionTypeUseTests)),
            new TypeUseSignature(system.Mapper.MapType(typeof(void)), RefKind.None, TypeUseKind.Return),
            [new ParameterSignature("value", 0, use)], [], [], [], []);
        var compilation = new AspectNEngine().CompileMemberExpression($"method(* Probe({parameter}))");
        Assert.True(compilation.TryGetMemberPattern(out var pattern));

        var result = pattern.Match(candidate, system);

        Assert.Equal(expected, result.Status);
        if (expected == MatchStatus.Unknown) Assert.NotEmpty(result.Diagnostics);
    }

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
