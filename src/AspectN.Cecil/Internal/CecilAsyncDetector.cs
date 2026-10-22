using Mono.Cecil;
using AspectN.TypeSystem;

namespace AspectN.Cecil.Internal;

internal static class CecilAsyncDetector
{
    private const string ASYNC_STATE_MACHINE_ATTRIBUTE =
        "System.Runtime.CompilerServices.AsyncStateMachineAttribute";
    private const string ASYNC_ITERATOR_STATE_MACHINE_ATTRIBUTE =
        "System.Runtime.CompilerServices.AsyncIteratorStateMachineAttribute";

    public static bool IsAsync(MethodDefinition? method, MappingContext resources)
    {
        return method is not null && resources.Attributes(method.CustomAttributes).Any(
            attribute => CecilNativeTypeMetadata.FullName(attribute.AttributeType, resources) is
                ASYNC_STATE_MACHINE_ATTRIBUTE or ASYNC_ITERATOR_STATE_MACHINE_ATTRIBUTE);
    }
}
