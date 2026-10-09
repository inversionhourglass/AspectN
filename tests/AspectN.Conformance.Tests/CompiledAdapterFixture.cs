using System.Reflection;
using System.Runtime.Loader;
using AspectN.Cecil;
using AspectN.Reflection;
using AspectN.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Mono.Cecil;

namespace AspectN.Conformance.Tests;

internal sealed class CompiledAdapterFixture : IDisposable
{
    private readonly DefaultAssemblyResolver _resolver = new();
    private readonly AssemblyLoadContext _context = new($"Review.{Guid.NewGuid():N}", isCollectible: true);

    public CompiledAdapterFixture(string source)
    {
        var references = FixtureCompiler.TrustedPlatformReferences().ToArray();
        var compilation = CSharpCompilation.Create($"Review{Guid.NewGuid():N}", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var image = FixtureCompiler.Emit(compilation);
        _resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
        Cecil = new CecilTypeSystem(ModuleDefinition.ReadModule(new MemoryStream(image),
            new ReaderParameters { AssemblyResolver = _resolver }));
        Assembly = _context.LoadFromStream(new MemoryStream(image));
        Reflection = new ReflectionTypeSystem();
        Roslyn = new RoslynTypeSystem(compilation);
        RoslynMetadata = new RoslynTypeSystem(CSharpCompilation.Create("Consumer", references:
            references.Append(MetadataReference.CreateFromImage(image)), options:
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, metadataImportOptions: MetadataImportOptions.All)));
    }

    public CecilTypeSystem Cecil { get; }
    public Assembly Assembly { get; }
    public ReflectionTypeSystem Reflection { get; }
    public RoslynTypeSystem Roslyn { get; }
    public RoslynTypeSystem RoslynMetadata { get; }

    public void Dispose()
    {
        Cecil.Dispose();
        _resolver.Dispose();
        _context.Unload();
    }
}
