# C# Style Rules

These rules apply to every C# project in this repository. `.editorconfig` is authoritative for machine-checkable formatting and naming.

## Namespaces and indentation

- Use four spaces; never use tabs.
- Use file-scoped namespaces: `namespace AspectN.Syntax;`.
- Keep one primary type per file and match the file name to that type.

## Field naming

- Private instance fields use `_camelCase`, for example `_sourceText`.
- Private static fields, including `static readonly`, use `_PascalCase`, for example `_DefaultOptions`.
- Constants use `UPPER_SNAKE_CASE`, for example `DEFAULT_CACHE_SIZE`.

## Control flow

Prefer guard clauses and early `return`, `continue`, or `break` statements. Handle exceptional or disqualifying conditions first so the main path stays at the lowest indentation level.

```csharp
public void Process(List<int> values)
{
    if (values.Count < 10) return;

    ProcessCore(values);
}
```

Do not wrap the main path in the inverse condition:

```csharp
if (values.Count >= 10)
{
    ProcessCore(values);
}
```

When a simple condition is followed only by `return`, `continue`, or `break`, keep it on one line and omit braces:

```csharp
if (candidate is null) return;
if (!candidate.IsSupported) continue;
if (matchFound) break;
```

Use braces when a branch has multiple statements, contains explanatory comments, or placing it on one line would reduce readability. Do not compress ordinary business logic into one-line conditionals.
