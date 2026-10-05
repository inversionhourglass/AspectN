using System.Runtime.CompilerServices;

namespace AspectN.Metadata;

internal sealed class SignatureReferenceComparer : IEqualityComparer<TypeSignature>
{
    internal static SignatureReferenceComparer Instance { get; } = new();
    public bool Equals(TypeSignature? x, TypeSignature? y) => ReferenceEquals(x, y);
    public int GetHashCode(TypeSignature obj) => RuntimeHelpers.GetHashCode(obj);
}
