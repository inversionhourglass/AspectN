using AspectN.Evaluation;
using Xunit;

namespace AspectN.NetFramework.Tests;

public sealed class NetFrameworkSmokeTests
{
    [Fact]
    public void Match_GenericVariableModesAndBudget_ExecuteOnCompatibilityAsset()
    {
        var system = new Reflection.ReflectionTypeSystem();
        var method = typeof(NetFrameworkFixture).GetMethod(nameof(NetFrameworkFixture.Identity))!;
        var engine = new AspectNEngine();
        foreach (var mode in new[] { GenericVariableMode.Declaration, GenericVariableMode.Capture })
        {
            Assert.True(engine.CompileMemberExpression("method($T NetFrameworkFixture.Identity<$T>($T))",
                new AspectNCompilationOptions(null, mode)).TryGetMemberPattern(out var pattern));
            Assert.Equal(MatchStatus.Matched, pattern!.Match(system.Mapper.MapMethod(method), system).Status);
            Assert.Equal(mode == GenericVariableMode.Declaration ? MatchStatus.NotMatched : MatchStatus.Matched,
                pattern.Match(system.Mapper.MapMethod(method.MakeGenericMethod(typeof(int))), system).Status);
        }
        Assert.True(engine.CompileMemberExpression("method(* Identity<$L>(..)) || method(* Identity<$R>(..))",
            new AspectNCompilationOptions(null, GenericVariableMode.Capture, 1)).TryGetMemberPattern(out var limited));
        var result = limited!.Match(system.Mapper.MapMethod(method), system);
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, item => item.Code == "AN3007");
    }

    [Fact]
    public void CompileAndMatch_ReflectionMethod_WorksOnNet472()
    {
        var compilation = new AspectNEngine().CompileMemberExpression(
            "method(public string " +
            "AspectN.NetFramework.Tests.NetFrameworkFixture.Echo(string))");
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        var method = Assert.IsAssignableFrom<System.Reflection.MethodInfo>(
            typeof(NetFrameworkFixture).GetMethod(
                nameof(NetFrameworkFixture.Echo)));
        var typeSystem = new Reflection.ReflectionTypeSystem();
        var candidate = typeSystem.Mapper.MapMethod(method);

        var result = pattern!.Match(candidate, typeSystem);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void AdapterAssemblies_PublicEntryPoints_AreConsumableOnNet472()
    {
        Assert.True(typeof(Cecil.CecilTypeSystem).IsPublic);
        Assert.True(typeof(Roslyn.RoslynTypeSystem).IsPublic);
        Assert.True(typeof(Reflection.ReflectionTypeSystem).IsPublic);
    }
}

public sealed class NetFrameworkFixture
{
    public static T Identity<T>(T value) => value;

    public static string Echo(string value)
    {
        return value;
    }
}
