using AspectN.Cecil;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Reflection;
using AspectN.Roslyn;
using AspectN.TestAssembly;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Mono.Cecil;
using Xunit;
using MethodKind = AspectN.Metadata.MethodKind;

namespace AspectN.Conformance.Tests;

public sealed class InstanceOperatorMappingTests
{
    [Theory]
    [InlineData(typeof(InstanceOperatorFixture), "op_AdditionAssignment", MethodKind.Operator)]
    [InlineData(typeof(InstanceOperatorFixture), "op_CheckedAdditionAssignment", MethodKind.Operator)]
    [InlineData(typeof(InstanceOperatorFixture), "op_IncrementAssignment", MethodKind.Operator)]
    [InlineData(typeof(InstanceOperatorFixture), "op_CheckedIncrementAssignment", MethodKind.Operator)]
    [InlineData(typeof(InstanceOperatorFixture), "op_Explicit", MethodKind.Conversion)]
    [InlineData(typeof(InstanceOperatorFixture), "op_CheckedExplicit", MethodKind.Conversion)]
    [InlineData(typeof(GenericOperatorFixture<>), "op_Addition", MethodKind.Operator)]
    public void MapMethod_ActualOperators_PreservesKindAndSelectorDomain(Type fixtureType, string name, MethodKind expected)
    {
        var reflection = new ReflectionTypeSystem();
        var reflectionCandidate = reflection.Mapper.MapMethod(fixtureType.GetMethod(name)!);
        using var module = ModuleDefinition.ReadModule(fixtureType.Assembly.Location);
        using var cecil = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var definition = module.GetType(fixtureType.FullName!).Methods.Single(method => method.Name == name);
        var cecilCandidate = cecil.Mapper.MapMethod(definition);
        var compilation = CSharpCompilation.Create("OperatorMetadataProbe", references:
            FixtureCompiler.TrustedPlatformReferences().Append(
                MetadataReference.CreateFromFile(fixtureType.Assembly.Location)));
        var roslyn = new RoslynTypeSystem(compilation);
        var type = compilation.GetTypeByMetadataName(fixtureType.FullName!)!;
        var symbol = type.GetMembers(name).OfType<IMethodSymbol>().Single();
        var roslynCandidate = roslyn.Mapper.MapMethod(symbol);
        Assert.Equal(expected, reflectionCandidate.Kind);
        Assert.Equal(expected, cecilCandidate.Kind);
        Assert.Equal(expected, roslynCandidate.Kind);
        var methodPattern = Compile($"method(* {name}(..))");
        var callablePattern = Compile($"callable(* {name}(..))");
        Assert.Equal(MatchStatus.NotMatched, methodPattern.Match(reflectionCandidate, reflection).Status);
        Assert.Equal(MatchStatus.NotMatched, methodPattern.Match(cecilCandidate, cecil).Status);
        Assert.Equal(MatchStatus.NotMatched, methodPattern.Match(roslynCandidate, roslyn).Status);
        Assert.Equal(MatchStatus.Matched, callablePattern.Match(reflectionCandidate, reflection).Status);
        Assert.Equal(MatchStatus.Matched, callablePattern.Match(cecilCandidate, cecil).Status);
        Assert.Equal(MatchStatus.Matched, callablePattern.Match(roslynCandidate, roslyn).Status);
    }

    [Theory]
    [InlineData("op_AdditionAssignment")]
    [InlineData("op_IncrementAssignment")]
    [InlineData("op_CheckedAddition")]
    [InlineData("op_UnaryPlus")]
    [InlineData("op_CheckedSubtraction")]
    [InlineData("op_CheckedMultiply")]
    [InlineData("op_Modulus")]
    [InlineData("op_Division")]
    [InlineData("op_UnsignedRightShift")]
    [InlineData("op_BitwiseOr")]
    public void MapMethod_OrdinaryOperatorNamedMetadata_RemainsOrdinary(string name)
    {
        var compilation = CSharpCompilation.Create("OrdinaryOperatorMetadataProbe",
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, metadataImportOptions: MetadataImportOptions.All), references:
            FixtureCompiler.TrustedPlatformReferences().Append(
                MetadataReference.CreateFromFile(typeof(OperatorNamedMethodsFixture).Assembly.Location)));
        var roslyn = new RoslynTypeSystem(compilation);
        var type = compilation.GetTypeByMetadataName(typeof(OperatorNamedMethodsFixture).FullName!)!;
        var symbol = type.GetMembers(name).OfType<IMethodSymbol>().Single();
        Assert.Equal(MethodKind.OrdinaryMethod, roslyn.Mapper.MapMethod(symbol).Kind);
        var reflection = new ReflectionTypeSystem();
        var reflectionMethod = typeof(OperatorNamedMethodsFixture).GetMethod(name,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance)!;
        Assert.Equal(MethodKind.OrdinaryMethod, reflection.Mapper.MapMethod(reflectionMethod).Kind);
        using var module = ModuleDefinition.ReadModule(typeof(OperatorNamedMethodsFixture).Assembly.Location);
        using var cecil = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var definition = module.GetType(typeof(OperatorNamedMethodsFixture).FullName!).Methods.Single(method => method.Name == name);
        Assert.Equal(MethodKind.OrdinaryMethod, cecil.Mapper.MapMethod(definition).Kind);
    }

    private static CompiledMemberPattern Compile(string source)
    {
        var compilation = AspectNCompilation.Create(AspectNParser.ParseMemberExpression(source));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        return pattern!;
    }
}
