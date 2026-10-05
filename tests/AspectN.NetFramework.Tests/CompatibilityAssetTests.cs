using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Mono.Cecil;
using Xunit;

namespace AspectN.NetFramework.Tests;

public sealed class CompatibilityAssetTests
{
    [Fact]
    public void LoadedAssets_AllAdaptersActuallyUseNetstandard20()
    {
        foreach (var type in new[] { typeof(AspectNEngine), typeof(Cecil.CecilTypeSystem),
                     typeof(Roslyn.RoslynTypeSystem), typeof(Reflection.ReflectionTypeSystem) })
            Assert.Equal(".NETStandard,Version=v2.0", type.Assembly
                .GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute), false)
                .Cast<System.Runtime.Versioning.TargetFrameworkAttribute>().Single().FrameworkName);
    }

    [Fact]
    public void Cecil_ArrayShapeAndNominalRelation_ExecuteOnCompatibilityAsset()
    {
        using var module = ModuleDefinition.CreateModule("Compatibility", ModuleKind.Dll);
        using var system = new Cecil.CecilTypeSystem(module);
        var candidate = Assert.IsType<ArrayTypeSignature>(system.Mapper.MapType(new ArrayType(module.TypeSystem.String)));
        Assert.Equal(ArrayShape.Vector, candidate.Shape);
        Assert.Equal(MatchStatus.Matched, system.IsAssignableTo(candidate, system.Mapper.MapType(module.TypeSystem.Object)).Status);
        var general = new ArrayType(module.TypeSystem.String);
        general.Dimensions[0] = new ArrayDimension(0, null);
        Assert.Equal(ArrayShape.General, Assert.IsType<ArrayTypeSignature>(system.Mapper.MapType(general)).Shape);
    }

    [Fact]
    public void Roslyn_ArrayAndGenericConstraint_ExecuteOnCompatibilityAsset()
    {
        var compilation = CSharpCompilation.Create("Compatibility",
            new[] { CSharpSyntaxTree.ParseText("public class C { public string[] M<T>(T x) where T:System.IDisposable => null; }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.DoesNotContain(compilation.GetDiagnostics(), diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        var system = new Roslyn.RoslynTypeSystem(compilation);
        var method = compilation.GetTypeByMetadataName("C")!.GetMembers("M").OfType<IMethodSymbol>().Single();
        Assert.Equal(ArrayShape.Vector, Assert.IsType<ArrayTypeSignature>(system.Mapper.MapType(method.ReturnType)).Shape);
        Assert.Equal(MatchStatus.Matched, system.IsAssignableTo(system.Mapper.MapType(method.TypeParameters[0]),
            system.Mapper.MapType(compilation.GetTypeByMetadataName("System.IDisposable")!)).Status);
    }

    [Fact]
    public void Reflection_RankOneGeneralArray_IsDistinctFromVector()
    {
        var system = new Reflection.ReflectionTypeSystem();
        var vector = Assert.IsType<ArrayTypeSignature>(system.Mapper.MapType(typeof(int[])));
        var general = Assert.IsType<ArrayTypeSignature>(system.Mapper.MapType(typeof(int).MakeArrayType(1)));
        Assert.Equal(ArrayShape.Vector, vector.Shape);
        Assert.Equal(ArrayShape.General, general.Shape);
        Assert.Equal(MatchStatus.NotMatched, StructuralTypeComparer.AreEqual(vector, general).Status);
    }

#if NET10_0_OR_GREATER
    [Fact]
    public void Reflection_FunctionPointerBridge_ReadsModifiedCallingConventionsOnNetstandardAsset()
    {
        var system = new Reflection.ReflectionTypeSystem();
        var managed = system.Mapper.MapMethod(typeof(FunctionPointerFixture).GetMethod("Managed")!).GenericParameters.Single();
        var cdecl = system.Mapper.MapMethod(typeof(FunctionPointerFixture).GetMethod("Cdecl")!).GenericParameters.Single();
        var suppressed = system.Mapper.MapMethod(typeof(FunctionPointerFixture).GetMethod("Suppressed")!).GenericParameters.Single();
        Assert.Contains("delegate*[managed]", managed.OwnerId);
        Assert.Contains("delegate*[cdecl]", cdecl.OwnerId);
        Assert.Contains("delegate*[unmanaged:cdecl,suppressgctransition]", suppressed.OwnerId);
        Assert.DoesNotContain("unknown", cdecl.OwnerId);
    }

    private static class FunctionPointerFixture
    {
        public static unsafe void Managed<T>(delegate*<int, void> value) { }
        public static unsafe void Cdecl<T>(delegate* unmanaged[Cdecl]<int, void> value) { }
        public static unsafe void Suppressed<T>(delegate* unmanaged[Cdecl, SuppressGCTransition]<int, void> value) { }
    }
#endif
}
