using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using AspectN.Cecil;
using AspectN.Reflection;
using AspectN.Roslyn;
using AspectN.TestAssembly;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Mono.Cecil;

namespace AspectN.Conformance.Tests;

public sealed class FixtureCompiler : IDisposable
{
    private const string RESOURCE_NAME =
        "AspectN.TestAssembly.ConformanceFixture.cs";

    private readonly DefaultAssemblyResolver _resolver;
    private readonly AssemblyLoadContext _loadContext;

    public FixtureCompiler()
    {
        var source = ReadFixtureSource();
        var references = TrustedPlatformReferences();
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(
                LanguageVersion.Latest));
        Compilation = CSharpCompilation.Create(
            "AspectN.Conformance.Emitted",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                deterministic: true));
        var image = Emit(Compilation);

        _resolver = new DefaultAssemblyResolver();
        _resolver.AddSearchDirectory(
            Path.GetDirectoryName(typeof(object).Assembly.Location)!);
        var module = ModuleDefinition.ReadModule(
            new MemoryStream(image, writable: false),
            new ReaderParameters
            {
                AssemblyResolver = _resolver,
                InMemory = true
            });
        Cecil = new CecilTypeSystem(module);

        _loadContext = new AssemblyLoadContext(
            $"AspectN.Conformance.{Guid.NewGuid():N}",
            isCollectible: true);
        ReflectionAssembly = _loadContext.LoadFromStream(
            new MemoryStream(image, writable: false));
        Reflection = new ReflectionTypeSystem();
        Roslyn = new RoslynTypeSystem(Compilation);
    }

    public CSharpCompilation Compilation { get; }

    internal static UnresolvedFixture CreateUnresolved()
    {
        const string externalSource = """
            namespace External;

            public class Base
            {
            }

            public class Dependency : Base
            {
            }
            """;
        var references = TrustedPlatformReferences().ToArray();
        var externalCompilation = CSharpCompilation.Create(
            "AspectN.Conformance.External",
            [CSharpSyntaxTree.ParseText(externalSource)],
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                deterministic: true));
        var externalImage = Emit(externalCompilation);
        var fixtureReferences = references
            .Append(
                MetadataReference.CreateFromImage(
                    ImmutableArray.Create(externalImage),
                    filePath: "AspectN.Conformance.External.dll"));
        var syntaxTree = CSharpSyntaxTree.ParseText(
            ReadFixtureSource(),
            CSharpParseOptions.Default
                .WithLanguageVersion(LanguageVersion.Latest)
                .WithPreprocessorSymbols("CONFORMANCE_UNRESOLVED"));
        var compilation = CSharpCompilation.Create(
            "AspectN.Conformance.Unresolved",
            [syntaxTree],
            fixtureReferences,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                deterministic: true));
        var image = Emit(compilation);
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(
            Path.GetDirectoryName(typeof(object).Assembly.Location)!);
        var module = ModuleDefinition.ReadModule(
            new MemoryStream(image, writable: false),
            new ReaderParameters
            {
                AssemblyResolver = resolver,
                InMemory = true
            });
        var loadContext = new AssemblyLoadContext(
            $"AspectN.Conformance.Unresolved.{Guid.NewGuid():N}",
            isCollectible: true);
        var reflectionAssembly = loadContext.LoadFromStream(
            new MemoryStream(image, writable: false));
        return new UnresolvedFixture(
            compilation,
            new CecilTypeSystem(module),
            resolver,
            new ReflectionTypeSystem(),
            reflectionAssembly,
            loadContext);
    }

    public CecilTypeSystem Cecil { get; }

    public ReflectionTypeSystem Reflection { get; }

    public Assembly ReflectionAssembly { get; }

    public RoslynTypeSystem Roslyn { get; }

    public void Dispose()
    {
        Cecil.Dispose();
        _resolver.Dispose();
        _loadContext.Unload();
    }

    private static byte[] Emit(CSharpCompilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
        {
            var diagnostics = string.Join(
                Environment.NewLine,
                result.Diagnostics.Select(diagnostic => diagnostic.ToString()));
            throw new InvalidOperationException(
                $"Conformance fixture emit failed:{Environment.NewLine}" +
                diagnostics);
        }

        return stream.ToArray();
    }

    private static string ReadFixtureSource()
    {
        var assembly = typeof(LegacyFixture).Assembly;
        using var stream = assembly.GetManifestResourceStream(RESOURCE_NAME) ??
            throw new InvalidOperationException(
                $"Embedded fixture '{RESOURCE_NAME}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static IEnumerable<MetadataReference> TrustedPlatformReferences()
    {
        var trustedAssemblies = AppContext.GetData(
            "TRUSTED_PLATFORM_ASSEMBLIES") as string ??
            throw new InvalidOperationException(
                "Trusted platform assemblies are unavailable.");
        return trustedAssemblies
            .Split(Path.PathSeparator)
            .Where(path => !StringComparer.OrdinalIgnoreCase.Equals(
                Path.GetFileName(path),
                "AspectN.TestAssembly.dll"))
            .Select(path => MetadataReference.CreateFromFile(path));
    }
}

internal sealed class UnresolvedFixture : IDisposable
{
    private readonly DefaultAssemblyResolver _resolver;
    private readonly AssemblyLoadContext _loadContext;

    public UnresolvedFixture(
        CSharpCompilation compilation,
        CecilTypeSystem cecil,
        DefaultAssemblyResolver resolver,
        ReflectionTypeSystem reflection,
        Assembly reflectionAssembly,
        AssemblyLoadContext loadContext)
    {
        Compilation = compilation;
        Cecil = cecil;
        _resolver = resolver;
        Reflection = reflection;
        ReflectionAssembly = reflectionAssembly;
        _loadContext = loadContext;
        Roslyn = new RoslynTypeSystem(compilation);
    }

    public CSharpCompilation Compilation { get; }

    public CecilTypeSystem Cecil { get; }

    public ReflectionTypeSystem Reflection { get; }

    public Assembly ReflectionAssembly { get; }

    public RoslynTypeSystem Roslyn { get; }

    public void Dispose()
    {
        Cecil.Dispose();
        _resolver.Dispose();
        _loadContext.Unload();
    }
}
