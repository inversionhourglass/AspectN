using AspectN.Evaluation;
using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilDetachedArrayTests
{
    [Theory]
    [InlineData("detached", 0)]
    [InlineData("detached", 1)]
    [InlineData("detached", 2)]
    [InlineData("unresolved", 0)]
    [InlineData("unresolved", 1)]
    [InlineData("unresolved", 2)]
    [InlineData("native", 0)]
    [InlineData("native", 1)]
    [InlineData("native", 2)]
    public void IsAssignableTo_ArrayWithNonNullElement_PreservesFixedRelations(string element, int shape)
    {
        using var fixture = new Fixture(element, shape);
        foreach (var (space, name) in FixedRelations())
        {
            var result = fixture.System.IsAssignableTo(fixture.Signature, Query(space, name));
            Assert.Equal(MatchStatus.Matched, result.Status);
            Assert.Empty(result.Diagnostics);
        }
        var nativeObject = fixture.System.Mapper.MapType(fixture.Module.TypeSystem.Object);
        Assert.Equal(MatchStatus.Matched, fixture.System.IsAssignableTo(fixture.Signature, nativeObject).Status);
    }

    [Theory]
    [InlineData("detached", 0)]
    [InlineData("detached", 1)]
    [InlineData("detached", 2)]
    [InlineData("unresolved", 0)]
    [InlineData("unresolved", 1)]
    [InlineData("unresolved", 2)]
    [InlineData("native", 0)]
    [InlineData("native", 1)]
    [InlineData("native", 2)]
    public void GetInheritanceClosure_ArrayWithNonNullElement_ReturnsCompleteFixedClosure(string element, int shape)
    {
        using var fixture = new Fixture(element, shape);
        var closure = fixture.System.GetInheritanceClosure(fixture.Signature);
        Assert.Null(closure.Failure);
        Assert.Equal(shape == 0 ? 13 : 8, closure.Ancestors.Count);
        foreach (var (space, name) in FixedRelations())
        {
            var relation = Assert.Single(closure.Ancestors.OfType<NamedTypeSignature>(), item =>
                item.Identity.Namespace == space && item.Identity.MetadataName == name);
            Assert.Equal(fixture.Module.TypeSystem.CoreLibrary.Name, relation.Identity.Assembly.Name);
        }
        foreach (var generic in closure.Ancestors.OfType<NamedTypeSignature>().Where(item => item.Identity.GenericArity == 1))
        {
            Assert.Equal(MatchStatus.Matched,
                fixture.System.AreEqual(fixture.Signature.ElementType, Assert.Single(generic.TypeArguments)).Status);
        }
    }

    [Theory]
    [InlineData("detached", 0)]
    [InlineData("detached", 1)]
    [InlineData("detached", 2)]
    [InlineData("unresolved", 0)]
    [InlineData("unresolved", 1)]
    [InlineData("unresolved", 2)]
    [InlineData("native", 0)]
    [InlineData("native", 1)]
    [InlineData("native", 2)]
    public void IsAssignableTo_GenericArrayInterfaces_RequireVectorAndSameElement(string element, int shape)
    {
        using var fixture = new Fixture(element, shape);
        foreach (var name in new[] { "IEnumerable", "ICollection", "IList", "IReadOnlyList", "IReadOnlyCollection" })
        {
            var target = Query("System.Collections.Generic", name, fixture.Signature.ElementType);
            var result = fixture.System.IsAssignableTo(fixture.Signature, target);
            Assert.Equal(shape == 0 ? MatchStatus.Matched : MatchStatus.NotMatched, result.Status);
            Assert.Empty(result.Diagnostics);
            var wrong = fixture.System.IsAssignableTo(fixture.Signature,
                Query("System.Collections.Generic", name, Query("Other", "DifferentElement")));
            Assert.Equal(MatchStatus.NotMatched, wrong.Status);
            Assert.Empty(wrong.Diagnostics);
        }
    }

    [Fact]
    public void GetInheritanceClosure_ArrayWithOwnModule_PrefersOwnCoreLibrary()
    {
        using var fixture = new Fixture("detached", 0);
        using var own = ModuleDefinition.CreateModule("OwnModule", ModuleKind.Dll);
        Assert.IsType<AssemblyNameReference>(own.TypeSystem.CoreLibrary).Name = "Own.CoreLibrary";
        var array = new ArrayType(new TypeReference("Own", "Element", own, own));
        var signature = fixture.System.Mapper.MapType(array);
        var closure = fixture.System.GetInheritanceClosure(signature);

        Assert.Null(closure.Failure);
        Assert.NotEqual(fixture.Module.TypeSystem.CoreLibrary.Name, own.TypeSystem.CoreLibrary.Name);
        Assert.All(closure.Ancestors.OfType<NamedTypeSignature>(), relation =>
            Assert.Equal("Own.CoreLibrary", relation.Identity.Assembly.Name));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Query_DetachedArrayWithPreCancelledToken_PropagatesCancellation(bool closure)
    {
        using var fixture = new Fixture("detached", 0);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
        {
            if (closure) fixture.System.GetInheritanceClosure(fixture.Signature, cancellation.Token);
            else fixture.System.IsAssignableTo(fixture.Signature, Query("System", "Object"), cancellation.Token);
        });
    }

    private static NamedTypeSignature Query(string space, string name, params TypeSignature[] arguments) => new(
        new TypeIdentity(new AssemblyIdentity(""), space, name, arguments.Length, TypeKind.Unknown), arguments);

    private static IEnumerable<(string Space, string Name)> FixedRelations()
    {
        yield return ("System", "Array");
        yield return ("System", "Object");
        yield return ("System.Collections", "IEnumerable");
        yield return ("System.Collections", "ICollection");
        yield return ("System.Collections", "IList");
        yield return ("System", "ICloneable");
        yield return ("System.Collections", "IStructuralComparable");
        yield return ("System.Collections", "IStructuralEquatable");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly RejectingResolver _resolver = new();
        public ModuleDefinition Module { get; }
        public CecilTypeSystem System { get; }
        public ArrayTypeSignature Signature { get; }

        public Fixture(string element, int shape)
        {
            Module = ModuleDefinition.CreateModule("C07Context", new ModuleParameters
            {
                Kind = ModuleKind.Dll,
                AssemblyResolver = _resolver
            });
            System = new CecilTypeSystem(Module, new CecilMappingOptions(leaveOpen: true));
            var type = element switch
            {
                "detached" => new TypeReference("Example", "Unknown", null, null),
                "unresolved" => new TypeReference("Example", "Unknown", Module,
                    new AssemblyNameReference("Missing.C07.Element", new Version(1, 0))),
                _ => Module.TypeSystem.Int32
            };
            if (element == "unresolved") Assert.Throws<AssemblyResolutionException>(() => type.Resolve());
            var array = new ArrayType(type, shape == 2 ? 2 : 1);
            if (shape == 1) array.Dimensions[0] = new ArrayDimension(0, null);
            Assert.NotNull(array.ElementType);
            Assert.Equal(element == "detached", array.Module is null);
            Signature = Assert.IsType<ArrayTypeSignature>(System.Mapper.MapType(array));
        }

        public void Dispose()
        {
            System.Dispose();
            Module.Dispose();
            _resolver.Dispose();
        }
    }

    private sealed class RejectingResolver : IAssemblyResolver
    {
        public AssemblyDefinition Resolve(AssemblyNameReference name) => throw new AssemblyResolutionException(name);
        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters) => throw new AssemblyResolutionException(name);
        public void Dispose() { }
    }
}
