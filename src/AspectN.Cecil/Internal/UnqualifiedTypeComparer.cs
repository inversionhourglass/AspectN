using AspectN.AdapterInternals;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Cecil.Internal;

internal static class UnqualifiedTypeComparer
{
    public static TypeRelationResult AreEqual(TypeSignature actual, TypeSignature expected)
    {
        return UnqualifiedStructuralComparer.AreEqual(actual, expected);
    }
}
