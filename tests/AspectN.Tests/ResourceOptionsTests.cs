namespace AspectN.Tests;

public class ResourceOptionsTests
{
    private static readonly string[] _CompilationProperties = ["GenericVariableMode", "MatchOptions"];
    private static readonly string[] _CompileParameters = ["source", "parseOptions", "compilationOptions", "cancellationToken"];

    [Theory]
    [InlineData(256, false)]
    [InlineData(257, true)]
    public void Parse_SourceBoundary_CountsFullUtf16Input(int length, bool exceeds)
    {
        var tree = AspectNParser.ParseTypePattern("*" + new string(' ', length - 1));
        Assert.Equal(exceeds, tree.Diagnostics.Any(diagnostic => diagnostic.Code == "AN1003"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Parse_LoweredTokenOrNodeLimit_StopsWithResourceDiagnostic(bool tokens)
    {
        var options = tokens
            ? new AspectNParseOptions(maximumTokens: ResourceLimit.Finite(1))
            : new AspectNParseOptions(maximumSyntaxNodes: ResourceLimit.Finite(1));
        var tree = AspectNParser.ParseTypePattern("int[]", options);
        Assert.Contains(tree.Diagnostics, diagnostic => diagnostic.Code == "AN1003");
        Assert.Same(options, tree.ParseOptions);
    }

    [Fact]
    public void Parse_RaisedTypeDepthAndSource_AllowBeyondOldConstants()
    {
        var options = new AspectNParseOptions(maximumSourceLength: ResourceLimit.Unlimited,
            maximumTypeDepth: ResourceLimit.Finite(100), maximumSyntaxDepth: ResourceLimit.Finite(400));
        var compilation = new AspectNEngine().CompileTypePattern("int" + string.Concat(Enumerable.Repeat("[]", 80)), options);
        Assert.True(compilation.TryGetTypePattern(out _));
        Assert.Same(options, compilation.SyntaxTree.ParseOptions);
    }

    [Fact]
    public void Options_InvalidDefaultLimit_ThrowsAtConstruction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AspectNParseOptions(maximumTokens: default(ResourceLimit)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AspectNMappingOptions(maximumWork: default(ResourceLimit)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AspectNMatchOptions(regexTimeout: default(TimeLimit)));
    }

    [Fact]
    public void Parse_DefaultSourceLimit_Rejects257CodeUnits()
    {
        var tree = AspectNParser.ParseTypePattern(new string('A', 257));
        Assert.Contains(tree.Diagnostics, diagnostic => diagnostic.Code == "AN1003");
    }

    [Theory]
    [InlineData(64, false)]
    [InlineData(80, true)]
    public void Compile_RaisedGenericSlots_AreReadFromSyntaxTree(long limit, bool allowed)
    {
        var tree = AspectNParser.ParseTypePattern("Many<" + new string(',', 79) + ">",
            new AspectNParseOptions(maximumTypeGenericParameters: ResourceLimit.Finite(limit)));
        var compilation = AspectNCompilation.Create(tree);
        Assert.Equal(allowed, compilation.TryGetTypePattern(out _));
        if (!allowed) Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == "AN2011");
    }

    [Fact]
    public void Compilation_Options_ExposeOnlyModeAndMatchOptions()
    {
        Assert.Equal(_CompilationProperties,
            typeof(AspectNCompilationOptions).GetProperties().Select(property => property.Name).Order().ToArray());
    }

    [Fact]
    public void Engine_CompileEntry_ExposesIndependentOptionsAndCancellation()
    {
        Assert.Equal(_CompileParameters,
            typeof(AspectNEngine).GetMethod("CompileTypePattern")!.GetParameters().Select(parameter => parameter.Name).ToArray());
    }

    [Fact]
    public void TypeSystem_AreEqual_ExposesOrdinaryCancellation()
    {
        Assert.Equal(typeof(CancellationToken), typeof(AspectN.TypeSystem.ITypeSystem).GetMethod("AreEqual")!.GetParameters().Last().ParameterType);
    }

    [Theory]
    [InlineData("ResourceLimit")]
    [InlineData("TimeLimit")]
    [InlineData("AspectNParseOptions")]
    [InlineData("AspectNMappingOptions")]
    [InlineData("AspectNMatchOptions")]
    public void PublicOptions_ThreeIndependentStages_AreAvailable(string name)
    {
        Assert.NotNull(typeof(AspectNEngine).Assembly.GetType("AspectN." + name));
    }
}
