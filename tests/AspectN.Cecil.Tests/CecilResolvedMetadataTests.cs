using System.Globalization;
using System.Reflection;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TypeAttributes = Mono.Cecil.TypeAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace AspectN.Cecil.Tests;

public sealed class CecilResolvedMetadataTests
{
    [Fact]
    public void MapType_UnresolvedReference_PreservesReferenceAssemblyIdentity()
    {
        using var module = ModuleDefinition.CreateModule("UnresolvedIdentity", ModuleKind.Dll);
        using var system = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var assembly = new AssemblyNameReference("Missing.Review.Dependency", new Version(1, 2, 3, 4));

        var signature = Assert.IsType<NamedTypeSignature>(system.Mapper.MapType(
            new TypeReference("Example", "Missing", module, assembly)));

        Assert.Equal(assembly.Name, signature.Identity.Assembly.Name);
        Assert.Equal(assembly.Version, signature.Identity.Assembly.Version);
    }

    [Fact]
    public void MapType_SdkReferenceAndResolvedDefinition_HaveEqualIdentity()
    {
        using var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
        using var module = ModuleDefinition.ReadModule(typeof(CecilResolvedMetadataTests).Assembly.Location,
            new ReaderParameters { AssemblyResolver = resolver });
        using var system = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var reference = module.TypeSystem.String;
        var definition = reference.Resolve();

        var mapped = Assert.IsType<NamedTypeSignature>(system.Mapper.MapType(reference));

        Assert.Equal(definition.Module.Assembly.Name.Name, mapped.Identity.Assembly.Name);
        Assert.Equal(MatchStatus.Matched, system.AreEqual(mapped, system.Mapper.MapType(definition)).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Query_DamagedLazyInterfaceTypeSpec_ReturnsResolutionFailure(bool closure)
    {
        using var original = ModuleDefinition.CreateModule("DamagedInterface", ModuleKind.Dll);
        var contract = new TypeDefinition("Example", "I`1", TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        contract.GenericParameters.Add(new GenericParameter("T", contract));
        original.Types.Add(contract);
        var candidate = new TypeDefinition("Example", "C", TypeAttributes.Public, original.TypeSystem.Object);
        var implementation = new GenericInstanceType(contract);
        implementation.GenericArguments.Add(original.TypeSystem.Int32);
        candidate.Interfaces.Add(new InterfaceImplementation(implementation));
        original.Types.Add(candidate);
        using var image = new MemoryStream();
        original.Write(image);
        using var loaded = ModuleDefinition.ReadModule(new MemoryStream(image.ToArray()),
            new ReaderParameters { ReadingMode = ReadingMode.Deferred });
        var type = loaded.Types.Single(item => item.Name == "C");
        var metadata = Field(loaded, "Image");
        var heap = Field(metadata, "TableHeap");
        var row = ((Array)Field(heap, "Tables")).GetValue(27)!;
        var offset = Convert.ToInt32(Field(row, "Offset"), CultureInfo.InvariantCulture);
        var data = (byte[])Field(heap, "data");
        var blobIndex = data[offset] | data[offset + 1] << 8;
        var blobs = (byte[])Field(Field(metadata, "BlobHeap"), "data");
        // ARRAY int32，秩为零：接口集合的延迟读取会抛出可恢复的元数据异常。
        blobs[blobIndex + 1] = 0x14;
        blobs[blobIndex + 2] = 0x08;
        blobs[blobIndex + 3] = 0;
        blobs[blobIndex + 4] = 0;
        blobs[blobIndex + 5] = 0;
        using var system = new CecilTypeSystem(loaded, new CecilMappingOptions(leaveOpen: true));
        var signature = system.Mapper.MapType(type);

        var result = closure ? system.GetInheritanceClosure(signature).Failure! :
            system.IsAssignableTo(signature, system.Mapper.MapType(loaded.TypeSystem.String));

        Assert.NotNull(result);
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.ResolutionFailure);
        if (closure) Assert.NotEmpty(system.GetInheritanceClosure(signature).Ancestors);
    }

    [Fact]
    public void MapMethod_ArrayOwner_DoesNotReadElementAttributes()
    {
        using var module = ModuleDefinition.CreateModule("ArrayAttributes", ModuleKind.Dll);
        var marker = new TypeDefinition("Example", "MarkerAttribute", TypeAttributes.Public, module.ImportReference(typeof(Attribute)));
        module.Types.Add(marker);
        var constructor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            module.TypeSystem.Void);
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        marker.Methods.Add(constructor);
        var element = new TypeDefinition("Example", "Element", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(element);
        element.CustomAttributes.Add(new CustomAttribute(constructor));
        var array = new ArrayType(element, 2);
        var method = new MethodReference("Get", element, array) { HasThis = true };
        method.Parameters.Add(new ParameterDefinition(module.TypeSystem.Int32));
        method.Parameters.Add(new ParameterDefinition(module.TypeSystem.Int32));
        using var system = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));

        var mapped = system.Mapper.MapMethod(method);

        Assert.Empty(mapped.TypeAttributes);
        Assert.True(mapped.Availability.IsKnown(MetadataFacts.TypeAttributes));
        Assert.Equal(MatchStatus.NotMatched, system.HasDirectAttribute(
            AttributeTarget.ForType(mapped.DeclaringType), system.Mapper.MapType(marker)).Status);
    }

    private static object Field(object value, string name) => value.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(value)!;
}
