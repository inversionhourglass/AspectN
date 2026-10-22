using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace AspectN.Execution;

// 编译器生成的状态机保存局部变量；显式栈调度子帧及续体，禁止同步内联子调用。
// 调度器由根操作持有，不依赖线程本地、全局状态或线程池，也不引入异步并发。
internal sealed class WorkStack
{
    private readonly Stack<Action> _pending = new();

    public static T Run<T>(StackWork<T> work, CancellationToken cancellationToken = default)
    {
        var stack = new WorkStack();
        work.Promise.Schedule(stack, null);
        while (stack._pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            stack._pending.Pop()();
        }
        cancellationToken.ThrowIfCancellationRequested();
        return work.Promise.GetResult();
    }

    internal void Push(Action continuation) => _pending.Push(continuation);
}

[AsyncMethodBuilder(typeof(StackWorkBuilder<>))]
internal readonly struct StackWork<T>(WorkPromise<T> promise)
{
    internal WorkPromise<T> Promise { get; } = promise;
    public StackWorkAwaiter<T> GetAwaiter() => new(Promise);
}

internal interface IStackWorkAwaiter
{
    void Schedule(WorkStack stack, Action continuation);
}

internal readonly struct StackWorkAwaiter<T>(WorkPromise<T> promise) : ICriticalNotifyCompletion, IStackWorkAwaiter
{
    // 即使结果已完成也通过显式栈恢复父帧，避免深链的同步续体累积调用栈。
    public bool IsCompleted => false;
    public T GetResult() => promise.GetResult();
    public void Schedule(WorkStack stack, Action continuation) => promise.Schedule(stack, continuation);
    public void OnCompleted(Action continuation) => throw new InvalidOperationException("A work stack is required.");
    public void UnsafeOnCompleted(Action continuation) => OnCompleted(continuation);
}

internal struct StackWorkBuilder<T>
{
    private WorkPromise<T> _promise;
    public static StackWorkBuilder<T> Create() => new() { _promise = new WorkPromise<T>() };
    public StackWork<T> Task => new(_promise);
    public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine =>
        _promise.Start = stateMachine.MoveNext;
    // 自定义方法生成器协议要求实例成员；状态机由 Start 的委托副本持有。
#pragma warning disable CA1822
    public void SetStateMachine(IAsyncStateMachine stateMachine) { }
#pragma warning restore CA1822
    public void SetResult(T result) => _promise.Complete(result, null);
    public void SetException(Exception exception) => _promise.Complete(default!, exception);

    public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion where TStateMachine : IAsyncStateMachine =>
        ((IStackWorkAwaiter)awaiter).Schedule(_promise.Stack!, stateMachine.MoveNext);

    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion where TStateMachine : IAsyncStateMachine =>
        AwaitOnCompleted(ref awaiter, ref stateMachine);
}

internal sealed class WorkPromise<T>
{
    private Action? _continuation;
    private bool _completed;
    private T _result = default!;
    private ExceptionDispatchInfo? _exception;
    public WorkStack? Stack { get; private set; }
    public Action Start { private get; set; } = null!;

    public void Schedule(WorkStack stack, Action? continuation)
    {
        if (_completed)
        {
            if (continuation is not null) stack.Push(continuation);
            return;
        }
        if (Stack is not null) throw new InvalidOperationException("A work frame can only be scheduled once.");
        Stack = stack;
        _continuation = continuation;
        stack.Push(Start);
    }

    public void Complete(T result, Exception? exception)
    {
        _result = result;
        _exception = exception is null ? null : ExceptionDispatchInfo.Capture(exception);
        _completed = true;
        if (_continuation is not null) Stack!.Push(_continuation);
    }

    public T GetResult()
    {
        if (!_completed) throw new InvalidOperationException("The work frame has not completed.");
        _exception?.Throw();
        return _result;
    }
}
