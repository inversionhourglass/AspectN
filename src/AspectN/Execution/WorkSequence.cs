namespace AspectN.Execution;

internal static class WorkSequence
{
    // 顺序投影保持变量作用域和诊断的原始访问顺序，不并发运行子帧。
    public static async StackWork<TResult[]> Select<TSource, TResult>(IEnumerable<TSource> source,
        Func<TSource, StackWork<TResult>> selector)
    {
        var result = new List<TResult>();
        foreach (var item in source) result.Add(await selector(item));
        return result.ToArray();
    }

    public static async StackWork<TResult[]> Select<TSource, TResult>(IEnumerable<TSource> source,
        Func<TSource, int, StackWork<TResult>> selector)
    {
        var result = new List<TResult>();
        var index = 0;
        foreach (var item in source) result.Add(await selector(item, index++));
        return result.ToArray();
    }
}
