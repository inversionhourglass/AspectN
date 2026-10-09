using System.Collections.Immutable;
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

public sealed class MethodGenericOwnerIdentityTests
{
    [Theory]
    [InlineData("A::Shared.Foo", "B::Shared.Foo")]
    [InlineData("System.Collections.Generic.List<A::Shared.Foo[]>", "System.Collections.Generic.List<B::Shared.Foo[]>")]
    public void MapMethod_AssemblyDistinctParameterTypes_DistinguishesOwnersAndConstraints(string firstType, string secondType)
    {
        var platform = FixtureCompiler.TrustedPlatformReferences().ToArray();
        var firstImage = FixtureCompiler.Emit(Compile("OwnerIdentity.A", "namespace Shared { public class Foo {} }", platform));
        var secondImage = FixtureCompiler.Emit(Compile("OwnerIdentity.B", "namespace Shared { public class Foo {} }", platform));
        var references = platform.Concat(new[]
        {
            MetadataReference.CreateFromImage(firstImage, new MetadataReferenceProperties(aliases: ImmutableArray.Create("A"))),
            MetadataReference.CreateFromImage(secondImage, new MetadataReferenceProperties(aliases: ImmutableArray.Create("B")))
        });
        var source = $$"""
            extern alias A;
            extern alias B;
            namespace Review;
            public class Owner
            {
                public T M<T>({{firstType}} x, T value) where T : System.IDisposable => value;
                public T M<T>({{secondType}} x, T value) where T : System.IComparable => value;
            }
            """;
        var compilation = Compile("OwnerIdentity.Main", source, references);
        var image = FixtureCompiler.Emit(compilation);
        var loadContext = new AssemblyLoadContext("OwnerIdentity", isCollectible: true);
        using var resolver = CreateResolver();
        try
        {
            loadContext.LoadFromStream(new MemoryStream(firstImage));
            loadContext.LoadFromStream(new MemoryStream(secondImage));
            var assembly = loadContext.LoadFromStream(new MemoryStream(image));
            using var cecil = new CecilTypeSystem(ReadModule(image, resolver));
            var reflection = new ReflectionTypeSystem();
            var roslyn = new RoslynTypeSystem(compilation);
            var cecilMethods = cecil.Module.GetType("Review.Owner").Methods.Where(method => method.Name == "M")
                .Select(cecil.Mapper.MapMethod).ToArray();
            var importedMethods = assembly.GetType("Review.Owner")!.GetMethods().Where(method => method.Name == "M")
                .Select(method => cecil.Mapper.MapMethod(cecil.Module.ImportReference(method))).ToArray();
            var reflectionMethods = assembly.GetType("Review.Owner")!.GetMethods().Where(method => method.Name == "M")
                .Select(reflection.Mapper.MapMethod).ToArray();
            var roslynMethods = compilation.GetTypeByMetadataName("Review.Owner")!.GetMembers("M").OfType<IMethodSymbol>()
                .Select(roslyn.Mapper.MapMethod).ToArray();
            foreach (var (methods, system) in new (MethodSignature[], ITypeSystem)[]
                     { (cecilMethods, cecil), (reflectionMethods, reflection), (roslynMethods, roslyn), (importedMethods, cecil) })
            {
                Assert.Equal(MatchStatus.NotMatched, system.AreEqual(methods[0].ReturnType.Type, methods[1].ReturnType.Type).Status);
                var pattern = CompilePattern("method(System.IDisposable+ Review.Owner.M<..>(..))");
                Assert.Equal(MatchStatus.Matched, pattern.Match(methods[0], system).Status);
                Assert.Equal(MatchStatus.NotMatched, pattern.Match(methods[1], system).Status);
            }

            for (var index = 0; index < 2; index++)
            {
                Assert.Equal(roslynMethods[index].GenericParameters[0].OwnerId, cecilMethods[index].GenericParameters[0].OwnerId);
                Assert.Equal(roslynMethods[index].GenericParameters[0].OwnerId, reflectionMethods[index].GenericParameters[0].OwnerId);
                Assert.Equal(roslynMethods[index].GenericParameters[0].OwnerId, importedMethods[index].GenericParameters[0].OwnerId);
            }
        }
        finally
        {
            loadContext.Unload();
        }
    }

    [Fact]
    public void MapMethod_ClosedDeclaringType_PreservesDeclarationIdentityAndConstraintContext()
    {
        var compilation = Compile("OwnerIdentity.Closed", """
            namespace Review;
            public class Owner<T>
            {
                public V M<V>(T x, V value) where V : T => value;
            }
            """, FixtureCompiler.TrustedPlatformReferences());
        var image = FixtureCompiler.Emit(compilation);
        var loadContext = new AssemblyLoadContext("OwnerIdentity.Closed", isCollectible: true);
        using var resolver = CreateResolver();
        try
        {
            var assembly = loadContext.LoadFromStream(new MemoryStream(image));
            using var cecil = new CecilTypeSystem(ReadModule(image, resolver));
            var reflection = new ReflectionTypeSystem();
            var roslyn = new RoslynTypeSystem(compilation);
            var reflectionOwner = assembly.GetType("Review.Owner`1")!;
            var roslynOwner = compilation.GetTypeByMetadataName("Review.Owner`1")!;
            var open = cecil.Mapper.MapMethod(cecil.Module.GetType("Review.Owner`1").Methods.Single(method => method.Name == "M"));
            var previous = open.ReturnType.Type;
            foreach (var (runtimeType, specialType) in new[] { (typeof(string), SpecialType.System_String), (typeof(int), SpecialType.System_Int32) })
            {
                var runtimeMethod = reflectionOwner.MakeGenericType(runtimeType).GetMethod("M")!;
                var cecilMethod = cecil.Mapper.MapMethod(cecil.Module.ImportReference(runtimeMethod));
                var reflectionMethod = reflection.Mapper.MapMethod(runtimeMethod);
                var roslynMethod = roslyn.Mapper.MapMethod(roslynOwner.Construct(compilation.GetSpecialType(specialType))
                    .GetMembers("M").OfType<IMethodSymbol>().Single());
                Assert.Equal(MatchStatus.Matched, cecil.AreEqual(previous, cecilMethod.ReturnType.Type).Status);
                Assert.Equal(reflectionMethod.GenericParameters[0].OwnerId, cecilMethod.GenericParameters[0].OwnerId);
                Assert.Equal(roslynMethod.GenericParameters[0].OwnerId, cecilMethod.GenericParameters[0].OwnerId);
                var target = cecil.Mapper.MapType(cecil.Module.ImportReference(runtimeType));
                Assert.Equal(MatchStatus.Matched, cecil.IsAssignableTo(cecilMethod.ReturnType.Type, target).Status);
                var other = cecil.Mapper.MapType(cecil.Module.ImportReference(runtimeType == typeof(string) ? typeof(int) : typeof(string)));
                Assert.Equal(MatchStatus.NotMatched, cecil.IsAssignableTo(cecilMethod.ReturnType.Type, other).Status);
                Assert.Equal(MatchStatus.Matched, reflection.IsAssignableTo(reflectionMethod.ReturnType.Type, target).Status);
                Assert.Equal(MatchStatus.NotMatched, reflection.IsAssignableTo(reflectionMethod.ReturnType.Type, other).Status);
                Assert.Equal(MatchStatus.Matched, reflection.AreEqual(previous, reflectionMethod.ReturnType.Type).Status);
                var pattern = CompilePattern($"method({(runtimeType == typeof(string) ? "string" : "int")}+ M<>(..))");
                Assert.Equal(MatchStatus.Matched, pattern.Match(reflectionMethod, reflection).Status);
                previous = cecilMethod.ReturnType.Type;
            }
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private static CSharpCompilation Compile(string name, string source, IEnumerable<MetadataReference> references) =>
        CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, deterministic: true));

    [Fact]
    public void MapMethod_ClosedOverloadsWithCoincidentParameterTypes_PreservesDeclarationSlots()
    {
        var compilation = Compile("OwnerIdentity.ClosedOverloads", """
            namespace Review;
            public class Owner<T>
            {
                public V M<V>(T x, V value) where V : System.IDisposable => value;
                public V M<V>(int x, V value) where V : System.IComparable => value;
            }
            """, FixtureCompiler.TrustedPlatformReferences());
        var image = FixtureCompiler.Emit(compilation);
        var loadContext = new AssemblyLoadContext("OwnerIdentity.ClosedOverloads", isCollectible: true);
        using var resolver = CreateResolver();
        try
        {
            var assembly = loadContext.LoadFromStream(new MemoryStream(image));
            using var cecil = new CecilTypeSystem(ReadModule(image, resolver));
            var reflection = new ReflectionTypeSystem();
            var methods = assembly.GetType("Review.Owner`1")!.MakeGenericType(typeof(int)).GetMethods()
                .Where(method => method.Name == "M").ToArray();
            Assert.Equal(2, methods.Length);
            var pattern = CompilePattern("method(System.IDisposable+ M<..>(..))");
            var signatures = methods.Select(method => cecil.Mapper.MapMethod(cecil.Module.ImportReference(method))).ToArray();
            Assert.NotEqual(signatures[0].GenericParameters[0].OwnerId, signatures[1].GenericParameters[0].OwnerId);
            for (var index = 0; index < methods.Length; index++)
            {
                var expected = reflection.Mapper.MapMethod(methods[index]);
                Assert.True(signatures[index].Availability.IsKnown(MetadataFacts.Kind));
                Assert.Equal(expected.GenericParameters[0].OwnerId, signatures[index].GenericParameters[0].OwnerId);
                Assert.Equal(pattern.Match(expected, reflection).Status, pattern.Match(signatures[index], cecil).Status);
            }
        }
        finally
        {
            loadContext.Unload();
        }
    }

    [Fact]
    public void MapMethod_InheritedMemberReference_ResolvesExecutableClrReference()
    {
        var compilation = Compile("OwnerIdentity.Inherited", """
            namespace Review;
            public class Base { public int Run() => 42; }
            public class Derived : Base { }
            public static class Runner { public static int Invoke(Derived value) => value.Run(); }
            """, FixtureCompiler.TrustedPlatformReferences());
        var image = FixtureCompiler.Emit(compilation);
        var loadContext = new AssemblyLoadContext("OwnerIdentity.Inherited", isCollectible: true);
        using var resolver = CreateResolver();
        try
        {
            using var cecil = new CecilTypeSystem(ReadModule(image, resolver));
            var reference = new MethodReference("Run", cecil.Module.TypeSystem.Int32, cecil.Module.GetType("Review.Derived")) { HasThis = true };
            var runner = cecil.Module.GetType("Review.Runner").Methods.Single(method => method.Name == "Invoke");
            runner.Body.Instructions.Single(instruction => instruction.Operand is MethodReference { Name: "Run" }).Operand = reference;
            using var rewritten = new MemoryStream();
            cecil.Module.Write(rewritten);
            rewritten.Position = 0;
            var assembly = loadContext.LoadFromStream(rewritten);
            var instance = Activator.CreateInstance(assembly.GetType("Review.Derived")!);
            Assert.Equal(42, assembly.GetType("Review.Runner")!.GetMethod("Invoke")!.Invoke(null, [instance]));

            var signature = cecil.Mapper.MapMethod(reference);
            Assert.True(signature.Availability.IsKnown(MetadataFacts.Kind));
            Assert.Equal(MatchStatus.Matched, CompilePattern("method(int Run())").Match(signature, cecil).Status);
        }
        finally
        {
            loadContext.Unload();
        }
    }

    [Fact]
    public void MapMethod_VarArgCallSite_ResolvesUsingFixedParameterSignature()
    {
        var compilation = Compile("OwnerIdentity.VarArg", """
            namespace Review;
            public static class Owner
            {
                public static void M(int value, __arglist) { }
                public static void Call() => M(1, __arglist("optional"));
            }
            """, FixtureCompiler.TrustedPlatformReferences());
        using var resolver = CreateResolver();
        using var cecil = new CecilTypeSystem(ReadModule(FixtureCompiler.Emit(compilation), resolver));
        var caller = cecil.Module.GetType("Review.Owner").Methods.Single(method => method.Name == "Call");
        var reference = Assert.IsType<MethodReference>(caller.Body.Instructions.Single(
            instruction => instruction.Operand is MethodReference { Name: "M" }).Operand);
        Assert.IsType<SentinelType>(reference.Parameters[1].ParameterType);

        var signature = cecil.Mapper.MapMethod(reference);

        Assert.True(signature.Availability.IsKnown(MetadataFacts.Kind));
        Assert.Equal(MatchStatus.Matched, CompilePattern("method(void M(..))").Match(signature, cecil).Status);
    }

    [Fact]
    public void MapMethod_FunctionPointerOwnerEncoding_PreservesDisplayAndRegexMatching()
    {
        var compilation = Compile("OwnerIdentity.Pointer", """
            namespace Review;
            public unsafe class Owner
            {
                public T M<T>(delegate*<int, void> action, T value) => value;
            }
            """, FixtureCompiler.TrustedPlatformReferences());
        var image = FixtureCompiler.Emit(compilation);
        var loadContext = new AssemblyLoadContext("OwnerIdentity.Pointer", isCollectible: true);
        using var resolver = CreateResolver();
        try
        {
            var assembly = loadContext.LoadFromStream(new MemoryStream(image));
            using var cecil = new CecilTypeSystem(ReadModule(image, resolver));
            var reflection = new ReflectionTypeSystem();
            var roslyn = new RoslynTypeSystem(compilation);
            var methods = new (MethodSignature Method, ITypeSystem System)[]
            {
                (cecil.Mapper.MapMethod(cecil.Module.GetType("Review.Owner").Methods.Single(method => method.Name == "M")), cecil),
                (reflection.Mapper.MapMethod(assembly.GetType("Review.Owner")!.GetMethod("M")!), reflection),
                (roslyn.Mapper.MapMethod(compilation.GetTypeByMetadataName("Review.Owner")!.GetMembers("M").OfType<IMethodSymbol>().Single()), roslyn)
            };
            var pattern = CompilePattern("""regex(^public T Review\.Owner\.M<T>\(delegate\*\[managed\]<System\.Int32,System\.Void>, T\)$)""");
            foreach (var (method, system) in methods)
            {
                Assert.Equal("delegate*[managed]<System.Int32,System.Void>", Assert.IsType<UnsupportedTypeSignature>(method.Parameters[0].Type.Type).DisplayName);
                Assert.Equal(MatchStatus.Matched, pattern.Match(method, system).Status);
            }
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private static FixtureResolver CreateResolver()
    {
        var resolver = new FixtureResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
        return resolver;
    }

    private static ModuleDefinition ReadModule(byte[] image, FixtureResolver resolver)
    {
        var module = ModuleDefinition.ReadModule(new MemoryStream(image), new ReaderParameters { AssemblyResolver = resolver, InMemory = true });
        resolver.Register(module.Assembly);
        return module;
    }

    private sealed class FixtureResolver : DefaultAssemblyResolver
    {
        public void Register(AssemblyDefinition assembly) => RegisterAssembly(assembly);
    }

    private static CompiledMemberPattern CompilePattern(string source)
    {
        var compilation = new AspectNEngine().CompileMemberExpression(source);
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        return pattern!;
    }
}
