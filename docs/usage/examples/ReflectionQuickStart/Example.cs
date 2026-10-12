namespace Example;

public sealed class Service
{
    public static string GetName() => "AspectN";
    public static int Run() => 42;
    public static void TryRun(out int value) => value = 42;
}

public sealed class Owner<T>
{
    public TValue Echo<TValue>(TValue value) => value;
}
