using System.Text;
using AspectN.TypeSystem;

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
        Func<T, bool, bool, IEnumerable<TypeTextPart<T>>> expand, MappingContext resources) where T : class
    {
        var output = new StringBuilder();
        var pending = new Stack<IEnumerator<TypeTextPart<T>>>();
        pending.Push(Enumerable.Repeat(TypeTextPart<T>.Node(root, qualified, modified), 1).GetEnumerator());
        try
        {
            while (pending.Count > 0)
            {
                resources.SpendWork();
                var items = pending.Peek();
                if (!items.MoveNext()) { pending.Pop().Dispose(); continue; }
                var part = items.Current;
                if (part.Text is not null)
                {
                    resources.CheckTextLength(output.Length, part.Text.Length);
                    output.Append(part.Text);
                }
                else pending.Push(expand(part.Type!, part.Qualified, part.Modified).GetEnumerator());
            }
            return output.ToString();
        }
        finally { foreach (var items in pending) items.Dispose(); }
    }
}
