using AspectN.Evaluation;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilResourceBudgetTests
{
    [Fact]
    public void MapType_LongNamespace_ChecksBeforeConcatenation()
    {
        using var module = ModuleDefinition.CreateModule("Text", ModuleKind.Dll);
        var type = new TypeDefinition(new string('N', 1_000_000), "C", TypeAttributes.Public);
        module.Types.Add(type);
        using var system = new CecilTypeSystem(module, new CecilMappingOptions(resources:
            new AspectNMappingOptions(maximumWork: ResourceLimit.Unlimited, maximumGeneratedTextLength: ResourceLimit.Finite(128))));
        system.Mapper.MapType(module.TypeSystem.Int32);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var error = Assert.Throws<AspectNLimitException>(() => system.Mapper.MapType(type));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal("AN3008", error.Diagnostic.Code);
        Assert.True(allocated < 128 * 1024, $"Allocated {allocated} bytes before namespace text checks.");
    }

    [Fact]
    public void MapType_ManyCallingConventions_StopsBeforeFormattingWholeList()
    {
        using var module = ModuleDefinition.CreateModule("Conventions", ModuleKind.Dll);
        var marker = new CountingConvention(module);
        TypeReference returned = module.TypeSystem.Void;
        for (var index = 0; index < 10_000; index++) returned = new OptionalModifierType(marker, returned);
        var function = new FunctionPointerType { ReturnType = returned, CallingConvention = MethodCallingConvention.Unmanaged };
        using var system = new CecilTypeSystem(module, new CecilMappingOptions(resources:
            new AspectNMappingOptions(maximumTypeDepth: ResourceLimit.Unlimited, maximumWork: ResourceLimit.Unlimited,
                maximumGeneratedTextLength: ResourceLimit.Finite(128))));
        Assert.Equal("AN3008", Assert.Throws<AspectNLimitException>(() => system.Mapper.MapType(function)).Diagnostic.Code);
        Assert.True(marker.Reads < 1000, $"Read the calling-convention name {marker.Reads} times before checking text limits.");
    }

    [Fact]
    public void MapType_FailedPublication_RetryKeepsNativeAssociation()
    {
        var retried = 0;
        for (var quota = 1; quota <= 600; quota++)
        {
            using var module = ModuleDefinition.CreateModule("Publication", ModuleKind.Dll);
            var type = new TypeDefinition("Example", "C", TypeAttributes.Public);
            module.Types.Add(type);
            using var system = new CecilTypeSystem(module, new CecilMappingOptions(resources:
                new AspectNMappingOptions(maximumWork: ResourceLimit.Finite(quota))));
            try { system.Mapper.MapType(type); continue; }
            catch (AspectNLimitException) { }
            AspectN.Metadata.TypeSignature signature;
            try { signature = system.Mapper.MapType(type); }
            catch (AspectNLimitException) { continue; }
            retried++;
            var closure = system.GetInheritanceClosure(signature);
            Assert.Null(closure.Failure);
            Assert.Empty(closure.Ancestors);
        }
        Assert.True(retried > 0);
    }

    [Fact]
    public void MapType_TypeArguments_DoNotUseMethodArityLimit()
    {
        using var module = ModuleDefinition.CreateModule("IndependentArity", ModuleKind.Dll);
        var type = new TypeDefinition("Example", "Pair`2", TypeAttributes.Public);
        type.GenericParameters.Add(new GenericParameter("T", type));
        type.GenericParameters.Add(new GenericParameter("U", type));
        module.Types.Add(type);
        var constructed = new GenericInstanceType(type);
        constructed.GenericArguments.Add(module.TypeSystem.Int32);
        constructed.GenericArguments.Add(module.TypeSystem.String);
        using var system = new CecilTypeSystem(module, new CecilMappingOptions(resources:
            new AspectNMappingOptions(maximumMethodGenericParameters: ResourceLimit.Finite(1))));
        Assert.IsType<AspectN.Metadata.NamedTypeSignature>(system.Mapper.MapType(constructed));
    }

    private sealed class CountingConvention(ModuleDefinition module) : TypeReference(
        "System.Runtime.CompilerServices", "CallConvCdecl", module, module.TypeSystem.CoreLibrary)
    {
        public int Reads { get; private set; }
        public override string Name { get { Reads++; return base.Name; } set => base.Name = value; }
    }
}
