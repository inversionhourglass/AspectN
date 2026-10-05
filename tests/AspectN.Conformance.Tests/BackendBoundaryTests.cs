using System.Runtime.Loader;
using AspectN.Cecil;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Reflection;
using AspectN.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;
using MethodKind = AspectN.Metadata.MethodKind;

namespace AspectN.Conformance.Tests;

public sealed class BackendBoundaryTests
{
    [Fact]
    public void MapMethod_CliEventRaiser_RecordsRoslynPeBoundary()
    {
        using var module = ModuleDefinition.CreateModule("EventBoundary", ModuleKind.Dll);
        var owner = AddOwner(module);
        var eventType = module.ImportReference(typeof(Action));
        var add = AddMethod(module, owner, "add_E");
        add.Parameters.Add(new ParameterDefinition(eventType));
        var remove = AddMethod(module, owner, "remove_E");
        remove.Parameters.Add(new ParameterDefinition(eventType));
        var raise = AddMethod(module, owner, "raise_E");
        owner.Events.Add(new EventDefinition("E", Mono.Cecil.EventAttributes.None, eventType)
        { AddMethod = add, RemoveMethod = remove, InvokeMethod = raise });
        AssertBoundary(module, "raise_E", MethodKind.EventRaiser);
    }

    [Fact]
    public void MapMethod_ForeignInstanceAssignmentWithoutCompilerMarker_RecordsRoslynPeBoundary()
    {
        using var module = ModuleDefinition.CreateModule("OperatorBoundary", ModuleKind.Dll);
        var method = AddMethod(module, AddOwner(module), "op_AdditionAssignment");
        method.IsStatic = false;
        method.HasThis = true;
        method.Parameters.Add(new ParameterDefinition(module.TypeSystem.Int32));
        AssertBoundary(module, method.Name, MethodKind.Operator);
    }

    [Fact]
    public void MapMethod_FunctionPointerModoptBehindModreq_RetainsConventionInOwnerId()
    {
        using var module = ModuleDefinition.CreateModule("ModifierBoundary", ModuleKind.Dll);
        var owner = AddOwner(module);
        var method = AddMethod(module, owner, "M");
        method.GenericParameters.Add(new GenericParameter("T", method));
        var convention = module.ImportReference(typeof(System.Runtime.CompilerServices.CallConvCdecl));
        var required = module.ImportReference(typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute));
        var pointer = new FunctionPointerType
        {
            CallingConvention = MethodCallingConvention.Unmanaged,
            ReturnType = new RequiredModifierType(required, new OptionalModifierType(convention, module.TypeSystem.Void))
        };
        method.Parameters.Add(new ParameterDefinition(pointer));
        using var system = new CecilTypeSystem(module);
        Assert.Contains("delegate*[cdecl]", system.Mapper.MapMethod(method).GenericParameters.Single().OwnerId);
    }

    private static void AssertBoundary(ModuleDefinition module, string methodName, MethodKind expected)
    {
        using var image = new MemoryStream();
        module.Write(image);
        var bytes = image.ToArray();
        using var cecil = new CecilTypeSystem(ModuleDefinition.ReadModule(new MemoryStream(bytes)));
        var cecilMethod = cecil.Mapper.MapMethod(cecil.Module.GetType("Review.Fixture").Methods.Single(method => method.Name == methodName));
        Assert.Equal(expected, cecilMethod.Kind);
        var compilation = CSharpCompilation.Create("Reader", references: FixtureCompiler.TrustedPlatformReferences()
            .Append(MetadataReference.CreateFromImage(bytes)), options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var roslyn = new RoslynTypeSystem(compilation);
        var symbol = compilation.GetTypeByMetadataName("Review.Fixture")!.GetMembers(methodName).OfType<IMethodSymbol>().Single();
        var roslynExpected = expected == MethodKind.Operator &&
            typeof(CSharpCompilation).Assembly.GetName().Version! >= new Version(5, 6)
            ? MethodKind.Operator : MethodKind.OrdinaryMethod;
        Assert.Equal(roslynExpected, roslyn.Mapper.MapMethod(symbol).Kind);
        if (expected == MethodKind.EventRaiser)
            Assert.Null(compilation.GetTypeByMetadataName("Review.Fixture")!.GetMembers("E").OfType<IEventSymbol>().Single().RaiseMethod);
        var loadContext = new AssemblyLoadContext("AspectN.Boundary", isCollectible: true);
        try
        {
            var assembly = loadContext.LoadFromStream(new MemoryStream(bytes));
            var reflection = new ReflectionTypeSystem();
            Assert.Equal(expected, reflection.Mapper.MapMethod(assembly.GetType("Review.Fixture")!.GetMethod(methodName)!).Kind);
        }
        finally { loadContext.Unload(); }
    }

    private static TypeDefinition AddOwner(ModuleDefinition module)
    {
        var owner = new TypeDefinition("Review", "Fixture", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Class,
            module.TypeSystem.Object);
        module.Types.Add(owner);
        return owner;
    }

    private static MethodDefinition AddMethod(ModuleDefinition module, TypeDefinition owner, string name)
    {
        var method = new MethodDefinition(name, Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static |
            Mono.Cecil.MethodAttributes.SpecialName, module.TypeSystem.Void);
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        owner.Methods.Add(method);
        return method;
    }
}
