using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilDirectAttributeLimitTests
{
    [Theory]
    [InlineData(1, 62, false)]
    [InlineData(1, 63, true)]
    [InlineData(1, 65, true)]
    [InlineData(64, 0, false)]
    [InlineData(65, 0, true)]
    public void HasDirectAttribute_NativeAttributeLimit_PreservesResultChannel(int arity, int arrayLayers, bool exceedsLimit)
    {
        using var module = TestModuleFactory.Create();
        using var system = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var marked = new TypeDefinition("Example", "Marked", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(marked);
        var attribute = AddAttribute(module, marked, arity, arrayLayers);
        var target = AttributeTarget.ForType(system.Mapper.MapType(marked));
        var query = system.Mapper.MapType(module.TypeSystem.Object);
        var code = arrayLayers > 0 ? DiagnosticCodes.MetadataDepthExceeded : DiagnosticCodes.MetadataGenericArityExceeded;
        if (exceedsLimit)
        {
            var exception = Assert.Throws<AspectNLimitException>(() => system.Mapper.MapType(attribute));
            Assert.Equal(code, exception.Diagnostic.Code);
        }
        else query = system.Mapper.MapType(attribute);

        // 重复查询仍须保留结果通道，不能把失败缓存成已知空集合。
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var result = system.HasDirectAttribute(target, query);
            Assert.Equal(exceedsLimit ? MatchStatus.Unknown : MatchStatus.Matched, result.Status);
            if (exceedsLimit) Assert.Equal(code, Assert.Single(result.Diagnostics).Code);
            else Assert.Empty(result.Diagnostics);
        }

        var empty = new TypeDefinition("Example", "Empty", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(empty);
        var absent = system.HasDirectAttribute(AttributeTarget.ForType(system.Mapper.MapType(empty)), query);
        Assert.Equal(MatchStatus.NotMatched, absent.Status);
        Assert.Empty(absent.Diagnostics);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HasDirectAttribute_GenericParameterAttributeExceedsDepth_ReturnsUnknown(bool methodParameter)
    {
        using var module = TestModuleFactory.Create();
        using var system = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var owner = new TypeDefinition("Example", "Owner`1", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(owner);
        var method = new MethodDefinition("Run", MethodAttributes.Public, module.TypeSystem.Void);
        owner.Methods.Add(method);
        var parameter = new GenericParameter("T", methodParameter ? method : owner);
        if (methodParameter) method.GenericParameters.Add(parameter);
        else owner.GenericParameters.Add(parameter);
        AddAttribute(module, parameter, 1, 63);

        var result = system.HasDirectAttribute(AttributeTarget.ForType(system.Mapper.MapType(parameter)),
            system.Mapper.MapType(module.TypeSystem.Object));

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Equal(DiagnosticCodes.MetadataDepthExceeded, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void HasDirectAttribute_OverLimitRecordAndPreCanceledToken_ThrowsCancellation()
    {
        using var module = TestModuleFactory.Create();
        using var system = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var marked = new TypeDefinition("Example", "Marked", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(marked);
        AddAttribute(module, marked, 1, 63);
        var target = AttributeTarget.ForType(system.Mapper.MapType(marked));
        var query = system.Mapper.MapType(module.TypeSystem.Object);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => system.HasDirectAttribute(target, query, cancellation.Token));
    }

    private static GenericInstanceType AddAttribute(ModuleDefinition module, ICustomAttributeProvider target, int arity, int arrayLayers)
    {
        var definition = new TypeDefinition("Example", "Marker`" + arity, TypeAttributes.Public,
            module.ImportReference(typeof(Attribute)));
        module.Types.Add(definition);
        for (var index = 0; index < arity; index++)
            definition.GenericParameters.Add(new GenericParameter("T" + index, definition));
        var attribute = new GenericInstanceType(definition);
        TypeReference argument = module.TypeSystem.Int32;
        for (var index = 0; index < arrayLayers; index++) argument = new ArrayType(argument);
        for (var index = 0; index < arity; index++) attribute.GenericArguments.Add(argument);
        target.CustomAttributes.Add(new CustomAttribute(new MethodReference(".ctor", module.TypeSystem.Void, attribute) { HasThis = true }));
        return attribute;
    }
}
