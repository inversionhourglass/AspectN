using AspectN.Execution;

namespace AspectN.Tests;

public sealed class WorkStackTests
{
    [Fact]
    public void Run_TwentyThousandDeferredFrames_OnSmallStack()
    {
        var result = 0;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = WorkStack.Run(Count(20_000)); }
            catch (Exception exception) { failure = exception; }
        }, 128 * 1024);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(failure);
        Assert.Equal(20_000, result);
    }

    private static async StackWork<int> Count(int depth)
    {
        if (depth == 0) return 0;
        return 1 + await Count(depth - 1);
    }

    [Fact]
    public void WorkStack_DeferredFrames_HasAnInternalScheduler()
    {
        var scheduler = typeof(AspectNEngine).Assembly.GetType("AspectN.Execution.WorkStack");
        Assert.NotNull(scheduler);
        Assert.False(scheduler.IsPublic);
    }
}
