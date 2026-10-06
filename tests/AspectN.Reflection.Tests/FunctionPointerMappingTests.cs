using static AspectN.Tests.Shared.OwnerIdentityFixture;
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
    public void MapMethod_FunctionPointerParameterAndReturn_MapAsUnsupported()
    {
        var typeSystem = new ReflectionTypeSystem();
        var callable = typeSystem.Mapper.MapMethod(
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
        // 也不得退化为与无参方法相同的空括号形式；文本与 Cecil/Roslyn 逐字节一致。
        Assert.Equal(
            "AspectN.Reflection.Tests.FunctionPointerOwnerFixture.M`1(" +
                $"delegate*[managed]<{FrameworkType("System.String")},{FrameworkType("System.Int32")}>)",
            first.Parameter.OwnerId);
        Assert.Equal(
            "AspectN.Reflection.Tests.FunctionPointerOwnerFixture.M`1(" +
                $"delegate*[managed]<{FrameworkType("System.Int32")},{FrameworkType("System.Void")}>)",
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
        //（delegate*[managed]<System.Int32> 是无参、返回 int 的函数指针，末位类型为返回类型）。
        Assert.Equal(
            "AspectN.Reflection.Tests.FunctionPointerOwnerFixture.M`1(" +
                $"delegate*[managed]<{FrameworkType("System.Int32")}>*)",
            mapped.Parameter.OwnerId);
    }

    [Fact]
    public void MapType_TypeThrowingOnIsFunctionPointer_MapsWithoutCrash()
    {
        var typeSystem = new ReflectionTypeSystem();
        var hostile = new ThrowingFunctionPointerType();

        // MetadataLoadContext ≤ 7 的类型派生自未覆写 IsFunctionPointer 的
        // TypeDelegator，属性访问抛 NullReferenceException；探测失败必须按
        // 非函数指针回退，普通类型的映射不得因此崩溃。
        Assert.Throws<NullReferenceException>(
            () => hostile.IsFunctionPointer);
        var signature = typeSystem.Mapper.MapType(hostile);

        var named = Assert.IsType<NamedTypeSignature>(signature);
        Assert.Equal("System.Int32", named.Identity.FullMetadataName);
    }

    // 模拟 MLC ≤ 7 的 TypeDelegator 派生类型：IsFunctionPointer 访问即抛。
    private sealed class ThrowingFunctionPointerType : TypeDelegator
    {
        public ThrowingFunctionPointerType()
            : base(typeof(int))
        {
        }

        public override bool IsFunctionPointer =>
#pragma warning disable CA2201 // 复现 TypeDelegator 基类实现的真实崩溃形态
            throw new NullReferenceException();
#pragma warning restore CA2201
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
