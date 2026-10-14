using AspectN.Evaluation;
using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilCancellationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetInheritanceClosure_PreCanceled_Throws(bool associated)
    {
        using var system = new CecilTypeSystem(ModuleDefinition.CreateModule("Cancellation", ModuleKind.Dll));
        var candidate = associated ? system.Mapper.MapType(system.Module.TypeSystem.Object) : Detached();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            system.GetInheritanceClosure(candidate, cancellation.Token));
    }

    [Fact]
    public void GetInheritanceClosure_DetachedNotCanceled_ReturnsUnknown()
    {
        using var system = new CecilTypeSystem(ModuleDefinition.CreateModule("Cancellation", ModuleKind.Dll));

        var closure = system.GetInheritanceClosure(Detached());

        Assert.Empty(closure.Ancestors);
        Assert.Equal(MatchStatus.Unknown, closure.Failure!.Status);
        Assert.Contains(closure.Failure.Diagnostics, diagnostic => diagnostic.Code == "AN3001");
    }

    private static NamedTypeSignature Detached() => new(
        new TypeIdentity(new AssemblyIdentity("Example"), "Example", "Detached", 0, TypeKind.Class));
}
