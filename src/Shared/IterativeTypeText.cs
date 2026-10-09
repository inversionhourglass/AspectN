using System.Text;

namespace AspectN.AdapterInternals;

internal sealed class TypeTextPart<T>(T? type, string? text, bool qualified, bool modified) where T : class
{
    public T? Type { get; } = type;
    public string? Text { get; } = text;
    public bool Qualified { get; } = qualified;
    public bool Modified { get; } = modified;

    public static TypeTextPart<T> Literal(string text) => new(null, text, false, false);
    public static TypeTextPart<T> Node(T type, bool qualified, bool modified = false) => new(type, null, qualified, modified);
}

internal static class IterativeTypeText
{
    public static string Format<T>(T root, bool qualified, bool modified,
        Func<T, bool, bool, IEnumerable<TypeTextPart<T>>> expand) where T : class
    {
        var output = new StringBuilder();
        var pending = new Stack<TypeTextPart<T>>();
        pending.Push(TypeTextPart<T>.Node(root, qualified, modified));
        while (pending.Count > 0)
        {
            var part = pending.Pop();
            if (part.Text is not null)
            {
                output.Append(part.Text);
                continue;
            }
            // 文本片段与子形状一起逆序入栈，保持原有规范文本顺序。
            var parts = expand(part.Type!, part.Qualified, part.Modified).ToArray();
            for (var index = parts.Length - 1; index >= 0; index--) pending.Push(parts[index]);
        }
        return output.ToString();
    }
}
