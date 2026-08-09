using System.Runtime.CompilerServices;
using AspectN.Metadata;

namespace AspectN.Reflection.Internal;

internal sealed class ReflectionTypeCache
{
    private readonly ConditionalWeakTable<Type, TypeSignature> _forward = new();
    private readonly ConditionalWeakTable<TypeSignature, WeakReference<Type>>
        _reverse = new();

    public TypeSignature GetOrAdd(
        Type type,
        Func<TypeSignature> valueFactory)
    {
        var signature = _forward.GetValue(type, _ => valueFactory());
        _reverse.GetValue(
            signature,
            _ => new WeakReference<Type>(type));
        return signature;
    }

    public bool TryGetType(TypeSignature signature, out Type? type)
    {
        type = null;
        return _reverse.TryGetValue(signature, out var reference) &&
               reference.TryGetTarget(out type);
    }
}
