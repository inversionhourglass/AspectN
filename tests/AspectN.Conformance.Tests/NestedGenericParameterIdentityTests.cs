using System.Reflection;
using System.Runtime.Loader;
using AspectN.Cecil;
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

// 同一 Roslyn compilation 的源符号与其 emit 后字节在三适配器间的泛型参数身份对照：
// 同时覆盖外层 T 与内层 U 的实际成员使用处，逐字段比较结构身份。
public sealed class NestedGenericParameterIdentityTests
{
    private const string SOURCE = """
        namespace Review;

        public class Outer<T>
        {
            public class Inner<U>
            {
                public void M<V>(T x) { }

                public void M<V>(U x) { }
            }
        }
        """;

    [Fact]
    public void NestedMemberParameters_ThreeBackends_ProduceIdenticalIdentity()
    {
        var compilation = CSharpCompilation.Create(
            "AspectN.Conformance.NestedIdentity",
            [CSharpSyntaxTree.ParseText(
                SOURCE,
                CSharpParseOptions.Default.WithLanguageVersion(
                    LanguageVersion.Latest))],
            FixtureCompiler.TrustedPlatformReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                deterministic: true));
        var image = FixtureCompiler.Emit(compilation);
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(
            Path.GetDirectoryName(typeof(object).Assembly.Location)!);
        var loadContext = new AssemblyLoadContext(
            $"AspectN.Conformance.NestedIdentity.{Guid.NewGuid():N}",
            isCollectible: true);
        try
        {
            using var cecil = new CecilTypeSystem(
                ModuleDefinition.ReadModule(
                    new MemoryStream(image, writable: false),
                    new ReaderParameters
                    {
                        AssemblyResolver = resolver,
                        InMemory = true
                    }));
            var reflection = new ReflectionTypeSystem();
            var roslyn = new RoslynTypeSystem(compilation);
            var reflectionAssembly = loadContext.LoadFromStream(
                new MemoryStream(image, writable: false));

            var roslynT = MapRoslynParameter(roslyn, compilation, "T");
            var roslynU = MapRoslynParameter(roslyn, compilation, "U");
            var cecilT = MapCecilParameter(cecil, 0);
            var cecilU = MapCecilParameter(cecil, 1);
            var reflectionT = MapReflectionParameter(
                reflection,
                reflectionAssembly,
                0);
            var reflectionU = MapReflectionParameter(
                reflection,
                reflectionAssembly,
                1);

            AssertIdentical(roslynT, cecilT);
            AssertIdentical(roslynT, reflectionT);
            AssertIdentical(roslynU, cecilU);
            AssertIdentical(roslynU, reflectionU);
            Assert.Equal(
                MatchStatus.NotMatched,
                StructuralTypeComparer.AreEqual(
                    new GenericParameterTypeSignature(roslynT),
                    new GenericParameterTypeSignature(roslynU)).Status);
        }
        finally
        {
            resolver.Dispose();
            loadContext.Unload();
        }
    }

    private static void AssertIdentical(
        GenericParameterSignature expected,
        GenericParameterSignature actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.OwnerKind, actual.OwnerKind);
        Assert.Equal(expected.OwnerId, actual.OwnerId);
        Assert.Equal(expected.OwnerAssembly, actual.OwnerAssembly);
        Assert.Equal(expected.Position, actual.Position);
        Assert.Equal(
            MatchStatus.Matched,
            StructuralTypeComparer.AreEqual(
                new GenericParameterTypeSignature(expected),
                new GenericParameterTypeSignature(actual)).Status);
    }

    private static GenericParameterSignature MapRoslynParameter(
        RoslynTypeSystem typeSystem,
        Compilation compilation,
        string parameterName)
    {
        var inner = Assert.IsAssignableFrom<INamedTypeSymbol>(
            compilation.GetTypeByMetadataName("Review.Outer`1+Inner`1"));
        var overload = Assert.Single(
            inner.GetMembers("M").OfType<IMethodSymbol>(),
            method =>
                method.Parameters[0].Type is ITypeParameterSymbol parameter &&
                parameter.Name == parameterName);
        return Assert.IsType<GenericParameterTypeSignature>(
                Assert.Single(typeSystem.Mapper.MapCallable(overload).Parameters)
                    .Type
                    .Type)
            .Parameter;
    }

    private static GenericParameterSignature MapCecilParameter(
        CecilTypeSystem typeSystem,
        int position)
    {
        var inner = typeSystem.Module.GetType("Review.Outer`1/Inner`1") ??
            throw new InvalidOperationException(
                "Cecil type 'Review.Outer`1/Inner`1' was not found.");
        var overload = Assert.Single(
            inner.Methods,
            method => method.Name == "M" &&
                method.Parameters.Count == 1 &&
                method.Parameters[0].ParameterType is GenericParameter parameter &&
                parameter.Position == position);
        return Assert.IsType<GenericParameterTypeSignature>(
                Assert.Single(typeSystem.Mapper.MapCallable(overload).Parameters)
                    .Type
                    .Type)
            .Parameter;
    }

    private static GenericParameterSignature MapReflectionParameter(
        ReflectionTypeSystem typeSystem,
        Assembly assembly,
        int position)
    {
        var inner = assembly.GetType("Review.Outer`1+Inner`1", throwOnError: true)!;
        var overload = Assert.Single(
            inner.GetMethods(),
            method =>
                method.Name == "M" &&
                method.GetParameters()[0]
                    .ParameterType
                    .GenericParameterPosition == position);
        return Assert.IsType<GenericParameterTypeSignature>(
                Assert.Single(typeSystem.Mapper.MapCallable(overload).Parameters)
                    .Type
                    .Type)
            .Parameter;
    }
}
