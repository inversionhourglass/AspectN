using AspectN;
using AspectN.Cecil;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Reflection;
using AspectN.Roslyn;
using AspectN.TypeSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Mono.Cecil;

var backend = args[0];
var shape = args[1];
var depth = int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
using var module = ModuleDefinition.CreateModule("MappingDepth", ModuleKind.Dll);
var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
    .Select(path => MetadataReference.CreateFromFile(path));
var compilation = CSharpCompilation.Create("MappingDepth", references: references);
using var cecil = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
var reflection = new ReflectionTypeSystem();
var roslyn = new RoslynTypeSystem(compilation);
var definition = new TypeDefinition("Example", "Box`1", Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object);
definition.GenericParameters.Add(new GenericParameter("T", definition));
module.Types.Add(definition);
TypeReference cecilType = module.TypeSystem.Int32;
Type runtimeType = shape == "hidden-pointer" ? typeof(IntPtr) : typeof(int);
ITypeSymbol symbol = compilation.GetSpecialType(SpecialType.System_Int32);
TypeDefinition? containing = null;
for (var index = 0; index < depth; index++)
{
    if (shape is "containing" or "owner" or "owner-reference")
    {
        if (shape == "owner-reference")
        {
            cecilType = new TypeReference(index == 0 ? "Example" : string.Empty, "Level" + index, module, module)
            {
                DeclaringType = index == 0 ? null : cecilType
            };
            continue;
        }
        var nested = new TypeDefinition(containing is null ? "Example" : string.Empty, "Level" + index,
            containing is null ? Mono.Cecil.TypeAttributes.Public : Mono.Cecil.TypeAttributes.NestedPublic, module.TypeSystem.Object);
        if (containing is null) module.Types.Add(nested);
        else containing.NestedTypes.Add(nested);
        containing = nested;
        runtimeType = new ContainingType(index, index == 0 ? null : runtimeType);
        cecilType = nested;
        continue;
    }
    if (backend == "Cecil")
    {
        if (shape is "array" or "pointer") cecilType = new ArrayType(cecilType);
        else
        {
            var constructed = new GenericInstanceType(definition);
            constructed.GenericArguments.Add(cecilType);
            cecilType = constructed;
        }
    }
    if (backend == "Reflection") runtimeType = shape is "array" or "pointer" or "hidden-pointer" ? runtimeType.MakeArrayType() : typeof(List<>).MakeGenericType(runtimeType);
    if (backend == "Roslyn") symbol = shape is "array" or "pointer" ? compilation.CreateArrayTypeSymbol(symbol) :
        compilation.GetTypeByMetadataName("System.Collections.Generic.List`1")!.Construct(symbol);
}
if (shape is "owner" or "owner-reference")
{
    cecilType.Name += "`1";
    var parameter = new GenericParameter("T", cecilType);
    cecilType.GenericParameters.Add(parameter);
    cecilType = parameter;
}
if (shape == "containing" && backend == "Roslyn")
{
    using var image = new MemoryStream();
    module.Write(image);
    compilation = compilation.AddReferences(MetadataReference.CreateFromImage(image.ToArray()));
    roslyn = new RoslynTypeSystem(compilation);
    symbol = compilation.GetTypeByMetadataName("Example." + string.Join("+", Enumerable.Range(0, depth).Select(index => "Level" + index)))!;
}
if (shape == "pointer")
{
    cecilType = new PointerType(cecilType);
    runtimeType = runtimeType.MakePointerType();
    symbol = compilation.CreatePointerTypeSymbol(symbol);
}

Exception? failure = null;
var thread = new Thread(() =>
{
    try
    {
        if (shape == "hidden-pointer")
        {
            var facts = typeof(ReflectionSignatureMapper).Assembly.GetType("AspectN.Reflection.Internal.RuntimeTypeFacts")!;
            var contains = facts.GetMethod("ContainsIntPtr", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            if (contains.Invoke(null, [runtimeType]) is not true || contains.Invoke(null, [typeof(List<int[]>)]) is not false)
                throw new InvalidOperationException("Incorrect legacy function pointer ambiguity.");
            return;
        }
        ITypeSystem system = backend switch { "Cecil" => cecil, "Reflection" => reflection, _ => roslyn };
        var mapped = backend switch
        {
            "Cecil" => cecil.Mapper.MapType(cecilType),
            "Reflection" => reflection.Mapper.MapType(runtimeType),
            _ => roslyn.Mapper.MapType(symbol)
        };
        var current = mapped;
        if (shape is "owner" or "owner-reference")
        {
            var expected = "Example." + string.Join("/", Enumerable.Range(0, depth).Select(index => "Level" + index)) + "`1";
            if (mapped is not GenericParameterTypeSignature parameter || parameter.Parameter.OwnerId != expected || parameter.Parameter.Position != 0)
                throw new InvalidOperationException("Wrong deep declaration owner.");
            return;
        }
        if (shape == "containing")
        {
            if (mapped is not NamedTypeSignature named) throw new InvalidOperationException("Missing named type.");
            var identity = named.Identity;
            var count = 0;
            for (; identity is not null; identity = identity.ContainingType) count++;
            if (count != depth) throw new InvalidOperationException("Missing containing level.");
            return;
        }
        if (shape == "pointer")
        {
            if (mapped is not UnsupportedTypeSignature pointer || pointer.DisplayName != "System.Int32" + string.Concat(Enumerable.Repeat("[]", depth)) + "*")
                throw new InvalidOperationException("Wrong pointer display name.");
            return;
        }
        for (var index = 0; index < depth; index++) current = current switch
        {
            ArrayTypeSignature array => array.ElementType,
            NamedTypeSignature { TypeArguments.Count: 1 } generic => generic.TypeArguments[0],
            _ => throw new InvalidOperationException($"Missing mapped layer {index}.")
        };
        if (current is not NamedTypeSignature { Identity.MetadataName: "Int32" }) throw new InvalidOperationException("Wrong leaf.");
        if (depth >= 256)
        {
            if (!new AspectNEngine().CompileTypePattern("*").TryGetTypePattern(out var pattern)) throw new InvalidOperationException("Cannot compile.");
            var result = pattern.Match(mapped, system);
            if (result.Status != MatchStatus.Unknown || !result.Diagnostics.Any(diagnostic => diagnostic.Code == DiagnosticCodes.MetadataDepthExceeded))
                throw new InvalidOperationException("Match depth guard was lost.");
        }
    }
    catch (Exception exception)
    {
        failure = exception;
    }
}, 256 * 1024);
thread.Start();
thread.Join();
if (failure is not null) throw failure;
Console.WriteLine($"mapped:{backend}:{shape}:{depth}");

internal sealed class ContainingType(int index, Type? containing) : System.Reflection.TypeDelegator(typeof(object))
{
    public override string Name => "Level" + index;
    public override string Namespace => containing is null ? "Example" : string.Empty;
    public override Type? DeclaringType => containing;
}
