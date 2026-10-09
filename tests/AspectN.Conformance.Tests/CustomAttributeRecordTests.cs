using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Runtime.Loader;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class CustomAttributeRecordTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MapParameter_RealOptionalRecordWithOrWithoutFlag_PreservesRecord(bool optionalFlag)
    {
        using var module = ModuleDefinition.CreateModule($"OptionalRecord{Guid.NewGuid():N}", ModuleKind.Dll);
        var owner = new TypeDefinition("Example", "C", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(owner);
        var method = new MethodDefinition("M", MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
        owner.Methods.Add(method);
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        var parameter = new ParameterDefinition("value", optionalFlag ? ParameterAttributes.Optional : ParameterAttributes.None, module.TypeSystem.Int32);
        parameter.CustomAttributes.Add(new CustomAttribute(module.ImportReference(typeof(System.Runtime.InteropServices.OptionalAttribute).GetConstructor(Type.EmptyTypes)!)));
        method.Parameters.Add(parameter);
        using var image = new MemoryStream();
        module.Write(image);
        var context = new AssemblyLoadContext("OptionalRecord", isCollectible: true);
        try
        {
            var runtime = context.LoadFromStream(new MemoryStream(image.ToArray())).GetType("Example.C")!.GetMethod("M")!;
            var mapped = new AspectN.Reflection.ReflectionTypeSystem().Mapper.MapMethod(runtime);

            Assert.Equal(optionalFlag, runtime.GetParameters()[0].IsOptional);
            Assert.Equal("System.Runtime.InteropServices.OptionalAttribute", Assert.IsType<NamedTypeSignature>(
                Assert.Single(mapped.Parameters[0].Attributes).AttributeType).Identity.FullMetadataName);
        }
        finally
        {
            context.Unload();
        }
    }

    [Fact]
    public void MapParameter_FrameworkReferencesWithoutReadonlyMarker_PreservesEmbeddedRecordIdentity()
    {
        var reference = MetadataReference.CreateFromFile(Path.Combine(AppContext.BaseDirectory, "framework-references", "mscorlib.dll"));
        var compilation = CSharpCompilation.Create("EmbeddedReadonly", [CSharpSyntaxTree.ParseText(
            "public class C { public void M(in int x, ref readonly int y) {} }")], [reference],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var image = FixtureCompiler.Emit(compilation);
        using var module = ModuleDefinition.ReadModule(new MemoryStream(image));
        using var cecil = new AspectN.Cecil.CecilTypeSystem(module, new AspectN.Cecil.CecilMappingOptions(leaveOpen: true));
        var roslyn = new AspectN.Roslyn.RoslynTypeSystem(compilation);
        var source = roslyn.Mapper.MapMethod(compilation.GetTypeByMetadataName("C")!.GetMembers("M").OfType<IMethodSymbol>().Single());
        var metadata = cecil.Mapper.MapMethod(module.GetType("C").Methods.Single(method => method.Name == "M"));

        for (var index = 0; index < 2; index++)
        {
            var actual = Assert.Single(source.Parameters[index].Attributes).AttributeType;
            var expected = Assert.Single(metadata.Parameters[index].Attributes).AttributeType;
            Assert.Equal(Assert.IsType<NamedTypeSignature>(expected).Identity.FullMetadataName,
                Assert.IsType<NamedTypeSignature>(actual).Identity.FullMetadataName);
            if (index == 1) Assert.Equal(MatchStatus.Matched, StructuralTypeComparer.AreEqual(expected, actual).Status);
        }
    }

    private const string SOURCE = """
        using System;
        using System.Runtime.CompilerServices;
        using System.Runtime.InteropServices;
        namespace Example;
        public sealed class MarkerAttribute : Attribute { }
        [Serializable, StructLayout(LayoutKind.Sequential), Marker]
        public class C
        {
            [Marker]
            public void M([Marker] out int output, in int input, ref readonly int location, [Optional, DefaultParameterValue(3), Marker] int optional) { output = input; }
            [DllImport("unused"), Marker]
            [return: MarshalAs(UnmanagedType.I4), Marker]
            public static extern int Native([In, Out, MarshalAs(UnmanagedType.I4), Marker] ref int value);
            [MethodImpl(MethodImplOptions.NoInlining), PreserveSig, Marker]
            public int Flags() => 0;
            private int _value;
            [Marker]
            public ref readonly int Readonly() => ref _value;
            public struct S { public int Value; }
            public unsafe S* Pointer(S* value) => value;
        }
        """;

    [Theory]
    [InlineData("M")]
    [InlineData("Native")]
    [InlineData("Flags")]
    [InlineData("Readonly")]
    public void MapMethod_MetadataEncodedAttributes_OnlyExposesCustomAttributeRecords(string name)
    {
        using var fixture = new CompiledAdapterFixture(SOURCE);
        var cecil = fixture.Cecil.Mapper.MapMethod(fixture.Cecil.Module.GetType("Example.C").Methods.Single(method => method.Name == name));
        var reflection = fixture.Reflection.Mapper.MapMethod(fixture.Assembly.GetType("Example.C")!.GetMethod(name)!);
        var source = fixture.Roslyn.Mapper.MapMethod(fixture.Roslyn.Compilation.GetTypeByMetadataName("Example.C")!
            .GetMembers(name).OfType<IMethodSymbol>().Single());
        var metadata = fixture.RoslynMetadata.Mapper.MapMethod(fixture.RoslynMetadata.Compilation.GetTypeByMetadataName("Example.C")!
            .GetMembers(name).OfType<IMethodSymbol>().Single());
        Assert.Equal(Attributes(cecil.Attributes), Attributes(reflection.Attributes));
        Assert.Equal(Attributes(cecil.Attributes), Attributes(source.Attributes));
        Assert.Equal(Attributes(cecil.Attributes), Attributes(metadata.Attributes));
        Assert.Equal(Attributes(cecil.ReturnAttributes), Attributes(reflection.ReturnAttributes));
        Assert.Equal(Attributes(cecil.ReturnAttributes), Attributes(source.ReturnAttributes));
        Assert.Equal(Attributes(cecil.TypeAttributes), Attributes(reflection.TypeAttributes));
        Assert.Equal(Attributes(cecil.TypeAttributes), Attributes(source.TypeAttributes));
        for (var index = 0; index < cecil.Parameters.Count; index++)
        {
            Assert.Equal(Attributes(cecil.Parameters[index].Attributes), Attributes(reflection.Parameters[index].Attributes));
            Assert.Equal(Attributes(cecil.Parameters[index].Attributes), Attributes(source.Parameters[index].Attributes));
            Assert.Equal(Attributes(cecil.Parameters[index].Attributes), Attributes(metadata.Parameters[index].Attributes));
        }
        Assert.Contains("Example.MarkerAttribute", Attributes(cecil.Attributes));
        foreach (var (signature, system) in new (MethodSignature, ITypeSystem)[]
        { (cecil, fixture.Cecil), (reflection, fixture.Reflection), (source, fixture.Roslyn), (metadata, fixture.RoslynMetadata) })
        {
            Assert.True(new AspectNEngine().CompileMemberExpression("attr(type System.SerializableAttribute)").TryGetMemberPattern(out var pattern));
            Assert.Equal(MatchStatus.NotMatched, pattern.Match(signature, system).Status);
            var marker = Assert.Single(signature.TypeAttributes);
            Assert.Equal(MatchStatus.Matched, system.HasDirectAttribute(AttributeTarget.ForType(signature.DeclaringType), marker.AttributeType).Status);
        }
    }

    [Fact]
    public void MapMethod_NestedPointer_UsesCanonicalDisplayNameAndRegex()
    {
        using var fixture = new CompiledAdapterFixture(SOURCE);
        var cecil = fixture.Cecil.Mapper.MapMethod(fixture.Cecil.Module.GetType("Example.C").Methods.Single(method => method.Name == "Pointer"));
        var reflection = fixture.Reflection.Mapper.MapMethod(fixture.Assembly.GetType("Example.C")!.GetMethod("Pointer")!);
        var roslyn = fixture.Roslyn.Mapper.MapMethod(fixture.Roslyn.Compilation.GetTypeByMetadataName("Example.C")!
            .GetMembers("Pointer").OfType<IMethodSymbol>().Single());
        Assert.Equal("Example.C/S*", Assert.IsType<UnsupportedTypeSignature>(reflection.ReturnType.Type).DisplayName);
        Assert.True(new AspectNEngine().CompileMemberExpression(@"regex(Example\.C/S\*)").TryGetMemberPattern(out var pattern));
        Assert.Equal(MatchStatus.Matched, pattern.Match(cecil, fixture.Cecil).Status);
        Assert.Equal(MatchStatus.Matched, pattern.Match(reflection, fixture.Reflection).Status);
        Assert.Equal(MatchStatus.Matched, pattern.Match(roslyn, fixture.Roslyn).Status);
    }

    private static string[] Attributes(IReadOnlyList<AttributeSignature> attributes) => attributes
        .Select(attribute => Assert.IsType<NamedTypeSignature>(attribute.AttributeType).Identity.FullMetadataName)
        .Order(StringComparer.Ordinal).ToArray();
}
