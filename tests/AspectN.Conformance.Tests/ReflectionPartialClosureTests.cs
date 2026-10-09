using System.Reflection;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class ReflectionPartialClosureTests
{
    [Theory]
    [InlineData("Known*+", MatchStatus.Matched)]
    [InlineData("KnownBase+", MatchStatus.Matched)]
    [InlineData("Missing*+", MatchStatus.Unknown)]
    public void GetInheritanceClosure_UnreadableInterface_PreservesKnownBaseBranch(string expression, MatchStatus expected)
    {
        var references = FixtureCompiler.TrustedPlatformReferences().ToArray();
        var dependency = Emit("MissingDependency", "public interface IMissing {}", references);
        var main = Emit("PartialClosure", "public class KnownBase {} public class C : KnownBase, IMissing {}",
            references.Append(MetadataReference.CreateFromImage(dependency)));
        using var context = new MetadataLoadContext(new PathAssemblyResolver(
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)), "System.Private.CoreLib");
        var native = context.LoadFromByteArray(main).GetType("C")!;
        Assert.Equal("KnownBase", native.BaseType!.Name);
        Assert.Throws<FileNotFoundException>(() => native.GetInterfaces());

        var system = new ReflectionTypeSystem();
        var candidate = system.Mapper.MapType(native);
        var closure = system.GetInheritanceClosure(candidate);
        Assert.Contains(closure.Ancestors, ancestor => ancestor is NamedTypeSignature named && named.Identity.MetadataName == "KnownBase");
        Assert.NotNull(closure.Failure);
        Assert.Contains(closure.Failure.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.ResolutionFailure);
        var compilation = new AspectNEngine().CompileTypePattern(expression);
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        Assert.Equal(expected, pattern.Match(candidate, system).Status);
    }

    private static byte[] Emit(string name, string source, IEnumerable<MetadataReference> references) =>
        FixtureCompiler.Emit(CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));
}
