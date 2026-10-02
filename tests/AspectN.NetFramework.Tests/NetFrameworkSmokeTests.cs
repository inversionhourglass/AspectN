using AspectN.Evaluation;
using Xunit;

namespace AspectN.NetFramework.Tests;

public sealed class NetFrameworkSmokeTests
{
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
    public static string Echo(string value)
    {
        return value;
    }
}
