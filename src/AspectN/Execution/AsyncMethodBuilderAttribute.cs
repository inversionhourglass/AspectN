#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices;

// netstandard2.0 引用程序集未声明此编译器协议特性；内部补充不扩大公共 API。
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum |
    AttributeTargets.Interface | AttributeTargets.Delegate | AttributeTargets.Method, Inherited = false)]
internal sealed class AsyncMethodBuilderAttribute(Type builderType) : Attribute
{
    public Type BuilderType { get; } = builderType;
}
#endif
