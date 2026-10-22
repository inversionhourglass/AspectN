using System.Runtime.Loader;
using AspectN.Cecil;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Reflection;
using AspectN.Roslyn;
using AspectN.Tests.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Mono.Cecil;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class CustomModifierOwnerIdentityTests
{
    [Fact]
    public void MapType_MetadataOnlyUnreadableSibling_PreservesReadableCandidate()
    {
        var references = FixtureCompiler.TrustedPlatformReferences().ToArray();
        var dependency = FixtureCompiler.Emit(CSharpCompilation.Create("AbsentDependency",
            [CSharpSyntaxTree.ParseText("namespace External; public class Missing {}")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));
        var image = FixtureCompiler.Emit(CSharpCompilation.Create("ReadableCandidate",
            [CSharpSyntaxTree.ParseText("""
                namespace Example;
                public static class Owner {
                    public static T M<T>(T x) => x;
                    public static T M<T>(External.Missing x, T y) => y;
                }
                """)], references.Append(MetadataReference.CreateFromImage(dependency)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));
        using var metadata = new System.Reflection.MetadataLoadContext(new System.Reflection.PathAssemblyResolver(
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)), "System.Private.CoreLib");
        var methods = metadata.LoadFromByteArray(image).GetType("Example.Owner")!.GetMethods()
            .Where(method => method.Name == "M").OrderBy(method => method.MetadataToken).ToArray();
        Assert.Single(methods[0].GetParameters());
        Assert.Throws<FileNotFoundException>(() => methods[1].GetParameters());
        var context = new AssemblyLoadContext("ReadableCandidate", isCollectible: true);
        try
        {
            var runtime = context.LoadFromStream(new MemoryStream(image)).GetType("Example.Owner")!.GetMethods()
                .Single(method => method.MetadataToken == methods[0].MetadataToken);
            Assert.Equal(42, runtime.MakeGenericMethod(typeof(int)).Invoke(null, [42]));
            var system = new ReflectionTypeSystem();
            var mapped = Assert.IsType<GenericParameterTypeSignature>(system.Mapper.MapType(methods[0].GetGenericArguments()[0]));
            Assert.Contains("#declaration:", mapped.Parameter.OwnerId);
        }
        finally { context.Unload(); }
    }

    [Fact]
    public void MapType_MetadataOnlyView_PreservesUnambiguousDeclarationIdentity()
    {
        var image = CustomModifierOwnerFixture.CreateImage();
        using var metadata = new System.Reflection.MetadataLoadContext(new System.Reflection.PathAssemblyResolver(
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)), "System.Private.CoreLib");
        var context = new AssemblyLoadContext("CustomModifierMetadataView", isCollectible: true);
        try
        {
            var runtime = context.LoadFromStream(new MemoryStream(image)).GetType("Example.Owner")!.GetMethods()
                .Where(method => method.Name == "M").ToArray();
            var readOnly = metadata.LoadFromByteArray(image).GetType("Example.Owner")!.GetMethods()
                .Where(method => method.Name == "M").ToArray();
            var system = new ReflectionTypeSystem();
            for (var index = 0; index < runtime.Length; index++)
            {
                var first = system.Mapper.MapType(runtime[index].GetGenericArguments()[0]);
                var second = system.Mapper.MapType(readOnly[index].GetGenericArguments()[0]);
                Assert.Equal(Assert.IsType<GenericParameterTypeSignature>(first).Parameter.OwnerId,
                    Assert.IsType<GenericParameterTypeSignature>(second).Parameter.OwnerId);
                Assert.Equal(MatchStatus.Matched, system.AreEqual(first, second).Status);
            }
        }
        finally { context.Unload(); }
    }

    [Theory]
    [InlineData("mixed")]
    [InlineData("array")]
    [InlineData("generic")]
    public void MapType_MetadataOnlyAmbiguousModifiers_KeepPhysicalDeclarationsSeparate(string location)
    {
        using var metadata = new System.Reflection.MetadataLoadContext(new System.Reflection.PathAssemblyResolver(
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)), "System.Private.CoreLib");
        var methods = metadata.LoadFromByteArray(CustomModifierOwnerFixture.CreateImage(location, optional: true))
            .GetType("Example.Owner")!.GetMethods().Where(method => method.Name == "M").ToArray();
        var system = new ReflectionTypeSystem();
        Assert.Equal(MatchStatus.NotMatched, system.AreEqual(
            system.Mapper.MapType(methods[0].GetGenericArguments()[0]),
            system.Mapper.MapType(methods[1].GetGenericArguments()[0])).Status);
    }

    [Fact]
    public void MapMethod_DifferentModifierKindsTypesAndPositions_HaveDifferentOwners()
    {
        var owners = new List<string>();
        foreach (var (location, optional, otherModifier) in new[]
                 { ("return", false, false), ("return", true, false), ("return", false, true),
                     ("parameter", false, false), ("element", true, false) })
        {
            using var system = new CecilTypeSystem(ModuleDefinition.ReadModule(new MemoryStream(
                CustomModifierOwnerFixture.CreateImage(location, optional, otherModifier))));
            var method = system.Module.GetType("Example.Owner").Methods[1];
            owners.Add(system.Mapper.MapMethod(method).GenericParameters[0].OwnerId);
        }
        Assert.Equal(owners.Count, owners.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("return", false, false)]
    [InlineData("return", true, false)]
    [InlineData("return", false, true)]
    [InlineData("parameter", false, false)]
    [InlineData("element", true, false)]
    [InlineData("required-order", false, false)]
    [InlineData("mixed", false, false)]
    [InlineData("array", true, false)]
    [InlineData("generic", true, false)]
    [InlineData("pointer", true, false)]
    [InlineData("function-return", true, false)]
    [InlineData("function-parameter", true, false)]
    public void MapMethod_CustomModifierOverloads_AgreeAcrossAdapters(string location, bool optional, bool otherModifier)
    {
        var image = CustomModifierOwnerFixture.CreateImage(location, optional, otherModifier);
        using var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
        using var cecil = new CecilTypeSystem(ModuleDefinition.ReadModule(new MemoryStream(image),
            new ReaderParameters { AssemblyResolver = resolver }));
        var compilation = CSharpCompilation.Create("Reader", references: FixtureCompiler.TrustedPlatformReferences()
            .Append(MetadataReference.CreateFromImage(image)), options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var roslyn = new RoslynTypeSystem(compilation);
        var reflection = new ReflectionTypeSystem();
        var context = new AssemblyLoadContext("CustomModifierIdentity", isCollectible: true);
        try
        {
            var runtime = context.LoadFromStream(new MemoryStream(image)).GetType("Example.Owner")!.GetMethods()
                .Where(method => method.Name == "M").ToArray();
            var cecilMethods = cecil.Module.GetType("Example.Owner").Methods.Select(method => cecil.Mapper.MapMethod(method)).ToArray();
            var reflectionMethods = runtime.Select(method => reflection.Mapper.MapMethod(method)).ToArray();
            var roslynMethods = compilation.GetTypeByMetadataName("Example.Owner")!.GetMembers("M")
                .OfType<IMethodSymbol>().Select(method => roslyn.Mapper.MapMethod(method)).ToArray();
            var cecilVariables = cecil.Module.GetType("Example.Owner").Methods
                .Select(method => cecil.Mapper.MapType(method.GenericParameters[0])).ToArray();
            var reflectionVariables = runtime.Select(method => reflection.Mapper.MapType(method.GetGenericArguments()[0])).ToArray();
            var roslynVariables = compilation.GetTypeByMetadataName("Example.Owner")!.GetMembers("M")
                .OfType<IMethodSymbol>().Select(method => roslyn.Mapper.MapType(method.TypeParameters[0])).ToArray();
            foreach (var methods in new[] { reflectionMethods, roslynMethods })
                Assert.Equal(cecilMethods.Select(method => method.GenericParameters[0].OwnerId),
                    methods.Select(method => method.GenericParameters[0].OwnerId));
            foreach (var (system, methods, variables) in new (AspectN.TypeSystem.ITypeSystem, MethodSignature[], TypeSignature[])[]
                     { (cecil, cecilMethods, cecilVariables), (reflection, reflectionMethods, reflectionVariables), (roslyn, roslynMethods, roslynVariables) })
            {
                Assert.Equal(2, methods.Length);
                Assert.NotEqual(methods[0].GenericParameters[0].OwnerId, methods[1].GenericParameters[0].OwnerId);
                Assert.Equal(MatchStatus.NotMatched, system.AreEqual(
                    new GenericParameterTypeSignature(methods[0].GenericParameters[0]),
                    new GenericParameterTypeSignature(methods[1].GenericParameters[0])).Status);
                var target = reflection.Mapper.MapType(typeof(IDisposable));
                Assert.Equal(MatchStatus.Matched, system.IsAssignableTo(variables[0], target).Status);
                Assert.Equal(MatchStatus.NotMatched, system.IsAssignableTo(variables[1], target).Status);
            }
        }
        finally { context.Unload(); }
    }
}
