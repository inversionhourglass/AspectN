using System.Runtime.CompilerServices;
using AspectN.Metadata;

namespace AspectN.Reflection.Internal;

internal sealed class ReflectionTypeCache
{
    private readonly ConditionalWeakTable<Type, TypeSignature> _forward = new();
    private readonly ConditionalWeakTable<TypeSignature, NativeAssociation> _reverse;
    private readonly WeakReference<Type>? _context;

    public ReflectionTypeCache()
        : this(new ConditionalWeakTable<TypeSignature, NativeAssociation>(), null)
    {
    }

    private ReflectionTypeCache(ConditionalWeakTable<TypeSignature, NativeAssociation> reverse, Type? context)
    {
        _reverse = reverse;
        _context = context is null ? null : new WeakReference<Type>(context);
    }

    public ReflectionTypeCache CreateContextCache(Type context) => new(_reverse, context);

    public TypeSignature GetOrAdd(
        Type type,
        Func<TypeSignature> valueFactory)
    {
        var signature = _forward.GetValue(type, _ => valueFactory());
        _reverse.GetValue(
            signature,
            _ => new NativeAssociation(type, _context));
        return signature;
    }

    public TypeSignature? Find(Type type) => _forward.TryGetValue(type, out var signature) ? signature : null;

    public bool TryGetType(TypeSignature signature, out Type? type)
        => TryGetType(signature, out type, out _);

    public bool TryGetType(TypeSignature signature, out Type? type, out Type? context)
    {
        type = null;
        context = null;
        return _reverse.TryGetValue(signature, out var reference) && reference.Type.TryGetTarget(out type) &&
            (reference.Context is null || reference.Context.TryGetTarget(out context));
    }

    // 中立签名可以比原生声明活得更久；查询上下文也必须保持弱引用。
    private sealed class NativeAssociation(Type type, WeakReference<Type>? context)
    {
        public WeakReference<Type> Type { get; } = new(type);
        public WeakReference<Type>? Context { get; } = context;
    }
}
