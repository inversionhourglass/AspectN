using System.Reflection;
using AspectN.Evaluation;
using AspectN.Metadata;
using Xunit;

namespace AspectN.Reflection.Tests;

public sealed unsafe class FunctionPointerMappingTests
{
    [Fact]
    public void MapType_FunctionPointer_ReturnsUnsupportedSignature()
    {
        var typeSystem = new ReflectionTypeSystem();

        var signature = typeSystem.Mapper.MapType(typeof(delegate*<string, int>));

        // 与 Roslyn 的 IFunctionPointerTypeSymbol 分支及指针分支对齐：
        // 函数指针不得落入普通 Named 构造（其 Name 为空串，会得到空名签名）。
        var unsupported = Assert.IsType<UnsupportedTypeSignature>(signature);
        Assert.Contains(
            "function pointer",
            unsupported.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AreEqual_TwoDistinctFunctionPointers_DoesNotMatch()
    {
        var typeSystem = new ReflectionTypeSystem();
        var first = typeSystem.Mapper.MapType(typeof(delegate*<string, int>));
        var second = typeSystem.Mapper.MapType(typeof(delegate*<int, void>));

        // 结构恒等对不支持的形状不可用：必须 Unknown，绝不能误报 Matched。
        Assert.Equal(
            MatchStatus.Unknown,
            typeSystem.AreEqual(first, second).Status);
        Assert.Equal(
            MatchStatus.Unknown,
            typeSystem.AreEqual(first, first).Status);
    }

    [Fact]
    public void MapCallable_FunctionPointerParameterAndReturn_MapAsUnsupported()
    {
        var typeSystem = new ReflectionTypeSystem();
        var callable = typeSystem.Mapper.MapCallable(
            typeof(FunctionPointerFixture).GetMethod(
                nameof(FunctionPointerFixture.Echo))!);

        Assert.IsType<UnsupportedTypeSignature>(callable.ReturnType.Type);
        Assert.Equal(2, callable.Parameters.Count);
        Assert.IsType<UnsupportedTypeSignature>(callable.Parameters[0].Type.Type);
        Assert.IsType<UnsupportedTypeSignature>(callable.Parameters[1].Type.Type);
    }

    [Fact]
    public void MapType_FunctionPointerParameterOverloads_HaveDistinctOwnerIds()
    {
        var typeSystem = new ReflectionTypeSystem();
        var overloads = typeof(FunctionPointerOwnerFixture).GetMethods(
            BindingFlags.Public | BindingFlags.Static);
        var stringOverload = Assert.Single(
            overloads,
            method =>
                method.GetParameters()[0].ParameterType ==
                    typeof(delegate*<string, int>));
        var intOverload = Assert.Single(
            overloads,
            method =>
                method.GetParameters()[0].ParameterType ==
                    typeof(delegate*<int, void>));
        var first = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(stringOverload.GetGenericArguments()[0]));
        var second = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(intOverload.GetGenericArguments()[0]));

        // 函数指针形状必须进入 owner ID：不同函数指针重载的泛型参数不得判等，
        // 也不得退化为与无参方法相同的空括号形式。
        Assert.Equal(
            "AspectN.Reflection.Tests.FunctionPointerOwnerFixture.M`1(" +
                "delegate*<System.String,System.Int32>)",
            first.Parameter.OwnerId);
        Assert.Equal(
            "AspectN.Reflection.Tests.FunctionPointerOwnerFixture.M`1(" +
                "delegate*<System.Int32,System.Void>)",
            second.Parameter.OwnerId);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.AreEqual(first, second).Status);
    }

    [Fact]
    public void MapType_FunctionPointerPointerOverload_ComposesPointerSuffix()
    {
        var typeSystem = new ReflectionTypeSystem();
        var pointerOverload = Assert.Single(
            typeof(FunctionPointerOwnerFixture).GetMethods(
                BindingFlags.Public | BindingFlags.Static),
            method =>
                method.GetParameters()[0].ParameterType ==
                    typeof(delegate*<int>*));
        var mapped = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(
                pointerOverload.GetGenericArguments()[0]));

        // 函数指针的指针经 * 后缀递归组合：元素先归约为函数指针形式
        //（delegate*<int> 是无参、返回 int 的函数指针，末位类型为返回类型）。
        Assert.Equal(
            "AspectN.Reflection.Tests.FunctionPointerOwnerFixture.M`1(" +
                "delegate*<System.Int32>*)",
            mapped.Parameter.OwnerId);
    }
}

internal static unsafe class FunctionPointerFixture
{
    public static delegate*<string, int> Echo(
        delegate*<string, int> value,
        delegate*<int, void> other)
    {
        _ = other;
        return value;
    }
}

internal static unsafe class FunctionPointerOwnerFixture
{
    public static void M<T>(delegate*<string, int> value)
    {
        _ = value;
    }

    public static void M<T>(delegate*<int, void> value)
    {
        _ = value;
    }

    public static void M<T>(delegate*<int>* value)
    {
        _ = value;
    }
}

