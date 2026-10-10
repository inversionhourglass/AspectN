using System.Runtime.Loader;
using AspectN.Cecil;
using AspectN.Diagnostics;
using AspectN.Reflection;
using AspectN.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Mono.Cecil;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class ModifierLimitConformanceTests
{
    [Theory]
    [InlineData(64, "parameter")]
    [InlineData(65, "parameter")]
    [InlineData(64, "ref-parameter")]
    [InlineData(65, "ref-parameter")]
    [InlineData(64, "return")]
    [InlineData(65, "return")]
    [InlineData(64, "property")]
    [InlineData(65, "property")]
    [InlineData(64, "function-parameter")]
    [InlineData(65, "function-parameter")]
    [InlineData(64, "function-property")]
    [InlineData(65, "function-property")]
    public void MapMember_ModifierTypeDepthLimit_AgreesAcrossMetadataBackends(int depth, string target)
    {
        using var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("ModifierLimit" + target + depth,
            new Version(1, 0)), "ModifierLimit", ModuleKind.Dll);
        var module = assembly.MainModule;
        TypeDefinition? marker = null;
        for (var index = 0; index < depth; index++)
        {
            var next = new TypeDefinition(marker is null ? "Review" : "", "Level" + index,
                marker is null ? TypeAttributes.Public : TypeAttributes.NestedPublic, module.TypeSystem.Object);
            if (marker is null) module.Types.Add(next);
            else marker.NestedTypes.Add(next);
            marker = next;
        }
        var host = new TypeDefinition("Review", "Host", TypeAttributes.Public | TypeAttributes.Abstract, module.TypeSystem.Object);
        module.Types.Add(host);
        var modified = new RequiredModifierType(marker!, target == "ref-parameter"
            ? new ByReferenceType(module.TypeSystem.Int32) : module.TypeSystem.Int32);
        TypeReference valueType = target is "function-parameter" or "function-property"
            ? new FunctionPointerType { ReturnType = modified } : modified;
        var isProperty = target is "property" or "function-property";
        var method = new MethodDefinition(isProperty ? "get_Value" : "Run",
            MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual,
            target == "return" || isProperty ? valueType : module.TypeSystem.Void);
        if (target is "parameter" or "ref-parameter") method.Parameters.Add(new ParameterDefinition(modified));
        if (target == "function-parameter")
            method.Parameters.Add(new ParameterDefinition(valueType));
        host.Methods.Add(method);
        var property = new PropertyDefinition("Value", PropertyAttributes.None, valueType) { GetMethod = method };
        if (isProperty) host.Properties.Add(property);
        using var image = new MemoryStream();
        assembly.Write(image);
        var bytes = image.ToArray();
        var context = new AssemblyLoadContext("ModifierLimit", isCollectible: true);
        try
        {
            var runtime = context.LoadFromStream(new MemoryStream(bytes)).GetType("Review.Host")!;
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path)).Append(MetadataReference.CreateFromImage(bytes));
            var compilation = CSharpCompilation.Create("ModifierConsumer", references: references);
            var symbol = compilation.GetTypeByMetadataName("Review.Host")!;
            var reflection = new ReflectionTypeSystem();
            var roslyn = new RoslynTypeSystem(compilation);
            using var cecil = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
            Func<object>[] mappings = isProperty
                ? [() => reflection.Mapper.MapProperty(runtime.GetProperty("Value")!),
                   () => roslyn.Mapper.MapProperty((IPropertySymbol)symbol.GetMembers("Value").Single()),
                   () => cecil.Mapper.MapProperty(property)]
                : [() => reflection.Mapper.MapMethod(runtime.GetMethod("Run")!),
                   () => roslyn.Mapper.MapMethod((IMethodSymbol)symbol.GetMembers("Run").Single()),
                   () => cecil.Mapper.MapMethod(method)];
            foreach (var map in mappings)
            {
                if (depth == 65)
                {
                    var exception = Assert.Throws<AspectNLimitException>(map);
                    Assert.Equal(DiagnosticCodes.MetadataDepthExceeded, exception.Diagnostic.Code);
                }
                else Assert.NotNull(map());
            }
        }
        finally
        {
            context.Unload();
        }
    }
}
