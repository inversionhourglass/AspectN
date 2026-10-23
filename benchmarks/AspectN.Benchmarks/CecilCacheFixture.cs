using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Benchmarks;

internal sealed class CecilCacheFixture : IDisposable
{
    internal const int BATCH_SIZE = 1024;
    private readonly AssemblyDefinition _assembly;
    private readonly string[] _names;
    internal ModuleDefinition Module => _assembly.MainModule;

    internal CecilCacheFixture()
    {
        _assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("AspectN.CacheBenchmarks", new Version(1, 0)),
            "AspectN.CacheBenchmarks.dll", ModuleKind.Dll);
        _names = Enumerable.Range(0, BATCH_SIZE).Select(index => $"Value{index:D4}").ToArray();
        foreach (var name in _names)
            Module.Types.Add(new TypeDefinition("CacheFixture", name, TypeAttributes.Public | TypeAttributes.Class, Module.TypeSystem.Object));
        var pair = new TypeDefinition("CacheFixture", "Pair`2", TypeAttributes.Public | TypeAttributes.Class, Module.TypeSystem.Object);
        pair.GenericParameters.Add(new GenericParameter("T", pair));
        pair.GenericParameters.Add(new GenericParameter("U", pair));
        Module.Types.Add(pair);
    }

    internal TypeReference Create(CecilCacheShape shape, int index)
    {
        var leaf = new TypeReference("CacheFixture", _names[index], Module, Module);
        if (shape == CecilCacheShape.Named) return leaf;
        if (shape == CecilCacheShape.GenericArray)
            return Pair(new ArrayType(leaf, 2), Pair(new ArrayType(leaf), leaf));
        TypeReference result = leaf;
        for (var depth = 0; depth < 32; depth++) result = new ArrayType(result);
        return result;
    }

    internal void Validate(TypeSignature signature, CecilCacheShape shape, int index)
    {
        if (shape == CecilCacheShape.Named) { ValidateLeaf(signature, index); return; }
        if (shape == CecilCacheShape.GenericArray)
        {
            var pair = ValidatePair(signature);
            ValidateLeaf(ValidateArray(pair.TypeArguments[0], 2, ArrayShape.General), index);
            var inner = ValidatePair(pair.TypeArguments[1]);
            ValidateLeaf(ValidateArray(inner.TypeArguments[0], 1, ArrayShape.Vector), index);
            ValidateLeaf(inner.TypeArguments[1], index);
            return;
        }
        for (var depth = 0; depth < 32; depth++) signature = ValidateArray(signature, 1, ArrayShape.Vector);
        ValidateLeaf(signature, index);
    }

    private GenericInstanceType Pair(TypeReference first, TypeReference second)
    {
        var pair = new GenericInstanceType(new TypeReference("CacheFixture", "Pair`2", Module, Module));
        pair.GenericArguments.Add(first);
        pair.GenericArguments.Add(second);
        return pair;
    }

    private void ValidateLeaf(TypeSignature signature, int index)
    {
        if (signature is not NamedTypeSignature named || named.Identity.MetadataName != _names[index]
            || named.Identity.Namespace != "CacheFixture" || named.Identity.Assembly.Name != "AspectN.CacheBenchmarks")
            throw new InvalidOperationException("Unexpected named signature.");
    }

    private static NamedTypeSignature ValidatePair(TypeSignature signature)
    {
        if (signature is not NamedTypeSignature named || named.Identity.MetadataName != "Pair" || named.Identity.GenericArity != 2
            || named.TypeArguments.Count != 2)
            throw new InvalidOperationException("Unexpected generic signature.");
        return named;
    }

    private static TypeSignature ValidateArray(TypeSignature signature, int rank, ArrayShape shape)
    {
        if (signature is not ArrayTypeSignature array || array.Rank != rank || array.Shape != shape)
            throw new InvalidOperationException("Unexpected array signature.");
        return array.ElementType;
    }

    public void Dispose() => _assembly.Dispose();
}
