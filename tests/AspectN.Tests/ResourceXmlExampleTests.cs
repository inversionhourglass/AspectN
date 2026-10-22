using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests;

public sealed class ResourceXmlExampleTests
{
    [Fact]
    public void ParseOptions_XmlExamples_CompileAndRespectBoundaries()
    {
        var source = "*" + new string(' ', 255);
        Assert.Empty(AspectNParser.ParseTypePattern(source).Diagnostics);
        Assert.Contains(AspectNParser.ParseTypePattern(source + " ").Diagnostics, diagnostic => diagnostic.Code == "AN1003");
        var options = new AspectNParseOptions(maximumSourceLength: ResourceLimit.Finite(1024), maximumTypeDepth: ResourceLimit.Unlimited);
        var tree = AspectNParser.ParseTypePattern("int[][]", options);
        Assert.Empty(tree.Diagnostics);
        Assert.Same(options, tree.ParseOptions);
    }

    [Fact]
    public void MappingAndMatchOptions_XmlExamples_KeepIndependentQuotas()
    {
        var mapping = new AspectNMappingOptions(maximumTypeDepth: ResourceLimit.Finite(128),
            maximumTypeGenericParameters: ResourceLimit.Finite(80), maximumGeneratedTextLength: ResourceLimit.Unlimited);
        Assert.Equal(64, mapping.MaximumMethodGenericParameters.Value);
        Assert.Equal(100_000, mapping.MaximumWork.Value);
        Assert.Equal(1024, mapping.MaximumAttributeRecords.Value);
        var limits = new AspectNMatchOptions(maximumWork: ResourceLimit.Finite(500000),
            maximumBindingAlternatives: ResourceLimit.Finite(512), regexTimeout: TimeLimit.Unlimited);
        var compilation = new AspectNEngine().CompileTypePattern("*",
            compilationOptions: new AspectNCompilationOptions(GenericVariableMode.Capture, limits));
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        var candidate = new NamedTypeSignature(new TypeIdentity(new AssemblyIdentity("Example"), "Example", "C", 0, TypeKind.Class));
        Assert.Equal(MatchStatus.Matched, pattern.Match(candidate, new StubTypeSystem()).Status);
        Assert.Same(limits, pattern.Options.MatchOptions);
        Assert.Equal(16_384, limits.MaximumCanonicalSignatureLength.Value);
        Assert.Equal(TimeSpan.FromMilliseconds(500), TimeLimit.Finite(TimeSpan.FromMilliseconds(500)).Value);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void GenericSlots_OuterTypeSlots_AreIndependentOfMethodSlots(int typeLimit, bool accepted)
    {
        var compilation = new AspectNEngine().CompileMemberExpression("method(* Outer<>/Inner<>.M<>(..))",
            new AspectNParseOptions(maximumTypeGenericParameters: ResourceLimit.Finite(typeLimit),
                maximumMethodGenericParameters: ResourceLimit.Finite(1)));
        Assert.Equal(accepted, compilation.TryGetMemberPattern(out _));
    }
}
