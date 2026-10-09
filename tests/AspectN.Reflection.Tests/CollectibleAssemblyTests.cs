using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using Xunit;

namespace AspectN.Reflection.Tests;

public sealed class CollectibleAssemblyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MapAndMatch_CollectibleAssembly_DoesNotPreventUnload(bool openMethod)
    {
        var assemblyPath = Path.Combine(
            AppContext.BaseDirectory,
            "AspectN.TestAssembly.dll");
        Assert.True(File.Exists(assemblyPath));
        var typeSystem = new ReflectionTypeSystem();

        var contextReference = LoadMapMatchAndUnload(
            assemblyPath,
            typeSystem,
            openMethod,
            out var signature);

        for (var attempt = 0;
             attempt < 10 && contextReference.IsAlive;
             attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.False(contextReference.IsAlive);
        if (openMethod)
        {
            var relation = typeSystem.IsAssignableTo(signature.ReturnType.Type, typeSystem.Mapper.MapType(typeof(object)));
            Assert.Equal(MatchStatus.Unknown, relation.Status);
            Assert.Contains(relation.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.MissingBackendCapability);
        }

        GC.KeepAlive(signature);
        GC.KeepAlive(typeSystem);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference LoadMapMatchAndUnload(
        string assemblyPath,
        ReflectionTypeSystem typeSystem,
        bool openMethod,
        out MethodSignature signature)
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
        var nativeMethod = openMethod ? method : method.MakeGenericMethod(typeof(double), typeof(bool));
        var candidate = typeSystem.Mapper.MapMethod(nativeMethod);
        signature = candidate;
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(
                openMethod ? "method(* AspectN.TestAssembly.MatchingFixture<string,int>.GenericMethod<,>(..))" :
                "method(bool AspectN.TestAssembly.MatchingFixture" +
                "<string,int>.GenericMethod<double,bool>" +
                "(string,int,double,bool))"));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));

        var result = pattern!.Match(candidate, typeSystem);

        Assert.Equal(MatchStatus.Matched, result.Status);
        if (openMethod)
            Assert.Equal(MatchStatus.Matched,
                typeSystem.IsAssignableTo(candidate.ReturnType.Type, typeSystem.Mapper.MapType(typeof(object))).Status);
        loadContext.Unload();
        return contextReference;
    }
}
