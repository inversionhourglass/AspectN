using System.Reflection;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

// 依据 Mono.Cecil 0.11.5 的 Image/TableHeap/BlobHeap 布局构造局部延迟读取失败。
// Fody 会链接本文件并使用自己的 Cecil 身份；升级任一载体后均需重新验证布局，不能只比较版本号。
internal static class CecilDamagedMetadataFixture
{
    private const int METHOD_SEMANTICS_TABLE = 24;
    private const int TYPE_SPEC_TABLE = 27;
    private const string IMAGE_PATH = "ModuleDefinition.Image";
    private const string TABLE_HEAP_PATH = IMAGE_PATH + ".TableHeap";
    private const string BLOB_HEAP_PATH = IMAGE_PATH + ".BlobHeap";

    public static void DamagePropertySemantics(ModuleDefinition module)
    {
        var image = ReadField<object>(module, "Image", IMAGE_PATH);
        var heap = ReadField<object>(image, "TableHeap", TABLE_HEAP_PATH);
        var offset = ReadTableOffset(heap, METHOD_SEMANTICS_TABLE, 6);
        var data = ReadField<byte[]>(heap, "data", TABLE_HEAP_PATH + ".data");
        // 当前夹具只有一个 getter；先验证语义标记，再写入原测试使用的无效值。
        RequireBytes(data, offset, [0x02, 0x00], TABLE_HEAP_PATH + ".data (MethodSemantics)");
        data[(int)offset] = 0xFF;
        data[(int)offset + 1] = 0xFF;
    }

    public static void DamageInterfaceTypeSpecification(ModuleDefinition module)
    {
        var image = ReadField<object>(module, "Image", IMAGE_PATH);
        var heap = ReadField<object>(image, "TableHeap", TABLE_HEAP_PATH);
        var offset = ReadTableOffset(heap, TYPE_SPEC_TABLE, 2);
        var data = ReadField<byte[]>(heap, "data", TABLE_HEAP_PATH + ".data");
        RequireRange(data, offset, 2, TABLE_HEAP_PATH + ".data (TypeSpec)");
        var blobIndex = (uint)(data[(int)offset] | data[(int)offset + 1] << 8);
        var blobs = ReadField<byte[]>(ReadField<object>(image, "BlobHeap", BLOB_HEAP_PATH), "data", BLOB_HEAP_PATH + ".data");
        // 当前夹具为第二个 TypeDef 的 I<int>，blob 长度为五，且使用单字节长度前缀。
        RequireBytes(blobs, blobIndex, [0x05, 0x15, 0x12, 0x08, 0x01, 0x08], BLOB_HEAP_PATH + ".data (I<int>)");
        // 保留原损坏方式：ARRAY int32，秩为零，使接口集合的延迟读取失败。
        blobs[(int)blobIndex + 1] = 0x14;
        blobs[(int)blobIndex + 2] = 0x08;
        blobs[(int)blobIndex + 3] = 0;
        blobs[(int)blobIndex + 4] = 0;
        blobs[(int)blobIndex + 5] = 0;
    }

    private static uint ReadTableOffset(object heap, int index, uint rowSize)
    {
        var path = $"{TABLE_HEAP_PATH}.Tables[{index}]";
        var tables = ReadField<Array>(heap, "Tables", TABLE_HEAP_PATH + ".Tables");
        Require(tables.Rank == 1 && tables.GetLowerBound(0) == 0 && index >= 0 && index < tables.Length,
            $"{path} 不在预期的零起一维表数组范围内；实际类型 {tables.GetType().FullName}，长度 {tables.Length}。");
        var row = tables.GetValue(index);
        Require(row is not null, $"{path} 为空。");
        var length = ReadField<uint>(row!, "Length", path + ".Length");
        var actualRowSize = ReadField<uint>(row!, "RowSize", path + ".RowSize");
        Require(length == 1 && actualRowSize == rowSize,
            $"{path} 应有一行且行大小为 {rowSize}；实际行数 {length}，行大小 {actualRowSize}。");
        return ReadField<uint>(row!, "Offset", path + ".Offset");
    }

    private static T ReadField<T>(object owner, string name, string path) where T : notnull
    {
        var type = owner.GetType();
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Require(field is not null, $"找不到字段 {path}；实际所属类型 {type.FullName}。");
        var value = field!.GetValue(owner);
        Require(value is T, $"字段 {path} 应为非空 {typeof(T).FullName}；实际为 {value?.GetType().FullName ?? "null"}。");
        return (T)value!;
    }

    private static void RequireBytes(byte[] data, uint offset, byte[] expected, string path)
    {
        RequireRange(data, offset, expected.Length, path);
        var actual = data.AsSpan((int)offset, expected.Length);
        Require(actual.SequenceEqual(expected),
            $"{path} 在偏移 {offset} 的预期字节为 {Convert.ToHexString(expected)}；实际为 {Convert.ToHexString(actual)}。");
    }

    private static void RequireRange(byte[] data, uint offset, int count, string path) =>
        Require(offset <= data.Length && count >= 0 && count <= data.Length - (long)offset,
            $"{path} 无法访问偏移 {offset} 起的 {count} 字节；缓冲区长度 {data.Length}。");

    private static void Require(bool condition, string detail) =>
        Assert.True(condition, $"Cecil 元数据测试夹具与内部布局不符（实际程序集：{typeof(ModuleDefinition).Assembly.FullName}；" +
            $"基准布局：Mono.Cecil 0.11.5）。{detail} 请重新验证夹具布局。");
}
