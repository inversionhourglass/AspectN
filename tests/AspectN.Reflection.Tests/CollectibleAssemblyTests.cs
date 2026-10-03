using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using AspectN.Evaluation;
using Xunit;

namespace AspectN.Reflection.Tests;

public sealed class CollectibleAssemblyTests
{
    [Fact]
    public void MapAndMatch_CollectibleAssembly_DoesNotPreventUnload()
    {
        var assemblyPath = Path.Combine(
            AppContext.BaseDirectory,
            "AspectN.TestAssembly.dll");
        Assert.True(File.Exists(assemblyPath));
        var typeSystem = new ReflectionTypeSystem();

        var contextReference = LoadMapMatchAndUnload(
            assemblyPath,
            typeSystem);

        for (var attempt = 0;
             attempt < 10 && contextReference.IsAlive;
             attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.False(contextReference.IsAlive);
        GC.KeepAlive(typeSystem);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference LoadMapMatchAndUnload(
        string assemblyPath,
        ReflectionTypeSystem typeSystem)
    {
        var loadContext = new AssemblyLoadContext(
            $"AspectN.Reflection.Tests.{Guid.NewGuid():N}",
            isCollectible: true);
        var contextReference = new WeakReference(loadContext);
        var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
        var type = Assert.IsAssignableFrom<Type>(
            assembly.GetType(
                "AspectN.TestAssembly.MatchingFixture`2",
                throwOnError: true));
        var constructedType = type.MakeGenericType(
            typeof(string),
            typeof(int));
        var method = Assert.IsAssignableFrom<MethodInfo>(
            constructedType.GetMethod("GenericMethod"));
        var constructedMethod = method.MakeGenericMethod(
            typeof(double),
            typeof(bool));
        var candidate = typeSystem.Mapper.MapMethod(constructedMethod);
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(
                "method(bool AspectN.TestAssembly.MatchingFixture" +
                "<string,int>.GenericMethod<double,bool>" +
                "(string,int,double,bool))"));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));

        var result = pattern!.Match(candidate, typeSystem);

        Assert.Equal(MatchStatus.Matched, result.Status);
        loadContext.Unload();
        return contextReference;
    }
}
