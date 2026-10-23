namespace AspectN.Benchmarks.Fixtures;

internal sealed class OrderService : BaseService, IOrderContract
{
    public static int Run(int value) => value;
}
