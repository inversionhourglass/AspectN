using System.Runtime.InteropServices;

namespace AspectN.Cecil.Tests.Fixtures;

internal static class InteropRefKindFixture
{
    public static void InFlag([In] ref int value)
    {
        _ = value;
    }

    public static void InOutFlag([In, Out] ref int value)
    {
        _ = value;
    }
}
