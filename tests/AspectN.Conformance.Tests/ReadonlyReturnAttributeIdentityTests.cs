using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Mono.Cecil;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class ReadonlyReturnAttributeIdentityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MapReturn_ShadowReader_PreservesActualMarkerAssembly(bool embedded)
    {
        using var fixture = new CompiledAdapterFixture(embedded ? MARKER + SOURCE : SOURCE);
        var native = fixture.Assembly.GetType("Example.C")!.GetMethod("M")!.ReturnParameter.CustomAttributes
            .Single(attribute => attribute.AttributeType.FullName == MARKER_NAME);
        var cecil = fixture.Cecil.Module.GetType("Example.C").Methods.Single(method => method.Name == "M");
        var nativeRecord = cecil.MethodReturnType.CustomAttributes.Single(attribute => attribute.AttributeType.FullName == MARKER_NAME);
        var actualAssembly = native.AttributeType.Assembly.GetName().Name;
        Assert.Equal(actualAssembly, embedded ? fixture.Cecil.Module.Assembly.Name.Name :
            ((AssemblyNameReference)nativeRecord.Constructor.DeclaringType.Scope).Name);
        using var image = new MemoryStream();
        fixture.Cecil.Module.Write(image);
        var reader = Reader(image.ToArray(), []);
        var system = new RoslynTypeSystem(reader);
        var method = reader.GetTypeByMetadataName("Example.C")!.GetMembers("M").OfType<IMethodSymbol>().Single();

        var signature = system.Mapper.MapMethod(method);
        var marker = Assert.Single(signature.ReturnAttributes, attribute =>
            Assert.IsType<NamedTypeSignature>(attribute.AttributeType).Identity.FullMetadataName == MARKER_NAME);
        var identity = Assert.IsType<NamedTypeSignature>(marker.AttributeType).Identity;

        Assert.True(method.ReturnsByRefReadonly);
        Assert.Equal(actualAssembly, identity.Assembly.Name);
        Assert.Equal(MatchStatus.Matched, system.AreEqual(marker.AttributeType,
            fixture.Reflection.Mapper.MapType(native.AttributeType)).Status);
        Assert.True(new AspectNEngine().CompileMemberExpression("attr(ret " + MARKER_NAME + ")")
            .TryGetMemberPattern(out var pattern));
        Assert.Equal(MatchStatus.Matched, pattern.Match(signature, system).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MapReturn_MultipleSameNamedDependencies_UsesConstructorScope(bool available)
    {
        using var fixture = new CompiledAdapterFixture(SOURCE);
        var selected = EmitMarker("SelectedMarker");
        var other = EmitMarker("OtherMarker");
        var method = fixture.Cecil.Module.GetType("Example.C").Methods.Single(value => value.Name == "M");
        var scope = new AssemblyNameReference("SelectedMarker", new Version(0, 0, 0, 0));
        fixture.Cecil.Module.AssemblyReferences.Add(scope);
        var marker = new TypeReference("System.Runtime.CompilerServices", "IsReadOnlyAttribute", fixture.Cecil.Module, scope);
        method.MethodReturnType.CustomAttributes.Clear();
        method.MethodReturnType.CustomAttributes.Add(new CustomAttribute(
            new MethodReference(".ctor", fixture.Cecil.Module.TypeSystem.Void, marker) { HasThis = true }));
        Assert.Equal("SelectedMarker", ((AssemblyNameReference)method.MethodReturnType.CustomAttributes.Single()
            .Constructor.DeclaringType.Scope).Name);
        using var image = new MemoryStream();
        fixture.Cecil.Module.Write(image);
        var references = new List<MetadataReference> { MetadataReference.CreateFromImage(other) };
        if (available) references.Add(MetadataReference.CreateFromImage(selected));
        var reader = Reader(image.ToArray(), references);
        var system = new RoslynTypeSystem(reader);
        var symbol = reader.GetTypeByMetadataName("Example.C")!.GetMembers("M").OfType<IMethodSymbol>().Single();

        var signature = system.Mapper.MapMethod(symbol);
        Assert.True(symbol.ReturnsByRefReadonly);
        Assert.Equal(AspectN.Metadata.RefKind.RefReadOnly, signature.ReturnType.RefKind);
        Assert.True(new AspectNEngine().CompileMemberExpression("method(int M())").TryGetMemberPattern(out var ordinary));
        Assert.Equal(MatchStatus.Matched, ordinary.Match(signature, system).Status);
        if (available)
        {
            var identity = Assert.IsType<NamedTypeSignature>(Assert.Single(signature.ReturnAttributes).AttributeType).Identity;
            Assert.Equal("SelectedMarker", identity.Assembly.Name);
            Assert.True(signature.Availability.IsKnown(MetadataFacts.ReturnAttributes));
        }
        else
        {
            Assert.False(signature.Availability.IsKnown(MetadataFacts.ReturnAttributes));
            Assert.Contains(signature.Availability.Diagnostics, diagnostic => diagnostic.Code == "AN3001");
            Assert.True(new AspectNEngine().CompileMemberExpression("attr(ret " + MARKER_NAME + ")")
                .TryGetMemberPattern(out var attribute));
            var result = attribute.Match(signature, system);
            Assert.Equal(MatchStatus.Unknown, result.Status);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AN3001");
        }
    }

    private static byte[] EmitMarker(string assembly) => FixtureCompiler.Emit(CSharpCompilation.Create(assembly,
        [CSharpSyntaxTree.ParseText(MARKER)], FixtureCompiler.TrustedPlatformReferences(),
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));

    private static CSharpCompilation Reader(byte[] image, IEnumerable<MetadataReference> extra)
    {
        var reader = CSharpCompilation.Create("ShadowReader", [CSharpSyntaxTree.ParseText(MARKER)],
            FixtureCompiler.TrustedPlatformReferences().Concat(extra).Append(MetadataReference.CreateFromImage(image)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, metadataImportOptions: MetadataImportOptions.All));
        Assert.DoesNotContain(reader.GetDiagnostics(), diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        return reader;
    }

    private const string MARKER_NAME = "System.Runtime.CompilerServices.IsReadOnlyAttribute";
    private const string MARKER = """
        namespace System.Runtime.CompilerServices {
            public sealed class IsReadOnlyAttribute : System.Attribute {}
        }
        """;
    private const string SOURCE = """
        namespace Example {
            public class C {
                private int _value;
                public ref readonly int M() => ref _value;
            }
        }
        """;
}
