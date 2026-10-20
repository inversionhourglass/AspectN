using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilGenericParameterAttributeTests
{
    [Theory]
    [InlineData(false, 0, MatchStatus.Matched)]
    [InlineData(false, 1, MatchStatus.NotMatched)]
    [InlineData(true, 0, MatchStatus.Matched)]
    [InlineData(true, 1, MatchStatus.NotMatched)]
    public void HasDirectAttribute_GenericParameterDeclaration_ReadsOwnRecords(bool methodParameter, int position, MatchStatus expected)
    {
        using var module = ModuleDefinition.ReadModule(typeof(CecilGenericParameterAttributeTests).Assembly.Location);
        using var system = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var owner = module.GetType(MetadataName(typeof(Fixture<,>)));
        var method = owner.Methods.Single(item => item.Name == "M");
        var parameter = (methodParameter ? method.GenericParameters : owner.GenericParameters)[position];
        Assert.Null(parameter.Resolve());
        Assert.True(owner.HasCustomAttributes);
        Assert.True(method.HasCustomAttributes);
        Assert.Equal(expected == MatchStatus.Matched, parameter.HasCustomAttributes);
        var native = methodParameter ? typeof(Fixture<,>).GetMethod("M")!.GetGenericArguments()[position]
            : typeof(Fixture<,>).GetGenericArguments()[position];
        Assert.Equal(expected == MatchStatus.Matched,
            native.CustomAttributes.Any(item => item.AttributeType == typeof(MarkerAttribute)));

        var result = system.HasDirectAttribute(AttributeTarget.ForType(system.Mapper.MapType(parameter)),
            system.Mapper.MapType(module.GetType(MetadataName(typeof(MarkerAttribute)))));

        Assert.Equal(expected, result.Status);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void HasDirectAttribute_GenericParameterWithoutAssociation_RemainsUnknown()
    {
        using var module = TestModuleFactory.Create();
        using var system = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var signature = new GenericParameterTypeSignature(new GenericParameterSignature(
            "T", GenericParameterOwnerKind.Type, "Missing.Owner`1", new AssemblyIdentity("Missing"), 0));
        var result = system.HasDirectAttribute(AttributeTarget.ForType(signature), system.Mapper.MapType(module.TypeSystem.Object));
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, item => item.Code == DiagnosticCodes.MissingBackendCapability);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void HasDirectAttribute_ReferenceAndDeclarationMappingOrder_PreservesOwnRecords(bool methodParameter, bool referenceFirst)
    {
        using var module = ModuleDefinition.ReadModule(typeof(CecilGenericParameterAttributeTests).Assembly.Location);
        using var system = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var owner = module.GetType(MetadataName(typeof(Fixture<,>)));
        var method = owner.Methods.Single(item => item.Name == "M");
        var referenceOwner = new TypeReference(owner.Namespace, owner.Name, module, owner.Scope)
        {
            DeclaringType = owner.DeclaringType
        };
        foreach (var parameter in owner.GenericParameters)
            referenceOwner.GenericParameters.Add(new GenericParameter(parameter.Name, referenceOwner));
        var referenceMethod = new MethodReference(method.Name, method.ReturnType, referenceOwner)
        {
            HasThis = method.HasThis,
            ExplicitThis = method.ExplicitThis,
            CallingConvention = method.CallingConvention
        };
        foreach (var parameter in method.GenericParameters)
            referenceMethod.GenericParameters.Add(new GenericParameter(parameter.Name, referenceMethod));
        var attribute = system.Mapper.MapType(module.GetType(MetadataName(typeof(MarkerAttribute))));

        for (var position = 0; position < 2; position++)
        {
            var declaration = (methodParameter ? method.GenericParameters : owner.GenericParameters)[position];
            var reference = (methodParameter ? referenceMethod.GenericParameters : referenceOwner.GenericParameters)[position];
            Assert.Equal(declaration.FullName, reference.FullName);
            Assert.False(reference.HasCustomAttributes);
            var first = system.Mapper.MapType(referenceFirst ? reference : declaration);
            var second = system.Mapper.MapType(referenceFirst ? declaration : reference);
            var mappedDeclaration = referenceFirst ? second : first;
            var mappedReference = referenceFirst ? first : second;
            Assert.Equal(MatchStatus.Matched, system.AreEqual(mappedDeclaration, mappedReference).Status);

            var known = system.HasDirectAttribute(AttributeTarget.ForType(mappedDeclaration), attribute);
            Assert.Equal(position == 0 ? MatchStatus.Matched : MatchStatus.NotMatched, known.Status);
            Assert.Empty(known.Diagnostics);
            var unknown = system.HasDirectAttribute(AttributeTarget.ForType(mappedReference), attribute);
            Assert.Equal(MatchStatus.Unknown, unknown.Status);
            Assert.Contains(unknown.Diagnostics, item => item.Code == DiagnosticCodes.ResolutionFailure);
        }
    }

    [Fact]
    public void HasDirectAttribute_GenericParameterOnUnresolvedReference_RemainsUnknown()
    {
        using var module = TestModuleFactory.Create();
        using var system = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var missing = new AssemblyNameReference("Missing.GenericOwner", new Version(1, 0));
        var owner = new TypeReference("Missing", "Owner`1", module, missing);
        var parameter = new GenericParameter("T", owner);
        owner.GenericParameters.Add(parameter);
        var result = system.HasDirectAttribute(AttributeTarget.ForType(system.Mapper.MapType(parameter)),
            system.Mapper.MapType(module.TypeSystem.Object));
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, item => item.Code == DiagnosticCodes.ResolutionFailure);
    }

    [Fact]
    public void HasDirectAttribute_GenericParameter_PreCancelledThrows()
    {
        using var module = ModuleDefinition.ReadModule(typeof(CecilGenericParameterAttributeTests).Assembly.Location);
        using var system = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var parameter = system.Mapper.MapType(module.GetType(MetadataName(typeof(Fixture<,>))).GenericParameters[0]);
        var attribute = system.Mapper.MapType(module.GetType(MetadataName(typeof(MarkerAttribute))));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            system.HasDirectAttribute(AttributeTarget.ForType(parameter), attribute, cancellation.Token));
    }

    private static string MetadataName(Type type) => type.FullName!.Replace('+', '/');

    [AttributeUsage(AttributeTargets.All)]
    private sealed class MarkerAttribute : Attribute
    {
        public MarkerAttribute() => throw new InvalidOperationException("特性查询不能实例化特性对象。");
    }

    [Marker]
    private sealed class Fixture<[Marker] TMarked, TPlain>
    {
        [Marker]
        public static void M<[Marker] UMarked, UPlain>() { }
    }
}
