using System.Reflection;
using System.Runtime.Loader;
using AspectN.Cecil;
using AspectN.Metadata;
using AspectN.Reflection;
using AspectN.Roslyn;
using AspectN.TypeSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Mono.Cecil;

namespace AspectN.Conformance.Tests;

internal sealed class ReviewFixture : IDisposable
{
    private readonly DefaultAssemblyResolver _resolver = new();
    private readonly AssemblyLoadContext _loadContext = new("AspectN.ReviewFixture", isCollectible: true);
    private readonly Assembly _assembly;
    private readonly string _typeName;
    public RoslynTypeSystem Source { get; }
    public RoslynTypeSystem Metadata { get; }
    public CecilTypeSystem Cecil { get; }
    public ReflectionTypeSystem Reflection { get; } = new();

    internal ReviewFixture(string source, string typeName = "Review.Fixture", bool allowUnsafe = false)
    {
        _typeName = typeName;
        var compilation = CSharpCompilation.Create("AspectN.ReviewFixture",
            [CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest))],
            FixtureCompiler.TrustedPlatformReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: allowUnsafe));
        var image = FixtureCompiler.Emit(compilation);
        Source = new RoslynTypeSystem(compilation);
        Metadata = new RoslynTypeSystem(CSharpCompilation.Create("Reader",
            references: FixtureCompiler.TrustedPlatformReferences().Append(MetadataReference.CreateFromImage(image)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, metadataImportOptions: MetadataImportOptions.All)));
        _resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
        Cecil = new CecilTypeSystem(ModuleDefinition.ReadModule(new MemoryStream(image, writable: false),
            new ReaderParameters { AssemblyResolver = _resolver, InMemory = true }));
        _assembly = _loadContext.LoadFromStream(new MemoryStream(image, writable: false));
    }

    internal IEnumerable<(string Backend, ITypeSystem System, MethodSignature Method)> Methods(string name)
        => MethodMappings(name).Select(value => (value.Backend, value.System, value.Map()));

    internal IEnumerable<(string Backend, ITypeSystem System, Func<MethodSignature> Map)> MethodMappings(string name)
    {
        foreach (var (backend, system) in new[] { ("Roslyn source", Source), ("Roslyn PE", Metadata) })
        {
            var symbol = system.Compilation.GetTypeByMetadataName(_typeName)!.GetMembers(name).OfType<IMethodSymbol>().Single();
            yield return (backend, system, () => system.Mapper.MapMethod(symbol));
        }
        yield return ("Cecil", Cecil, () => Cecil.Mapper.MapMethod(Cecil.Module.GetType(_typeName).Methods.Single(method => method.Name == name)));
        yield return ("Reflection", Reflection, () => Reflection.Mapper.MapMethod(_assembly.GetType(_typeName)!.GetMethod(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)!));
    }

    public void Dispose()
    {
        Cecil.Dispose();
        _resolver.Dispose();
        _loadContext.Unload();
    }
}
