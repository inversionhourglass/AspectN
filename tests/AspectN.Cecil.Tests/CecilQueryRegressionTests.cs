using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilQueryRegressionTests
{
    [Theory]
    [InlineData("array")]
    [InlineData("pointer")]
    [InlineData("function-pointer")]
    [InlineData("pinned")]
    [InlineData("sentinel")]
    public void HasDirectAttribute_WrappedType_DoesNotBorrowElementAttributes(string shape)
    {
        var module = TestModuleFactory.Create();
        using var system = new CecilTypeSystem(module);
        var (marked, marker) = AddAttributedType(module);
        TypeReference wrapped = shape switch
        {
            "array" => new ArrayType(marked),
            "pointer" => new PointerType(marked),
            "function-pointer" => new FunctionPointerType { ReturnType = marked },
            "pinned" => new PinnedType(marked),
            "sentinel" => new SentinelType(marked),
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        };

        Assert.Equal(MatchStatus.Matched, system.HasDirectAttribute(
            AttributeTarget.ForType(system.Mapper.MapType(marked)), system.Mapper.MapType(marker)).Status);
        Assert.Equal(MatchStatus.NotMatched, system.HasDirectAttribute(
            AttributeTarget.ForType(system.Mapper.MapType(wrapped)), system.Mapper.MapType(marker)).Status);
    }

    [Fact]
    public void HasDirectAttribute_ConstructedNamedType_PreservesDefinitionAttributes()
    {
        var module = TestModuleFactory.Create();
        using var system = new CecilTypeSystem(module);
        var (marked, marker) = AddAttributedType(module, generic: true);
        var constructed = new GenericInstanceType(marked);
        constructed.GenericArguments.Add(module.TypeSystem.Int32);

        Assert.Equal(MatchStatus.Matched, system.HasDirectAttribute(
            AttributeTarget.ForType(system.Mapper.MapType(constructed)), system.Mapper.MapType(marker)).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsAssignableTo_DetachedGenericParameter_PreservesImplicitRoots(bool referenceOwner)
    {
        var module = TestModuleFactory.Create();
        using var system = new CecilTypeSystem(module);
        TypeReference owner = referenceOwner
            ? new TypeReference("Example", "Owner`1", null, null)
            : new TypeDefinition("Example", "Owner`1", TypeAttributes.Public, module.TypeSystem.Object);
        var parameter = new GenericParameter("T", owner) { HasNotNullableValueTypeConstraint = true };
        owner.GenericParameters.Add(parameter);
        Assert.Null(parameter.Module);
        var mapped = system.Mapper.MapType(parameter);
        var valueType = new TypeReference("System", "ValueType", module, module.TypeSystem.CoreLibrary);

        Assert.Equal(MatchStatus.Matched, system.IsAssignableTo(mapped, system.Mapper.MapType(module.TypeSystem.Object)).Status);
        Assert.Equal(MatchStatus.Matched, system.IsAssignableTo(mapped, system.Mapper.MapType(valueType)).Status);
        var closure = system.GetInheritanceClosure(mapped);
        Assert.Contains(closure.Ancestors, type => type is NamedTypeSignature { Identity.FullMetadataName: "System.Object" });
        Assert.Contains(closure.Ancestors, type => type is NamedTypeSignature { Identity.FullMetadataName: "System.ValueType" });
        if (!referenceOwner) return;
        Assert.NotNull(closure.Failure);
        Assert.Contains(closure.Failure.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.ResolutionFailure);
        var unrelated = system.IsAssignableTo(mapped, system.Mapper.MapType(module.TypeSystem.String));
        Assert.Equal(MatchStatus.Unknown, unrelated.Status);
        Assert.Contains(unrelated.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.ResolutionFailure);
    }

    private static (TypeDefinition Marked, TypeDefinition Marker) AddAttributedType(ModuleDefinition module, bool generic = false)
    {
        var marker = new TypeDefinition("Example", "MarkerAttribute", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(marker);
        var constructor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName, module.TypeSystem.Void);
        marker.Methods.Add(constructor);
        var marked = new TypeDefinition("Example", generic ? "Marked`1" : "Marked", TypeAttributes.Public, module.TypeSystem.Object);
        if (generic) marked.GenericParameters.Add(new GenericParameter("T", marked));
        marked.CustomAttributes.Add(new CustomAttribute(constructor));
        module.Types.Add(marked);
        return (marked, marker);
    }
}
