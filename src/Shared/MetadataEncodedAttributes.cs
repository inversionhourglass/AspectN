using AspectN.Metadata;

namespace AspectN.AdapterInternals;

internal static class MetadataEncodedAttributes
{
    // 这些源码特性由编译器编码为元数据标志/表项；从 PE 读取的真实 CA 记录仍保留。
    public static bool Contains(string? name, AttributeTargetKind target) => target switch
    {
        AttributeTargetKind.Type => name is
            "System.SerializableAttribute" or
            "System.Runtime.InteropServices.ComImportAttribute" or
            "System.Runtime.InteropServices.StructLayoutAttribute",
        AttributeTargetKind.Method => name is
            "System.Runtime.InteropServices.DllImportAttribute" or
            "System.Runtime.InteropServices.PreserveSigAttribute" or
            "System.Runtime.CompilerServices.MethodImplAttribute",
        AttributeTargetKind.Parameter or AttributeTargetKind.Return => name is
            "System.Runtime.InteropServices.InAttribute" or
            "System.Runtime.InteropServices.OutAttribute" or
            "System.Runtime.InteropServices.OptionalAttribute" or
            "System.Runtime.InteropServices.MarshalAsAttribute" or
            "System.Runtime.InteropServices.DefaultParameterValueAttribute",
        _ => false
    };
}
