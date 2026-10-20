using AspectN.AdapterInternals;

namespace AspectN.Reflection.Internal;

// 只解码签名中的修饰符及其结构位置；类型显示、调用约定和声明类型仍由现有映射器负责。
internal sealed class MethodCustomModifierReader(byte[] signature, Func<int, string> resolveType)
{
    private int _position;

    public static IReadOnlyList<string>[] Read(byte[] signature, Func<int, string> resolveType)
    {
        var reader = new MethodCustomModifierReader(signature, resolveType);
        var count = reader.ReadHeader();
        var result = new IReadOnlyList<string>[count + 1];
        for (var index = 0; index <= count; index++)
        {
            var entries = new List<string>();
            if (reader.Peek() == 0x41) reader.ReadByte(); // vararg sentinel 不属于参数形状。
            reader.ReadType(string.Empty, entries, 1);
            result[index] = entries;
        }
        if (reader._position != signature.Length) throw new BadImageFormatException("Unexpected method signature suffix.");
        return result;
    }

    private int ReadHeader()
    {
        var header = ReadByte();
        if ((header & 0x10) != 0) ReadCompressed(); // 泛型方法元数。
        var count = ReadCompressed();
        if (count >= signature.Length - _position) throw new BadImageFormatException("Invalid method parameter count.");
        return count;
    }

    private void ReadType(string path, List<string> entries, int depth)
    {
        NativeTypeLimitGuard.CheckDepth(depth);
        while (Peek() is 0x1f or 0x20)
        {
            var optional = ReadByte() == 0x20;
            var encoded = ReadCompressed();
            var token = (encoded & 3) switch
            {
                0 => 0x02000000 | (encoded >> 2),
                1 => 0x01000000 | (encoded >> 2),
                2 => 0x1b000000 | (encoded >> 2),
                _ => throw new BadImageFormatException("Invalid custom modifier token.")
            };
            entries.Add(GenericOwnerIdentity.CustomModifier(path, optional, resolveType(token)));
        }
        switch (ReadByte())
        {
            case 0x01 or 0x02 or 0x03 or 0x04 or 0x05 or 0x06 or 0x07 or 0x08 or 0x09 or
                0x0a or 0x0b or 0x0c or 0x0d or 0x0e or 0x16 or 0x18 or 0x19 or 0x1c:
                return;
            case 0x11 or 0x12 or 0x13 or 0x1e:
                ReadCompressed();
                return;
            case 0x0f:
                ReadType(path + "/pointer", entries, depth + 1);
                return;
            case 0x10:
                ReadType(path + "/ref", entries, depth + 1);
                return;
            case 0x1d:
                ReadType(path + "/element", entries, depth + 1);
                return;
            case 0x14:
                ReadType(path + "/element", entries, depth + 1);
                ReadCompressed(); // rank。
                var sizes = ReadCompressed();
                for (var index = 0; index < sizes; index++) ReadCompressed();
                var bounds = ReadCompressed();
                for (var index = 0; index < bounds; index++) ReadCompressed();
                return;
            case 0x15:
                if (ReadByte() is not (0x11 or 0x12)) throw new BadImageFormatException("Invalid generic type signature.");
                ReadCompressed();
                var arguments = ReadCompressed();
                for (var index = 0; index < arguments; index++) ReadType(path + "/arg:" + index, entries, depth + 1);
                return;
            case 0x1b:
                var parameters = ReadHeader();
                ReadType(path + "/return", entries, depth + 1);
                for (var index = 0; index < parameters; index++)
                {
                    if (Peek() == 0x41) ReadByte();
                    ReadType(path + "/parameter:" + index, entries, depth + 1);
                }
                return;
            default:
                throw new BadImageFormatException("Unsupported method signature element.");
        }
    }

    private int ReadCompressed()
    {
        var first = ReadByte();
        if ((first & 0x80) == 0) return first;
        if ((first & 0xc0) == 0x80) return ((first & 0x3f) << 8) | ReadByte();
        if ((first & 0xe0) == 0xc0)
            return ((first & 0x1f) << 24) | (ReadByte() << 16) | (ReadByte() << 8) | ReadByte();
        throw new BadImageFormatException("Invalid compressed signature integer.");
    }

    private byte Peek() => _position < signature.Length ? signature[_position] : throw new BadImageFormatException("Truncated method signature.");

    private byte ReadByte()
    {
        var value = Peek();
        _position++;
        return value;
    }
}
