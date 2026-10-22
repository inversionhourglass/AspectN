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
using Mono.Cecil.Cil;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class ReturnShapeOwnerIdentityTests
{
    [Theory]
    [InlineData("cecil")]
    [InlineData("reflection")]
    [InlineData("roslyn-pe")]
    public void MapMethod_ReturnOnlyOverloads_DistinguishesGenericDeclarations(string backend)
    {
        using var module = ModuleDefinition.CreateModule("ReturnShapeIdentity", ModuleKind.Dll);
        var owner = new TypeDefinition("Example", "Owner", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        module.Types.Add(owner);
        foreach (var integer in new[] { true, false })
        {
            var method = new MethodDefinition("M", MethodAttributes.Public | MethodAttributes.Static,
                integer ? module.TypeSystem.Int32 : module.TypeSystem.String);
            owner.Methods.Add(method);
            var parameter = new GenericParameter("T", method);
            method.GenericParameters.Add(parameter);
            parameter.Constraints.Add(new GenericParameterConstraint(module.ImportReference(
                integer ? typeof(IDisposable) : typeof(IComparable))));
            method.Parameters.Add(new ParameterDefinition(parameter));
            method.Body.Instructions.Add(Instruction.Create(integer ? OpCodes.Ldc_I4_0 : OpCodes.Ldnull));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        }
        using var image = new MemoryStream();
        module.Write(image);
        var bytes = image.ToArray();
        using var cecil = new CecilTypeSystem(ModuleDefinition.ReadModule(new MemoryStream(bytes)));
        var reader = CSharpCompilation.Create("ReturnReader",
            references: FixtureCompiler.TrustedPlatformReferences().Append(MetadataReference.CreateFromImage(bytes)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var roslyn = new RoslynTypeSystem(reader);
        var reflection = new ReflectionTypeSystem();
        var context = new AssemblyLoadContext("ReturnShapeIdentity", isCollectible: true);
        try
        {
            var runtime = context.LoadFromStream(new MemoryStream(bytes)).GetType("Example.Owner")!.GetMethods()
                .Where(method => method.Name == "M").ToArray();
            Assert.Equal(2, runtime.Length);
            using var disposable = new MemoryStream();
            foreach (var method in runtime)
                if (method.ReturnType == typeof(int))
                    Assert.Equal(0, method.MakeGenericMethod(typeof(MemoryStream)).Invoke(null, [disposable]));
                else
                    Assert.Null(method.MakeGenericMethod(typeof(int)).Invoke(null, [0]));
            var (system, methods) = backend switch
            {
                "cecil" => ((ITypeSystem)cecil, cecil.Module.GetType("Example.Owner").Methods.Select(method => cecil.Mapper.MapMethod(method)).ToArray()),
                "reflection" => (reflection, runtime.Select(method => reflection.Mapper.MapMethod(method)).ToArray()),
                _ => (roslyn, reader.GetTypeByMetadataName("Example.Owner")!.GetMembers("M")
                    .OfType<IMethodSymbol>().Select(method => roslyn.Mapper.MapMethod(method)).ToArray())
            };
            var first = methods[0].GenericParameters[0];
            var second = methods[1].GenericParameters[0];

            Assert.NotEqual(first.OwnerId, second.OwnerId);
            Assert.Equal(MatchStatus.NotMatched, system.AreEqual(
                new GenericParameterTypeSignature(first), new GenericParameterTypeSignature(second)).Status);
        }
        finally { context.Unload(); }
    }

    [Theory]
    [InlineData("T")]
    [InlineData("System.Collections.Generic.List<T[]>")]
    public void MapMethod_ClosedOwnerReturn_PreservesDeclarationAndQueryContext(string returnType)
    {
        using var fixture = new CompiledAdapterFixture($$"""
            namespace Example;
            public class Owner<T> {
                public {{returnType}} M<V>(V value) where V : T => default!;
            }
            """);
        var nativeOwner = fixture.Assembly.GetType("Example.Owner`1")!;
        using var resolver = new FixtureResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
        using var image = new MemoryStream();
        fixture.Cecil.Module.Write(image);
        var module = ModuleDefinition.ReadModule(new MemoryStream(image.ToArray()),
            new ReaderParameters { AssemblyResolver = resolver });
        resolver.Register(module.Assembly);
        using var cecil = new CecilTypeSystem(module);
        var open = cecil.Mapper.MapMethod(cecil.Module.GetType("Example.Owner`1").Methods
            .Single(method => method.Name == "M"));
        var openVariable = new GenericParameterTypeSignature(open.GenericParameters[0]);
        foreach (var (native, special) in new[] { (typeof(string), SpecialType.System_String), (typeof(int), SpecialType.System_Int32) })
        {
            var runtime = nativeOwner.MakeGenericType(native).GetMethod("M")!;
            foreach (var (system, method) in new (ITypeSystem, MethodSignature)[]
                     {
                         (cecil, cecil.Mapper.MapMethod(cecil.Module.ImportReference(runtime))),
                         (fixture.Reflection, fixture.Reflection.Mapper.MapMethod(runtime)),
                         (fixture.Roslyn, Closed(fixture.Roslyn, special)),
                         (fixture.RoslynMetadata, Closed(fixture.RoslynMetadata, special))
                     })
            {
                var variable = Assert.IsType<GenericParameterTypeSignature>(method.Parameters[0].Type.Type);
                var target = fixture.Reflection.Mapper.MapType(native);
                var other = fixture.Reflection.Mapper.MapType(native == typeof(string) ? typeof(int) : typeof(string));
                Assert.Equal(open.GenericParameters[0].OwnerId, method.GenericParameters[0].OwnerId);
                Assert.Equal(MatchStatus.Matched, system.AreEqual(openVariable, variable).Status);
                var relation = system.IsAssignableTo(variable, target);
                Assert.True(relation.Status == MatchStatus.Matched,
                    system.GetType().Name + ": " + relation.Status + " " + string.Join(",", relation.Diagnostics.Select(d => d.Code + " " + d.Message)));
                var negative = system.IsAssignableTo(variable, other);
                Assert.True(negative.Status == MatchStatus.NotMatched, system.GetType().Name + ": " + string.Join(",", negative.Diagnostics.Select(d => d.Message)));
                var expectedReturn = returnType == "T" ? native : typeof(List<>).MakeGenericType(native.MakeArrayType());
                Assert.Equal(MatchStatus.Matched, system.AreEqual(method.ReturnType.Type,
                    fixture.Reflection.Mapper.MapType(expectedReturn)).Status);
            }
        }
    }

    [Theory]
    [InlineData("R", "!!0&")]
    [InlineData("RR", "!!0&")]
    [InlineData("P", "delegate*[cdecl]")]
    public void MapMethod_GenericReturnShape_PreservesRefAndFunctionPointer(string name, string shape)
    {
        using var fixture = new CompiledAdapterFixture("""
            namespace Example;
            public static unsafe class Owner {
                public static ref V R<V>(ref V value) => ref value;
                public static ref readonly V RR<V>(ref V value) => ref value;
                public static delegate* unmanaged[Cdecl]<int, void> P<V>(V value) => null;
            }
            """);
        var native = fixture.Assembly.GetType("Example.Owner")!.GetMethod(name)!;
        var cecil = fixture.Cecil.Mapper.MapMethod(fixture.Cecil.Module.GetType("Example.Owner").Methods.Single(method => method.Name == name));
        var methods = new[]
        {
            cecil,
            fixture.Reflection.Mapper.MapMethod(native),
            fixture.Roslyn.Mapper.MapMethod(fixture.Roslyn.Compilation.GetTypeByMetadataName("Example.Owner")!
                .GetMembers(name).OfType<IMethodSymbol>().Single()),
            fixture.RoslynMetadata.Mapper.MapMethod(fixture.RoslynMetadata.Compilation.GetTypeByMetadataName("Example.Owner")!
                .GetMembers(name).OfType<IMethodSymbol>().Single())
        };
        foreach (var method in methods)
        {
            Assert.Equal(cecil.GenericParameters[0].OwnerId, method.GenericParameters[0].OwnerId);
            Assert.Contains(")->", method.GenericParameters[0].OwnerId);
            Assert.Contains(shape, method.GenericParameters[0].OwnerId);
        }
    }

    private static MethodSignature Closed(RoslynTypeSystem system, SpecialType type) => system.Mapper.MapMethod(
        system.Compilation.GetTypeByMetadataName("Example.Owner`1")!.Construct(system.Compilation.GetSpecialType(type))
            .GetMembers("M").OfType<IMethodSymbol>().Single());

    private sealed class FixtureResolver : DefaultAssemblyResolver
    {
        public void Register(AssemblyDefinition assembly) => RegisterAssembly(assembly);
    }
}
