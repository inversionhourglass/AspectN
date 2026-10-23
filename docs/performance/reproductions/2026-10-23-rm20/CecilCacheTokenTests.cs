using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace AspectN.Cecil.Tests;

public sealed class CecilCacheTokenTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(32)]
    public void Append_SmallIntegerToken_DoesNotAllocate(int value)
    {
        var append = CreateAppender<int>();
        var builder = new StringBuilder(2048);
        for (var index = 0; index < 32; index++) { builder.Clear(); append(builder, value); }
        builder.Clear();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 256; index++) append(builder, value);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(string.Concat(Enumerable.Repeat(value.ToString(CultureInfo.InvariantCulture), 256)), builder.ToString());
        Assert.Equal(0L, allocated);
    }

    [Theory]
    [InlineData(true, "True")]
    [InlineData(false, "False")]
    public void Append_BooleanToken_DoesNotAllocate(bool value, string expected)
    {
        var append = CreateAppender<bool>();
        var builder = new StringBuilder(2048);
        for (var index = 0; index < 32; index++) { builder.Clear(); append(builder, value); }
        builder.Clear();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 256; index++) append(builder, value);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(string.Concat(Enumerable.Repeat(expected, 256)), builder.ToString());
        Assert.Equal(0L, allocated);
    }

    [Theory]
    [InlineData(true, 3, false)]
    [InlineData(true, 4, true)]
    [InlineData(false, 4, false)]
    [InlineData(false, 5, true)]
    public void Append_BooleanToken_EnforcesTextBudget(bool value, int limit, bool succeeds)
    {
        var append = CreateAppender<bool>(new AspectNMappingOptions(maximumGeneratedTextLength: ResourceLimit.Finite(limit)));
        var builder = new StringBuilder();
        if (succeeds) { append(builder, value); Assert.Equal(value.ToString(), builder.ToString()); return; }

        var exception = Assert.Throws<AspectNLimitException>(() => append(builder, value));
        Assert.Contains("MaximumGeneratedTextLength", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, builder.Length);
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(int.MinValue, "-2147483648")]
    [InlineData(int.MaxValue, "2147483647")]
    public void Append_IntegerToken_PreservesInvariantText(int value, string expected)
    {
        var append = CreateAppender<int>();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NegativeSign = "~";
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = culture;
            var builder = new StringBuilder();
            append(builder, value);
            Assert.Equal(expected, builder.ToString());
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    private static Action<StringBuilder, T> CreateAppender<T>(AspectNMappingOptions? options = null)
    {
        // 反射定位仅发生在计量外；生成委托直接调用真实写入器，不把Invoke或反射装箱计入分配。
        var keyType = typeof(CecilTypeSystem).Assembly.GetType("AspectN.Cecil.Internal.CecilTypeCacheKey", throwOnError: true)!;
        var contextType = typeof(AspectNEngine).Assembly.GetType("AspectN.TypeSystem.MappingContext", throwOnError: true)!;
        var context = Activator.CreateInstance(contextType, options ?? new AspectNMappingOptions(), default(CancellationToken), null)!;
        const BindingFlags FLAGS = BindingFlags.NonPublic | BindingFlags.Static;
        var method = keyType.GetMethod("Append", FLAGS, null, [typeof(StringBuilder), typeof(T), contextType], null)
            ?? keyType.GetMethod("Append", FLAGS, null, [typeof(StringBuilder), typeof(object), contextType], null);
        Assert.NotNull(method);
        Assert.Equal(typeof(void), method.ReturnType);
        var builder = Expression.Parameter(typeof(StringBuilder));
        var value = Expression.Parameter(typeof(T));
        var call = Expression.Call(method, builder, Expression.Convert(value, method.GetParameters()[1].ParameterType),
            Expression.Constant(context, contextType));
        return Expression.Lambda<Action<StringBuilder, T>>(call, builder, value).Compile();
    }
}
