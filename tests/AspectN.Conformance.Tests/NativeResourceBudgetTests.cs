using AspectN.Cecil;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Reflection;
using AspectN.Roslyn;
using AspectN.TypeSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Mono.Cecil;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class NativeResourceBudgetTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MapMethod_RaisedAndUnlimitedArity_AgreesAcrossNativeViews(bool unlimited)
    {
        var slots = string.Join(",", Enumerable.Range(0, 80).Select(index => "T" + index));
        var arguments = string.Join(",", Enumerable.Repeat("int", 80));
        var compilation = CSharpCompilation.Create("RaisedArity",
            [CSharpSyntaxTree.ParseText($"public class Many<{slots}> {{}} public class C {{ public Many<{arguments}> M<{slots}>() => null; }}")],
            FixtureCompiler.TrustedPlatformReferences(), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var image = FixtureCompiler.Emit(compilation);
        var limit = unlimited ? ResourceLimit.Unlimited : ResourceLimit.Finite(80);
        var mapping = new AspectNMappingOptions(maximumTypeGenericParameters: limit,
            maximumMethodGenericParameters: limit, maximumWork: ResourceLimit.Unlimited);
        var match = new AspectNMatchOptions(maximumTypeGenericParameters: limit,
            maximumMethodGenericParameters: limit, maximumWork: ResourceLimit.Unlimited);
        var backend = new RoslynTypeSystem(compilation, mapping);
        var peCompilation = CSharpCompilation.Create("Reader", references:
            FixtureCompiler.TrustedPlatformReferences().Append(MetadataReference.CreateFromImage(image)));
        var peBackend = new RoslynTypeSystem(peCompilation, mapping);
        using var module = ModuleDefinition.ReadModule(new MemoryStream(image));
        using var cecil = new CecilTypeSystem(module, new CecilMappingOptions(resources: mapping));
        var load = new System.Runtime.Loader.AssemblyLoadContext("RaisedArity", isCollectible: true);
        try
        {
            var assembly = load.LoadFromStream(new MemoryStream(image));
            var reflection = new ReflectionTypeSystem(mapping);
            var methods = new[]
            {
                (System: (ITypeSystem)backend, Method: backend.Mapper.MapMethod((IMethodSymbol)compilation.GetTypeByMetadataName("C")!.GetMembers("M").Single())),
                (System: (ITypeSystem)peBackend, Method: peBackend.Mapper.MapMethod((IMethodSymbol)peCompilation.GetTypeByMetadataName("C")!.GetMembers("M").Single())),
                (System: (ITypeSystem)cecil, Method: cecil.Mapper.MapMethod(module.GetType("C").Methods.Single(method => method.Name == "M"))),
                (System: (ITypeSystem)reflection, Method: reflection.Mapper.MapMethod(assembly.GetType("C")!.GetMethod("M")!))
            };
            Assert.True(new AspectNEngine().CompileMemberExpression("method(* M<?>(..))",
                compilationOptions: new AspectNCompilationOptions(matchOptions: match)).TryGetMemberPattern(out var pattern));
            foreach (var (system, method) in methods)
            {
                Assert.Equal(80, method.GenericParameters.Count);
                Assert.Equal(80, Assert.IsType<NamedTypeSignature>(method.ReturnType.Type).TypeArguments.Count);
                Assert.Equal(MatchStatus.Matched, pattern.Match(method, system).Status);
                Assert.Equal(MatchStatus.Unknown, system.AreEqual(method.ReturnType.Type, method.ReturnType.Type).Status);
            }
        }
        finally
        {
            load.Unload();
        }
    }

    [Fact]
    public void Reflection_XmlScopeExample_UsesCompiledQueryOptions()
    {
        var backend = new ReflectionTypeSystem(queryOptions: new AspectNMatchOptions(maximumWork: ResourceLimit.Finite(1)));
        var candidate = backend.Mapper.MapType(typeof(string));
        AssertResource(backend.AreEqual(candidate, candidate));
        var compilation = new AspectNEngine().CompileTypePattern("IComparable+");
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        Assert.Equal(MatchStatus.Matched, pattern.Match(candidate, backend).Status);
    }

    [Theory]
    [InlineData("Cecil")]
    [InlineData("Reflection")]
    [InlineData("Roslyn")]
    public void MapType_TypeAndMethodArityLimits_AreIndependent(string backend)
    {
        using var methodLimited = Create(backend, new AspectNMappingOptions(maximumMethodGenericParameters: ResourceLimit.Finite(1)), closed: true);
        Assert.IsType<NamedTypeSignature>(methodLimited.MapType(default));
        using var typeLimited = Create(backend, new AspectNMappingOptions(maximumTypeGenericParameters: ResourceLimit.Finite(1)), closed: true);
        Assert.Equal("AN3006", Assert.Throws<AspectNLimitException>(() => typeLimited.MapType(default)).Diagnostic.Code);
    }

    [Theory]
    [InlineData("Cecil")]
    [InlineData("Reflection")]
    [InlineData("Roslyn")]
    public void Query_DepthOptions_ApplyToStandaloneClosure(string backend)
    {
        using var scope = Create(backend, query: new AspectNMatchOptions(maximumTypeDepth: ResourceLimit.Finite(1)), closed: true);
        var candidate = scope.MapType(default);
        var closure = scope.System.GetInheritanceClosure(candidate);
        Assert.NotNull(closure.Failure);
        Assert.Contains(closure.Failure.Diagnostics, diagnostic => diagnostic.Code == "AN3005");
        Assert.All(closure.Ancestors, ancestor => Assert.NotEqual(MatchStatus.Unknown, scope.System.AreEqual(ancestor, ancestor).Status));
        var missing = new NamedTypeSignature(new TypeIdentity(new AspectN.Metadata.AssemblyIdentity(""), "Example", "Missing", 0, AspectN.Metadata.TypeKind.Unknown));
        var attributeResult = scope.System.HasDirectAttribute(AttributeTarget.ForType(candidate), missing);
        Assert.Equal(MatchStatus.Unknown, attributeResult.Status);
        Assert.Contains(attributeResult.Diagnostics, diagnostic => diagnostic.Code == "AN3005");
    }

    [Theory]
    [InlineData("Cecil")]
    [InlineData("Reflection")]
    [InlineData("Roslyn")]
    public void MapType_WorkAndTextLimits_ThrowAndRespectCancellation(string backend)
    {
        using (var scope = Create(backend, new AspectNMappingOptions(maximumWork: ResourceLimit.Finite(1))))
            Assert.Equal("AN3008", Assert.Throws<AspectNLimitException>(() => scope.MapType(default)).Diagnostic.Code);
        using (var scope = Create(backend, new AspectNMappingOptions(maximumGeneratedTextLength: ResourceLimit.Finite(8))))
            Assert.Equal("AN3008", Assert.Throws<AspectNLimitException>(() => scope.MapVariable()).Diagnostic.Code);
        using (var scope = Create(backend))
        {
            scope.MapType(default);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => scope.MapType(cancellation.Token));
        }
    }

    [Theory]
    [InlineData("Cecil")]
    [InlineData("Reflection")]
    [InlineData("Roslyn")]
    public void MapMethod_AttributeLimit_CountsAllScannedRecords(string backend)
    {
        using var limited = Create(backend, new AspectNMappingOptions(maximumAttributeRecords: ResourceLimit.Finite(2)));
        Assert.Equal("AN3008", Assert.Throws<AspectNLimitException>(() => limited.MapMethod()).Diagnostic.Code);
        using var unlimited = Create(backend, new AspectNMappingOptions(maximumAttributeRecords: ResourceLimit.Unlimited));
        Assert.Equal(3, unlimited.MapMethod().Attributes.Count);
    }

    [Theory]
    [InlineData("Cecil")]
    [InlineData("Reflection")]
    [InlineData("Roslyn")]
    public void Query_RelationAndAttributeLimits_AreIndependentPerCall(string backend)
    {
        using var scope = Create(backend, query: new AspectNMatchOptions(maximumRelationEdges: ResourceLimit.Finite(1),
            maximumAttributeRecords: ResourceLimit.Finite(2)));
        var candidate = scope.MapType(default);
        var first = scope.System.GetInheritanceClosure(candidate);
        var second = scope.System.GetInheritanceClosure(candidate);
        AssertResource(first.Failure!);
        AssertResource(second.Failure!);
        var attribute = new NamedTypeSignature(new TypeIdentity(new AspectN.Metadata.AssemblyIdentity(""),
            "AspectN.Conformance.Tests", "BudgetMarkerAttribute", 0, AspectN.Metadata.TypeKind.Unknown));
        // 不存在的目标会扫描完整列表，不能因第一项命中提前结束。
        attribute = new NamedTypeSignature(new TypeIdentity(attribute.Identity.Assembly,
            attribute.Identity.Namespace, "MissingAttribute", 0, AspectN.Metadata.TypeKind.Unknown));
        AssertResource(scope.System.HasDirectAttribute(AttributeTarget.ForType(candidate), attribute));
    }

    [Theory]
    [InlineData("Cecil")]
    [InlineData("Reflection")]
    [InlineData("Roslyn")]
    public void Match_InternalQueries_UseCompiledOptionsInsteadOfStandaloneDefaults(string backend)
    {
        using var scope = Create(backend, query: new AspectNMatchOptions(maximumWork: ResourceLimit.Finite(1)));
        var candidate = scope.MapType(default);
        AssertResource(scope.System.AreEqual(candidate, candidate));
        var compilation = new AspectNEngine().CompileTypePattern("IResourceA+");
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        Assert.Equal(MatchStatus.Matched, pattern.Match(candidate, scope.System).Status);
    }

    [Theory]
    [InlineData("Cecil")]
    [InlineData("Reflection")]
    [InlineData("Roslyn")]
    public void Query_MappingSubBudget_ReportsResourceExhaustion(string backend)
    {
        using var scope = Create(backend, mapping: new AspectNMappingOptions(
            maximumAttributeRecords: ResourceLimit.Finite(2)));
        var candidate = scope.MapType(default);
        var absent = new NamedTypeSignature(new TypeIdentity(new AspectN.Metadata.AssemblyIdentity(""),
            "", "AbsentAttribute", 0, AspectN.Metadata.TypeKind.Unknown));

        var result = scope.System.HasDirectAttribute(AttributeTarget.ForType(candidate), absent);

        AssertResource(result);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("Mapping", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Cecil")]
    [InlineData("Reflection")]
    [InlineData("Roslyn")]
    public void Match_RepeatedInternalQueries_ShareRelationEdges(string backend)
    {
        using var scope = Create(backend);
        var candidate = scope.MapType(default);
        var options = new AspectNCompilationOptions(matchOptions: new AspectNMatchOptions(
            maximumRelationEdges: ResourceLimit.Finite(3)));
        var engine = new AspectNEngine();
        Assert.True(engine.CompileTypePattern("IResourceA+", compilationOptions: options).TryGetTypePattern(out var single));
        Assert.Equal(MatchStatus.Matched, single.Match(candidate, scope.System).Status);
        Assert.True(engine.CompileTypePattern("IResourceA+ && IResourceA+ && IResourceA+",
            compilationOptions: options).TryGetTypePattern(out var repeated));

        var result = repeated.Match(candidate, scope.System);

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AN3008" &&
            diagnostic.Message.Contains("MaximumRelationEdges", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Cecil")]
    [InlineData("Reflection")]
    [InlineData("Roslyn")]
    public void Query_RelationNodeLimit_CountsRootAndResetsEachCall(string backend)
    {
        using var scope = Create(backend, query: new AspectNMatchOptions(
            maximumRelationNodes: ResourceLimit.Finite(1)));
        var candidate = scope.MapType(default);
        for (var call = 0; call < 2; call++)
        {
            var result = scope.System.GetInheritanceClosure(candidate);
            AssertResource(result.Failure!);
            Assert.Contains(result.Failure!.Diagnostics, diagnostic =>
                diagnostic.Message.Contains("MaximumRelationNodes", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("Cecil")]
    [InlineData("Reflection")]
    [InlineData("Roslyn")]
    public void Match_ThirdPartyDecorator_UsesOrdinaryQueryBoundary(string backend)
    {
        using var scope = Create(backend);
        var candidate = scope.MapType(default);
        Assert.True(new AspectNEngine().CompileTypePattern("IResourceA+", compilationOptions:
            new AspectNCompilationOptions(matchOptions: new AspectNMatchOptions(
                maximumRelationEdges: ResourceLimit.Finite(1)))).TryGetTypePattern(out var pattern));

        Assert.Equal(MatchStatus.Unknown, pattern.Match(candidate, scope.System).Status);
        Assert.Equal(MatchStatus.Matched, pattern.Match(candidate, new OrdinaryTypeSystem(scope.System)).Status);
    }

    private sealed class OrdinaryTypeSystem(ITypeSystem inner) : ITypeSystem
    {
        public TypeSystemCapabilities Capabilities => inner.Capabilities;
        public TypeRelationResult AreEqual(TypeSignature left, TypeSignature right, CancellationToken cancellationToken = default)
            => inner.AreEqual(left, right, cancellationToken);
        public TypeRelationResult IsAssignableTo(TypeSignature candidate, TypeSignature target, CancellationToken cancellationToken = default)
            => inner.IsAssignableTo(candidate, target, cancellationToken);
        public InheritanceClosure GetInheritanceClosure(TypeSignature candidate, CancellationToken cancellationToken = default)
            => inner.GetInheritanceClosure(candidate, cancellationToken);
        public TypeRelationResult HasDirectAttribute(AttributeTarget target, TypeSignature attributeType, CancellationToken cancellationToken = default)
            => inner.HasDirectAttribute(target, attributeType, cancellationToken);
    }

    private static void AssertResource(TypeRelationResult result)
    {
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AN3008");
    }

    private static Scope Create(string backend, AspectNMappingOptions? mapping = null, AspectNMatchOptions? query = null, bool closed = false)
    {
        if (backend == "Reflection")
        {
            var system = new ReflectionTypeSystem(mapping, query);
            var native = closed ? typeof(ResourceNativeFixture<int[], int>) : typeof(ResourceNativeFixture<,>);
            return new(system, token => system.Mapper.MapType(native, token),
                () => system.Mapper.MapType(typeof(ResourceNativeFixture<,>).GetGenericArguments()[0]),
                () => system.Mapper.MapMethod(typeof(ResourceNativeFixture<,>).GetMethod("M")!));
        }
        if (backend == "Cecil")
        {
            var module = ModuleDefinition.ReadModule(typeof(ResourceNativeFixture<,>).Assembly.Location);
            var system = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: false, resources: mapping), query);
            var type = module.GetType("AspectN.Conformance.Tests.ResourceNativeFixture`2");
            TypeReference native = type;
            if (closed)
            {
                var constructed = new GenericInstanceType(type);
                constructed.GenericArguments.Add(new ArrayType(module.TypeSystem.Int32));
                constructed.GenericArguments.Add(module.TypeSystem.Int32);
                native = constructed;
            }
            return new(system, token => system.Mapper.MapType(native, token), () => system.Mapper.MapType(type.GenericParameters[0]),
                () => system.Mapper.MapMethod(type.Methods.Single(method => method.Name == "M")), system);
        }
        const string source = """
            using System;
            namespace AspectN.Conformance.Tests;
            [AttributeUsage(AttributeTargets.All, AllowMultiple=true)] public class BudgetMarkerAttribute : Attribute {}
            public interface IResourceA {} public interface IResourceB {}
            [BudgetMarker, BudgetMarker, BudgetMarker]
            public class ResourceNativeFixture<T,U> : IResourceA, IResourceB {
                [BudgetMarker, BudgetMarker, BudgetMarker] public void M() {} }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("Resources", [CSharpSyntaxTree.ParseText(source)], references);
        var roslyn = new RoslynTypeSystem(compilation, mapping, query);
        var symbol = compilation.GetTypeByMetadataName("AspectN.Conformance.Tests.ResourceNativeFixture`2")!;
        var nativeSymbol = closed ? symbol.Construct(compilation.CreateArrayTypeSymbol(compilation.GetSpecialType(SpecialType.System_Int32)),
            compilation.GetSpecialType(SpecialType.System_Int32)) : symbol;
        return new(roslyn, token => roslyn.Mapper.MapType(nativeSymbol, token), () => roslyn.Mapper.MapType(symbol.TypeParameters[0]),
            () => roslyn.Mapper.MapMethod(symbol.GetMembers("M").OfType<IMethodSymbol>().Single()));
    }

    private sealed class Scope(ITypeSystem system, Func<CancellationToken, TypeSignature> mapType,
        Func<TypeSignature> mapVariable, Func<MethodSignature> mapMethod, IDisposable? owner = null) : IDisposable
    {
        public ITypeSystem System { get; } = system;
        public Func<CancellationToken, TypeSignature> MapType { get; } = mapType;
        public Func<TypeSignature> MapVariable { get; } = mapVariable;
        public Func<MethodSignature> MapMethod { get; } = mapMethod;
        public void Dispose() => owner?.Dispose();
    }
}

[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
public sealed class BudgetMarkerAttribute : Attribute;
public interface IResourceA;
public interface IResourceB;
[BudgetMarker, BudgetMarker, BudgetMarker]
public sealed class ResourceNativeFixture<T, TU> : IResourceA, IResourceB
{
    [BudgetMarker, BudgetMarker, BudgetMarker]
    public void M() { }
}
