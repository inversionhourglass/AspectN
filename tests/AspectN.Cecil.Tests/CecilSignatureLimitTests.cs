using AspectN.Diagnostics;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilSignatureLimitTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MapMethod_ResolvedCandidateExceedsDepth_RejectsBeforeCanonicalText(bool returnType)
    {
        using var module = ModuleDefinition.CreateModule("ResolvedLimit", ModuleKind.Dll);
        var owner = new TypeDefinition("Example", "Owner", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(owner);
        var leaf = new ObservedTypeReference(module);
        TypeReference deep = leaf;
        for (var index = 0; index < 64; index++) deep = new ArrayType(deep);
        var definition = new MethodDefinition("Run", MethodAttributes.Public | MethodAttributes.Static,
            returnType ? deep : module.TypeSystem.Void);
        definition.Parameters.Add(new ParameterDefinition(returnType ? module.TypeSystem.Int32 : deep));
        owner.Methods.Add(definition);
        var reference = new ResolvedMethodReference(definition, module.TypeSystem.Void, owner) { HasThis = false };
        reference.Parameters.Add(new ParameterDefinition(module.TypeSystem.Int32));
        using var system = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var reads = leaf.NameReads;

        var exception = Assert.Throws<AspectNLimitException>(() => system.Mapper.MapMethod(reference));

        Assert.Equal(DiagnosticCodes.MetadataDepthExceeded, exception.Diagnostic.Code);
        Assert.Equal(reads, leaf.NameReads);
    }

    [Theory]
    [InlineData(64, false)]
    [InlineData(65, false)]
    [InlineData(64, true)]
    [InlineData(65, true)]
    public void MapType_DeclaredParameterCountExceedsLimit_RejectsDespiteMetadataName(int arity, bool definition)
    {
        using var module = ModuleDefinition.CreateModule("DeclaredLimit", ModuleKind.Dll);
        var name = arity == 65 ? "Owner`1" : "Owner`64";
        TypeReference type = definition
            ? new TypeDefinition("Example", name, TypeAttributes.Public, module.TypeSystem.Object)
            : new TypeReference("Example", name, module, module);
        for (var index = 0; index < arity; index++) type.GenericParameters.Add(new GenericParameter("T" + index, type));
        using var system = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));

        if (arity == 65)
        {
            var exception = Assert.Throws<AspectNLimitException>(() => system.Mapper.MapType(type));
            Assert.Equal(DiagnosticCodes.MetadataGenericArityExceeded, exception.Diagnostic.Code);
        }
        else Assert.NotNull(system.Mapper.MapType(type));
    }

    private sealed class ResolvedMethodReference(MethodDefinition definition, TypeReference result, TypeReference owner)
        : MethodReference("Run", result, owner)
    {
        public override MethodDefinition Resolve() => definition;
    }

    private sealed class ObservedTypeReference(ModuleDefinition module) : TypeReference("Example", "Leaf", module, module)
    {
        public int NameReads { get; private set; }
        public override string Name
        {
            get { NameReads++; return base.Name; }
            set => base.Name = value;
        }
    }
}
