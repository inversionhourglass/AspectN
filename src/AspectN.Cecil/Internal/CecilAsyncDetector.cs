using Mono.Cecil;

namespace AspectN.Cecil.Internal;

internal static class CecilAsyncDetector
{
    private const string ASYNC_STATE_MACHINE_ATTRIBUTE =
        "System.Runtime.CompilerServices.AsyncStateMachineAttribute";

    public static bool IsAsync(MethodDefinition? method)
    {
        return method is not null && method.CustomAttributes.Any(
            attribute => attribute.AttributeType.FullName ==
                         ASYNC_STATE_MACHINE_ATTRIBUTE);
    }
}
