# C# 风格规则

这些规则适用于仓库中的每个 C# 项目。可机器校验的格式与命名以 `.editorconfig` 为准。

## 命名空间与缩进

- 使用四空格缩进，不使用制表符。
- 使用文件范围命名空间，例如 `namespace AspectN.Syntax;`。
- 每个文件保留一个主要类型，文件名与该类型一致。

## 字段命名

- 私有实例字段使用 `_camelCase`，例如 `_sourceText`。
- 私有静态字段（包括 `static readonly`）使用 `_PascalCase`，例如 `_DefaultOptions`。
- 常量使用 `UPPER_SNAKE_CASE`，例如 `DEFAULT_CACHE_SIZE`。

## 控制流

优先使用前置条件检查及提前 `return`、`continue`、`break`。先处理异常或不满足条件的情况，让主路径保持最浅缩进。

```csharp
public void Process(List<int> values)
{
    if (values.Count < 10) return;

    ProcessCore(values);
}
```

不要用反向条件包裹主路径：

```csharp
if (values.Count >= 10)
{
    ProcessCore(values);
}
```

简单条件后只有 `return`、`continue` 或 `break` 时，保持单行并省略大括号：

```csharp
if (candidate is null) return;
if (!candidate.IsSupported) continue;
if (matchFound) break;
```

分支含多条语句、解释性注释，或单行会降低可读性时使用大括号。不要把普通业务逻辑压缩为单行条件语句。

## 主构造器与注释

- 当构造参数表示必需的实例依赖或初始化状态，且转换保留构造器可见性、参数顺序、校验、初始化顺序和可观察行为时，优先使用主构造器。
- 类型有多个独立构造路径、构造器特有的校验或副作用、初始化顺序要求，或转换会改变公共契约时，保留普通构造器。保留多个构造器时明确它们的委托关系。
- 每个对外公开的类型、接口、枚举、方法、构造器和属性都添加英文 `/// <summary>` API 文档，以类型与成员的有效可见性为准；内部类型中的 public 辅助成员仍按内部注释规则处理。对外公开方法按适用情况说明每个参数和返回值；属于公共契约的异常也必须说明。
- 内部 XML 注释及代码注释使用中文。对复杂内部类型、方法和实现块，解释不明显的算法、状态转换、元数据映射、兼容规则或不变量，不注释显然的语句。
- Markdown 说明文档使用中文；完整语言规则见仓库根目录 `AGENTS.md`。
