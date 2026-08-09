using System.Reflection;
using AspectN.Cecil;
using Mono.Cecil;

namespace AspectN.LegacyOracle.Tests;

internal sealed class LegacyOracleContext : IDisposable
{
    private readonly Dictionary<string, LegacyCandidate> _candidates =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<LegacyCandidate>>
        _candidatesByAssembly = new(StringComparer.Ordinal);
    private readonly DefaultAssemblyResolver _resolver = new();
    private readonly List<CecilTypeSystem> _typeSystems = new();

    public LegacyOracleContext(
        bool compositeAccessibility,
        IReadOnlyCollection<LegacyAssemblySource> sources)
    {
        var assemblyPaths = sources.ToDictionary(
            source => source,
            ResolveAssemblyPath);
        foreach (var directory in assemblyPaths.Values
                     .Select(Path.GetDirectoryName)
                     .Where(directory => directory is not null)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _resolver.AddSearchDirectory(directory!);
        }

        _resolver.AddSearchDirectory(AppContext.BaseDirectory);
        _resolver.AddSearchDirectory(
            Path.GetDirectoryName(typeof(object).Assembly.Location)!);

        try
        {
            foreach (var source in sources)
            {
                AddAssembly(
                    source,
                    assemblyPaths[source],
                    compositeAccessibility);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public IReadOnlyDictionary<string, LegacyCandidate> Candidates =>
        _candidates;

    public IReadOnlyList<LegacyCandidate> GetCandidates(string assemblyName)
    {
        if (!_candidatesByAssembly.TryGetValue(
                assemblyName,
                out var candidates))
        {
            throw new InvalidOperationException(
                $"Legacy corpus assembly '{assemblyName}' was not loaded.");
        }

        return candidates;
    }

    public void Dispose()
    {
        foreach (var typeSystem in _typeSystems)
        {
            typeSystem.Dispose();
        }

        _typeSystems.Clear();
        _resolver.Dispose();
    }

    private void AddAssembly(
        LegacyAssemblySource source,
        string assemblyPath,
        bool compositeAccessibility)
    {
        var module = ModuleDefinition.ReadModule(
            assemblyPath,
            new ReaderParameters
            {
                AssemblyResolver = _resolver,
                ReadingMode = ReadingMode.Immediate
            });
        var actualName = module.Assembly.Name.Name;
        if (!StringComparer.Ordinal.Equals(actualName, source.Name))
        {
            module.Dispose();
            throw new InvalidOperationException(
                $"Legacy corpus file '{assemblyPath}' contains assembly " +
                $"'{actualName}', expected '{source.Name}'.");
        }

        var typeSystem = new CecilTypeSystem(
            module,
            new CecilMappingOptions(compositeAccessibility));
        _typeSystems.Add(typeSystem);
        var assemblyCandidates = EnumerateTypes(module.Types)
            .SelectMany(type => type.Methods)
            .Select(method => new LegacyCandidate(
                GetCandidateId(module, method),
                actualName,
                method,
                typeSystem))
            .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToList();
        _candidatesByAssembly.Add(actualName, assemblyCandidates);
        foreach (var candidate in assemblyCandidates)
        {
            _candidates.Add(candidate.Id, candidate);
        }
    }

    private static string ResolveAssemblyPath(LegacyAssemblySource source)
    {
        string path;
        if (source.SourceKind == "output")
        {
            path = Path.Combine(AppContext.BaseDirectory, source.FileName);
        }
        else if (source.SourceKind == "rougamo")
        {
            var root = Path.GetFullPath(GetAssemblyMetadata("RougamoRoot"));
            path = Path.GetFullPath(Path.Combine(root, source.RelativePath));
            var prefix = root.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!path.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Rougamo fixture path '{path}' escaped '{root}'.");
            }
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported Legacy assembly source '{source.SourceKind}'.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Legacy corpus assembly '{source.Name}' was not found.",
                path);
        }

        return path;
    }

    private static string GetAssemblyMetadata(string key)
    {
        var value = typeof(LegacyOracleContext).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == key)
            .Value;
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"Assembly metadata '{key}' is empty.")
            : value;
    }

    private static IEnumerable<TypeDefinition> EnumerateTypes(
        IEnumerable<TypeDefinition> roots)
    {
        foreach (var type in roots)
        {
            yield return type;
            foreach (var nested in EnumerateTypes(type.NestedTypes))
            {
                yield return nested;
            }
        }
    }

    private static string GetCandidateId(
        ModuleDefinition module,
        MethodDefinition method)
    {
        return $"{module.Assembly.Name.Name}::" +
               $"{method.DeclaringType.FullName}::" +
               $"0x{method.MetadataToken.ToInt32():X8}";
    }
}

internal sealed class LegacyCandidate
{
    public LegacyCandidate(
        string id,
        string assemblyName,
        MethodDefinition method,
        CecilTypeSystem typeSystem)
    {
        Id = id;
        AssemblyName = assemblyName;
        Method = method;
        TypeSystem = typeSystem;
    }

    public string AssemblyName { get; }

    public string Id { get; }

    public MethodDefinition Method { get; }

    public CecilTypeSystem TypeSystem { get; }
}
