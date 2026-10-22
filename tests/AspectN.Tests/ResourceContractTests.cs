using System.Reflection;

namespace AspectN.Tests;

public sealed class ResourceContractTests
{
    [Theory]
    [InlineData(typeof(AspectNParseOptions))]
    [InlineData(typeof(AspectNMappingOptions))]
    [InlineData(typeof(AspectNMatchOptions))]
    public void Options_EveryCount_AcceptsFiniteAndUnlimitedAndRejectsInvalid(Type type)
    {
        var constructor = type.GetConstructors().Single();
        var parameters = constructor.GetParameters();
        var defaults = constructor.Invoke(new object?[parameters.Length]);
        var expected = type == typeof(AspectNParseOptions) ? new long[] { 256, 1024, 2048, 128, 256, 64, 64, 64 }
            : type == typeof(AspectNMappingOptions) ? [64, 64, 64, 100_000, 1024, 16_384]
            : [64, 64, 64, 250_000, 1024, 4096, 1024, 16_384, 256, 50_000];
        for (var index = 0; index < parameters.Length; index++)
        {
            if (parameters[index].ParameterType != typeof(ResourceLimit?)) continue;
            var property = type.GetProperty(char.ToUpperInvariant(parameters[index].Name![0]) + parameters[index].Name![1..])!;
            var defaultLimit = (ResourceLimit)property.GetValue(defaults)!;
            Assert.Equal(expected[index], defaultLimit.Value);
            foreach (var limit in new[] { ResourceLimit.Finite(1), ResourceLimit.Finite(long.MaxValue), ResourceLimit.Unlimited })
            {
                var arguments = new object?[parameters.Length];
                arguments[index] = limit;
                Assert.Equal(limit, property.GetValue(constructor.Invoke(arguments)));
            }
            var invalid = new object?[parameters.Length];
            invalid[index] = default(ResourceLimit);
            Assert.IsType<ArgumentOutOfRangeException>(Assert.Throws<TargetInvocationException>(() => constructor.Invoke(invalid)).InnerException);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParseAndCompile_PreCanceled_PrioritizeCancellationOverSourceLimit(bool member)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var source = new string('A', 257);
        var engine = new AspectNEngine();
        Assert.Throws<OperationCanceledException>(() => member
            ? AspectNParser.ParseMemberExpression(source, cancellationToken: cancellation.Token)
            : AspectNParser.ParseTypePattern(source, cancellationToken: cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => member
            ? engine.CompileMemberExpression(source, cancellationToken: cancellation.Token)
            : engine.CompileTypePattern(source, cancellationToken: cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => AspectNCompilation.Create(
            AspectNParser.ParseTypePattern("*"), cancellationToken: cancellation.Token));
    }

    [Fact]
    public void Limits_InvalidFiniteValuesAndDefaultAccess_Reject()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceLimit.Finite(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceLimit.Finite(-1));
        Assert.Throws<InvalidOperationException>(() => default(ResourceLimit).Value);
        Assert.Throws<InvalidOperationException>(() => ResourceLimit.Unlimited.Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeLimit.Finite(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeLimit.Finite(TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeLimit.Finite(TimeSpan.FromMilliseconds(2_147_483_647)));
        Assert.Equal(TimeSpan.FromTicks(1), TimeLimit.Finite(TimeSpan.FromTicks(1)).Value);
        Assert.Equal(TimeSpan.FromMilliseconds(2_147_483_646), TimeLimit.Finite(TimeSpan.FromMilliseconds(2_147_483_646)).Value);
        Assert.Throws<InvalidOperationException>(() => default(TimeLimit).Value);
        Assert.Throws<InvalidOperationException>(() => TimeLimit.Unlimited.Value);
    }

    [Fact]
    public void Compile_CacheKey_ContainsEveryParseAndMatchLimitByValue()
    {
        var engine = new AspectNEngine();
        var baseline = engine.CompileTypePattern("C");
        foreach (var type in new[] { typeof(AspectNParseOptions), typeof(AspectNMatchOptions) })
        {
            var constructor = type.GetConstructors().Single();
            var parameters = constructor.GetParameters();
            var defaults = constructor.Invoke(new object?[parameters.Length]);
            for (var index = 0; index < parameters.Length; index++)
            {
                var property = type.GetProperty(char.ToUpperInvariant(parameters[index].Name![0]) + parameters[index].Name![1..])!;
                var arguments = new object?[parameters.Length];
                arguments[index] = parameters[index].ParameterType == typeof(ResourceLimit?)
                    ? ResourceLimit.Finite(((ResourceLimit)property.GetValue(defaults)!).Value + 1)
                    : TimeLimit.Finite(TimeSpan.FromMilliseconds(251));
                var first = Compile(constructor.Invoke(arguments));
                var equal = Compile(constructor.Invoke(arguments));
                Assert.NotSame(baseline, first);
                Assert.Same(first, equal);
                arguments[index] = parameters[index].ParameterType == typeof(ResourceLimit?) ? ResourceLimit.Unlimited : TimeLimit.Unlimited;
                Assert.NotSame(first, Compile(constructor.Invoke(arguments)));
            }
        }
        AspectNCompilation Compile(object options) => options is AspectNParseOptions parse
            ? engine.CompileTypePattern("C", parse)
            : engine.CompileTypePattern("C", compilationOptions: new AspectNCompilationOptions(matchOptions: (AspectNMatchOptions)options));
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    public void Parse_SyntaxNodes_CountsTokenEofAndRoot(int nodes, bool limited)
    {
        var tree = AspectNParser.ParseTypePattern("*", new AspectNParseOptions(maximumSyntaxNodes: ResourceLimit.Finite(nodes)));
        Assert.Equal(limited, tree.Diagnostics.Any(diagnostic => diagnostic.Code == "AN1003"));
    }

    [Theory]
    [InlineData("*", false)]
    [InlineData("*[]", true)]
    public void Parse_TokenBoundary_ExcludesEof(string source, bool limited)
    {
        var tree = AspectNParser.ParseTypePattern(source, new AspectNParseOptions(maximumTokens: ResourceLimit.Finite(1)));
        Assert.Equal(limited, tree.Diagnostics.Any(diagnostic => diagnostic.Code == "AN1003"));
    }

    [Fact]
    public void Parse_SourceBoundary_IncludesWhitespaceEscapesAndSurrogatePairs()
    {
        var source = "*" + new string(' ', 253) + "\r\n";
        Assert.Equal(256, source.Length);
        Assert.DoesNotContain(AspectNParser.ParseTypePattern(source).Diagnostics, diagnostic => diagnostic.Code == "AN1003");
        Assert.Contains(AspectNParser.ParseTypePattern(source + " ").Diagnostics, diagnostic => diagnostic.Code == "AN1003");
        Assert.Equal(2, "😀".Length);
        const string regex = "regex(\\w+)";
        Assert.Equal(10, regex.Length);
        Assert.Contains(AspectNParser.ParseMemberExpression(regex, new AspectNParseOptions(maximumSourceLength: ResourceLimit.Finite(9))).Diagnostics,
            diagnostic => diagnostic.Code == "AN1003");
    }

    [Theory]
    [InlineData("*", 1, false)]
    [InlineData("(*)", 1, true)]
    [InlineData("(*)", 2, false)]
    public void Parse_ParserDepth_CountsLogicalFrames(string source, int limit, bool limited)
    {
        var tree = AspectNParser.ParseTypePattern(source, new AspectNParseOptions(maximumParserDepth: ResourceLimit.Finite(limit)));
        Assert.Equal(limited, tree.Diagnostics.Any(diagnostic => diagnostic.Code == "AN1002"));
    }

    [Theory]
    [InlineData(false, 2, false)]
    [InlineData(true, 2, true)]
    [InlineData(true, 3, false)]
    public void Parse_SyntaxDepth_CountsRootAndTokens(bool array, int depth, bool limited)
    {
        var tree = AspectNParser.ParseTypePattern(array ? "*[]" : "*",
            new AspectNParseOptions(maximumSyntaxDepth: ResourceLimit.Finite(depth)));
        Assert.Equal(limited, tree.Diagnostics.Any(diagnostic => diagnostic.Code == "AN1002"));
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(1, true)]
    public void Compile_MethodSlots_UseIndependentBindingLimit(int limit, bool limited)
    {
        var compilation = new AspectNEngine().CompileMemberExpression("method(* M<,>(..))",
            new AspectNParseOptions(maximumMethodGenericParameters: ResourceLimit.Finite(limit),
                maximumTypeGenericParameters: ResourceLimit.Finite(1)));
        Assert.Equal(limited, compilation.Diagnostics.Any(diagnostic => diagnostic.Code == "AN2011"));
    }
}
