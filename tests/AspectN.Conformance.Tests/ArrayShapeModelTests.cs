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
using Mono.Cecil.Cil;
using Xunit;
using AssemblyIdentity = AspectN.Metadata.AssemblyIdentity;
using Accessibility = AspectN.Metadata.Accessibility;
using RefKind = AspectN.Metadata.RefKind;
using CecilMethodAttributes = Mono.Cecil.MethodAttributes;
using CecilTypeAttributes = Mono.Cecil.TypeAttributes;

namespace AspectN.Conformance.Tests;

public sealed class ArrayShapeModelTests
{
    [Fact]
    public void AreEqual_MetadataVectorAndGeneralArray_AllAdaptersDistinguishShape()
    {
        WithAdapters((system, vector, general, nested) =>
        {
            Assert.Equal(ArrayShape.Vector, vector.Shape);
            Assert.Equal(ArrayShape.General, general.Shape);
            Assert.Equal(ArrayShape.General, Assert.IsType<ArrayTypeSignature>(nested.ElementType).Shape);
            Assert.Equal(MatchStatus.NotMatched, system.AreEqual(vector, general).Status);
            var vectorOfVector = new ArrayTypeSignature(vector, 1);
            Assert.Equal(MatchStatus.NotMatched, system.AreEqual(vectorOfVector, nested).Status);
            var definition = new NamedTypeSignature(new TypeIdentity(new AssemblyIdentity("Example"), "Example", "Box", 1,
                AspectN.Metadata.TypeKind.Class));
            Assert.Equal(MatchStatus.NotMatched, system.AreEqual(new NamedTypeSignature(definition.Identity, new[] { vector }),
                new NamedTypeSignature(definition.Identity, new[] { general })).Status);
        });
    }

    [Fact]
    public void IsAssignableTo_MetadataArrayShapes_AllAdaptersPreserveDirection()
    {
        WithAdapters((system, vector, general, _) =>
        {
            Assert.Equal(MatchStatus.Matched, system.IsAssignableTo(vector, general).Status);
            Assert.Equal(MatchStatus.NotMatched, system.IsAssignableTo(general, vector).Status);
            Assert.Equal(MatchStatus.Matched, system.IsAssignableTo(general, general).Status);
            var detachedVector = new ArrayTypeSignature(vector.ElementType, 1, ArrayShape.Vector);
            var detachedGeneral = new ArrayTypeSignature(vector.ElementType, 1, ArrayShape.General);
            Assert.Equal(MatchStatus.Matched, system.IsAssignableTo(detachedVector, detachedGeneral).Status);
            Assert.Equal(MatchStatus.NotMatched, system.IsAssignableTo(detachedGeneral, detachedVector).Status);
            Assert.Equal(MatchStatus.NotMatched, system.IsAssignableTo(new ArrayTypeSignature(detachedVector, 1),
                new ArrayTypeSignature(detachedGeneral, 1)).Status);
            Assert.Equal(MatchStatus.NotMatched, system.IsAssignableTo(new ArrayTypeSignature(detachedGeneral, 1),
                new ArrayTypeSignature(detachedVector, 1)).Status);
        });
    }

    [Fact]
    public void IsAssignableTo_ArrayGenericInterface_RequiresVectorShape()
    {
        var definition = new TypeIdentity(new AssemblyIdentity(string.Empty), "System.Collections.Generic", "IEnumerable", 1,
            AspectN.Metadata.TypeKind.Interface);
        WithAdapters((system, vector, general, _) =>
        {
            var target = new NamedTypeSignature(definition, new[] { vector.ElementType });
            Assert.Equal(MatchStatus.Matched, system.IsAssignableTo(vector, target).Status);
            Assert.Equal(MatchStatus.NotMatched, system.IsAssignableTo(general, target).Status);
        });
    }

    [Fact]
    public void Match_RankBasedArrayPattern_AllAdaptersAcceptBothRankOneShapes()
    {
        var compilation = new AspectNEngine().CompileTypePattern("int[]");
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        WithAdapters((system, vector, general, _) =>
        {
            Assert.Equal(MatchStatus.Matched, pattern!.Match(vector, system).Status);
            Assert.Equal(MatchStatus.Matched, pattern!.Match(general, system).Status);
        });
    }

    [Fact]
    public void AreEqual_GenericParameterOwnersDifferOnlyByArrayShape_AllAdaptersDistinguishOwners()
    {
        var image = CreateImage();
        using var module = ModuleDefinition.ReadModule(new MemoryStream(image));
        using var cecil = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var definitions = module.Types[1].Methods.Where(method => method.Name == "Owned").ToArray();
        Verify(cecil, definitions.Select(method => cecil.Mapper.MapType(method.GenericParameters[0])).ToArray());
        var roslyn = new RoslynTypeSystem(CSharpCompilation.Create("ArrayOwnerConsumer", references: new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location), MetadataReference.CreateFromImage(image)
        }));
        var symbols = roslyn.Compilation.GetTypeByMetadataName("Example.ArrayShapes")!.GetMembers("Owned").OfType<IMethodSymbol>();
        Verify(roslyn, symbols.Select(method => roslyn.Mapper.MapType(method.TypeParameters[0])).ToArray());
        var loadContext = new AssemblyLoadContext("ArrayOwners", isCollectible: true);
        try
        {
            var assembly = loadContext.LoadFromStream(new MemoryStream(image));
            var reflection = new ReflectionTypeSystem();
            var methods = assembly.GetType("Example.ArrayShapes")!.GetMethods().Where(method => method.Name == "Owned");
            Verify(reflection, methods.Select(method => reflection.Mapper.MapType(method.GetGenericArguments()[0])).ToArray());
        }
        finally { loadContext.Unload(); }

        static void Verify(ITypeSystem system, TypeSignature[] parameters)
        {
            Assert.Equal(2, parameters.Length);
            Assert.Equal(MatchStatus.NotMatched, system.AreEqual(parameters[0], parameters[1]).Status);
        }
    }

    [Theory]
    [InlineData("int[]", 0, true)]
    [InlineData("int[]", 0, false)]
    [InlineData("int[][]", 1, true)]
    [InlineData("int[][]", 1, false)]
    [InlineData("Example.Box<int[]>", 2, true)]
    [InlineData("Example.Box<int[]>", 2, false)]
    public void Match_AttributeAndRelationshipArrayArguments_PreserveRankSemantics(string argumentPattern, int wrapping, bool attributeQuery)
    {
        WithAdapters((system, _, general, _) =>
        {
            TypeSignature argument = wrapping switch
            {
                1 => new ArrayTypeSignature(general, 1),
                2 => Box(general),
                _ => general
            };
            var attributeType = new NamedTypeSignature(new TypeIdentity(new AssemblyIdentity("Example"), "Example",
                "ArrayAttribute", 1, AspectN.Metadata.TypeKind.Class), new[] { argument });
            var declaringType = Box(argument);
            var candidate = new MethodSignature("Run", AspectN.Metadata.MethodKind.OrdinaryMethod, Accessibility.Public,
                false, false, declaringType, new TypeUseSignature(general.ElementType, RefKind.None, TypeUseKind.Return),
                Array.Empty<ParameterSignature>(), Array.Empty<GenericParameterSignature>(), Array.Empty<TypeSignature>(),
                new[] { new AttributeSignature(attributeType, AttributeTargetKind.Method) }, Array.Empty<AttributeSignature>());
            var attributeCompilation = new AspectNEngine().CompileMemberExpression($"attr(call Example.ArrayAttribute<{argumentPattern}>)");
            Assert.True(attributeCompilation.TryGetMemberPattern(out var attributePattern));
            if (attributeQuery)
            {
                Assert.Equal(MatchStatus.Matched, attributePattern!.Match(candidate, system).Status);
                return;
            }
            var relationshipCompilation = new AspectNEngine().CompileTypePattern($"Example.Box<{argumentPattern}>+");
            Assert.True(relationshipCompilation.TryGetTypePattern(out var relationshipPattern));
            Assert.Equal(MatchStatus.Matched, relationshipPattern!.Match(declaringType, system).Status);
        });

        static NamedTypeSignature Box(TypeSignature argument) => new(new TypeIdentity(new AssemblyIdentity("Example"),
            "Example", "Box", 1, AspectN.Metadata.TypeKind.Class), new[] { argument });
    }

    [Fact]
    public void Match_RankOneArrayInGenericAncestor_AllAdaptersPreserveRankSemantics()
    {
        var compilation = new AspectNEngine().CompileTypePattern("Example.Box<int[]>+");
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        WithAdapters((_, _, _, _) => { }, (system, candidate) =>
            Assert.Equal(MatchStatus.Matched, pattern!.Match(candidate, system).Status));
    }

    private static void WithAdapters(Action<ITypeSystem, ArrayTypeSignature, ArrayTypeSignature, ArrayTypeSignature> verify,
        Action<ITypeSystem, TypeSignature>? verifyAncestor = null)
    {
        var image = CreateImage();
        using var module = ModuleDefinition.ReadModule(new MemoryStream(image));
        using var cecil = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var owner = module.Types.Single(type => type.FullName == "Example.ArrayShapes");
        verify(cecil, MapCecil("Vector"), MapCecil("General"), MapCecil("NestedGeneral"));
        verifyAncestor?.Invoke(cecil, cecil.Mapper.MapType(owner.Methods.Single(method => method.Name == "Ancestor").ReturnType));

        var roslyn = new RoslynTypeSystem(CSharpCompilation.Create("ArrayShapeConsumer", references: new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location), MetadataReference.CreateFromImage(image)
        }));
        var symbol = roslyn.Compilation.GetTypeByMetadataName("Example.ArrayShapes")!;
        verify(roslyn, MapRoslyn("Vector"), MapRoslyn("General"), MapRoslyn("NestedGeneral"));
        verifyAncestor?.Invoke(roslyn, roslyn.Mapper.MapType(symbol.GetMembers("Ancestor").OfType<IMethodSymbol>().Single().ReturnType));

        var loadContext = new AssemblyLoadContext("ArrayShapes", isCollectible: true);
        try
        {
            var assembly = loadContext.LoadFromStream(new MemoryStream(image));
            var type = assembly.GetType("Example.ArrayShapes", throwOnError: true)!;
            var reflection = new ReflectionTypeSystem();
            verify(reflection, MapReflection("Vector"), MapReflection("General"), MapReflection("NestedGeneral"));
            verifyAncestor?.Invoke(reflection, reflection.Mapper.MapType(type.GetMethod("Ancestor")!.ReturnType));
            ArrayTypeSignature MapReflection(string name) => Assert.IsType<ArrayTypeSignature>(
                reflection.Mapper.MapType(type.GetMethod(name, BindingFlags.Public | BindingFlags.Static)!.ReturnType));
        }
        finally { loadContext.Unload(); }

        ArrayTypeSignature MapCecil(string name) => Assert.IsType<ArrayTypeSignature>(
            cecil.Mapper.MapType(owner.Methods.Single(method => method.Name == name).ReturnType));
        ArrayTypeSignature MapRoslyn(string name) => Assert.IsType<ArrayTypeSignature>(
            roslyn.Mapper.MapType(symbol.GetMembers(name).OfType<IMethodSymbol>().Single().ReturnType));
    }

    private static byte[] CreateImage()
    {
        using var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("ArrayShapeFixture", new Version(1, 0)),
            "ArrayShapeFixture", ModuleKind.Dll);
        var module = assembly.MainModule;
        var owner = new TypeDefinition("Example", "ArrayShapes", CecilTypeAttributes.Public, module.ImportReference(typeof(object)));
        module.Types.Add(owner);
        var element = module.ImportReference(typeof(int));
        var vector = new ArrayType(element);
        var general = new ArrayType(element, 1);
        general.Dimensions[0] = new ArrayDimension(0, null);
        var box = new TypeDefinition("Example", "Box`1", CecilTypeAttributes.Public, module.ImportReference(typeof(object)));
        box.GenericParameters.Add(new GenericParameter("T", box));
        module.Types.Add(box);
        var baseType = new GenericInstanceType(box);
        baseType.GenericArguments.Add(general);
        var derived = new TypeDefinition("Example", "Derived", CecilTypeAttributes.Public, baseType);
        module.Types.Add(derived);
        var ancestorMethod = new MethodDefinition("Ancestor", CecilMethodAttributes.Public | CecilMethodAttributes.Static, derived);
        ancestorMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
        ancestorMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        owner.Methods.Add(ancestorMethod);
        foreach (var (name, type) in new[] { ("Vector", vector), ("General", general), ("NestedGeneral", new ArrayType(general)) })
        {
            var method = new MethodDefinition(name, CecilMethodAttributes.Public | CecilMethodAttributes.Static, type);
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            owner.Methods.Add(method);
        }

        foreach (var isVector in new[] { true, false })
        {
            var method = new MethodDefinition("Owned", CecilMethodAttributes.Public | CecilMethodAttributes.Static, module.TypeSystem.Void);
            var parameter = new GenericParameter("T", method);
            method.GenericParameters.Add(parameter);
            var array = new ArrayType(parameter);
            if (!isVector) array.Dimensions[0] = new ArrayDimension(0, null);
            method.Parameters.Add(new ParameterDefinition(array));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            owner.Methods.Add(method);
        }

        using var stream = new MemoryStream();
        assembly.Write(stream);
        return stream.ToArray();
    }
}
