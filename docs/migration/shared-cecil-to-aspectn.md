# 从 Shared.Cecil.AspectN 迁移

AspectN 用与后端无关的编译/匹配流程取代绑定 Cecil 的解析器，实现现有 Standard 语言。Legacy 和语言选择 API 已移除。这是破坏性迁移：切换调用方前，核对表达式及预期匹配范围。Rougamo 的实际集成与破坏性大版本发布属于后续独立任务。

## 程序包与命名空间变化

将 Shared.Cecil.AspectN 源码依赖替换为：

- `AspectN`：解析、编译、签名及匹配结果。
- `AspectN.Cecil`：使用官方 `Mono.Cecil` 程序包的应用。
- `AspectN.Cecil.Fody`：使用 `FodyHelpers` 内置 Cecil 程序集的 Fody 织入器。

两个 Cecil 程序包公开相同 `AspectN.Cecil` 命名空间，但使用不同强名称的 Cecil 身份。使用方项目只能引用其中一个。Rougamo 使用 `AspectN.Cecil.Fody`。

解决方案、格式及测试不再依赖 Shared 对照实现。历史检出保持冻结，不因其退休而修改。

## 编译表达式

```csharp
using AspectN;

var compilation = new AspectNEngine().CompileMemberExpression(
    expression,
    compilationOptions: new AspectNCompilationOptions(
        TimeSpan.FromMilliseconds(100)));

if (!compilation.TryGetMemberPattern(out var pattern))
{
    // 报告 compilation.Diagnostics。
}
```

`ParseMemberExpression(string source)`、`ParseTypePattern(string source)` 没有 options 参数。引擎编译方法接受 `source` 和可选 `compilationOptions`。移除对 `AspectNLanguageVersion`、`AspectNParseOptions`，以及树/编译/编译模式中 `LanguageVersion` 的引用。

不支持自定义选择器、谓词和匹配器，移除扩展注册表及扩展上下文。未知选择器名称（包括限定名）报告 AN2001。关键字小写且区分大小写，不从输入推断模式。

## 先映射 Cecil 元数据，再匹配

```csharp
using AspectN.Cecil;
using AspectN.Evaluation;
using Mono.Cecil;

using var module = ModuleDefinition.ReadModule(assemblyPath);
using var typeSystem = new CecilTypeSystem(
    module,
    new CecilMappingOptions(leaveOpen: true));

var method = module.Types
    .SelectMany(type => type.Methods)
    .Single(candidate => candidate.FullName == methodFullName);
var signature = typeSystem.Mapper.MapMethod(method);
var result = pattern!.Match(signature, typeSystem);

if (result.Status == MatchStatus.Unknown)
{
    // 保留诊断，并应用明确的调用方处理策略。
}
```

每个 `ModuleDefinition` 生命周期内保留一个 `CecilTypeSystem`，结束时释放。映射缓存归该上下文所有。示例中的 `leaveOpen: true` 让调用方负责模块释放；默认在类型系统释放时关闭模块。

移除的 `CompositeAccessibility` 选项没有替代项：可见性反映成员声明，不与外层类型求交。属性元数据仍合并访问器可见性，属性选择器则检查当前访问器。

## 行为迁移检查

1. 盘点表达式及各表达式要选择的候选成员。
2. 按下表重写历史形式，检查匹配范围变化。
3. 编译每个表达式并处理诊断；保留 AN4003/4004/4005 迁移消息，不抑制它们。
4. 将元数据映射为签名，显式处理 `Matched`、`NotMatched`、`Unknown`，保留程序集或事实缺失的诊断。
5. 验证应用场景；相同规则通过多个元数据后端使用时，重跑跨适配器一致性测试。

## 表达式迁移

当前默认变量为 Declaration 自身声明槽位引用。需要原 AspectN 的具体类型、特性或祖先首次捕获时，编译传入 `new AspectNCompilationOptions(null, GenericVariableMode.Capture)` 并处理预算耗尽的 Unknown/AN3007；[当前迁移表](../usage/migration.md)区分默认编译诊断与候选不匹配。后端继续映射实际构造，不丢弃实参。

以下替代写法表达预期行为，不保证与 Shared.Cecil.AspectN 得到相同结果。

| 历史形式或意图 | AspectN 写法与效果 |
| --- | --- |
| 有名类型或方法不限元数 | 写 `Service<?>.Get<?>`；省略子句现在要求零元数 |
| 隐式变量 `T` | 始终写 `$T`，如 `method($T Converter.Convert<$T>($T))`；裸 `T` 是字面名 |
| `privateprotected`、`protectedinternal` | `private protected`、`protected internal`（AN4003） |
| 用空格分隔备选可见性 | 用 `|` 连接；`(private protected)|public` 中的复合短语是一项备选 |
| `async T`、`async null` | `tasklike<T>`、裸 `tasklike`；依返回形状，与编译器 async 元数据无关 |
| 历史 `async void` 意图 | 写 `void`，不将其转换为 Task 形状 |
| `*..M` 成员路径 | 非空命名空间、非嵌套类型写 `*..*.M`；要包含全局及嵌套类型则用 `M` 省略声明类型 |
| 把 `*..*` 当通用声明类型 | 省略声明类型，或整个成员路径使用 `*`；显式路径遵守命名空间与嵌套规则 |
| 用 `int` 选择数组 | 写所需的 `[]` 维数；普通有名模式不剥离数组层 |
| `int[]+` | 若要求元素关系，使用 `int+[]`；`+` 的数组目标无效 |
| 用元组简写表示引用 Tuple | 写 `System.Tuple<...>`；`(int,string)` 只匹配 ValueTuple |
| `T?` | 引用注解保持同一 CLR 身份；值类型要求 `Nullable<T>` |
| 参数默认只按值传递 | 添加 `!ref`；未注解类型匹配全部引用分类 |
| 特性目标长名称 | `call`、`prop`、`para`、`ret`（AN4005）；保留 `para[0]`/`para 0`、`para[*]`/`para *` |
| `tasklike<void>` | 裸 `tasklike`（AN4004） |
| 路径前置 `!Run` | 支持；否定名称及其泛型子句；排除任意元数使用 `!Run<?>` |
| 布尔声明/成员路径 | 使用 `(A || B).(Run || Stop)`；各位置保持普通布尔语义 |
| 数组/可空声明模式 | `int[,].Get`、`int?.GetValueOrDefault` 使用结构类型匹配 |
| 连续 `!!` 或 `! !` | 移除多余否定；AN1001 指向第二个 `!` |
| 属性名 `Value<>` | 移除子句；属性名不能泛型化（AN2011） |
| 命名空间 `Ns<int>.Owner` | 移除命名空间子句，或嵌套类型写 `Ns.Outer<int>/Inner`（AN2011） |
| `tasklike <int>` | 将子句与关键字邻接：`tasklike<int>` |

`execution` 包含构造器，`callable` 排除构造器，`method` 选择普通方法。`attr(exec ...)` 检查当前方法及关联属性，`attr(call ...)` 只检查方法。带泛型子句的通配名称是有名类型模式：`*<?>` 排除数组、声明泛型形参和未显式写嵌套路径的嵌套类型。

按当前规范签名核对正则表达式：引用前缀、带空格的复合可见性、逗号加空格的分隔符、`/` 嵌套和位置泛型名。编译器 async 标记省略。

正则表达式还必须满足规范第 15 节受限子集。移除注释及全部内联选项，包括 `(?x)`、`(?i)`；空格和 `#` 是字面字符。用普通正则形式或布尔组合的结构选择器重写环视、反向引用、命名/平衡/条件/原子分组及嵌套/相减字符类。字符类中的字面 `[`、`]` 需转义，替换 `[[]`、`[]abc]` 等形式。编译时，不支持的特性报告 AN2012，无效 .NET 正则语法报告 AN3002，且不返回模式。有限匹配超时仍返回 Unknown/AN3003。

结构化声明类型模式中，`*.Run` 包含数组和嵌套声明类型；用 `*<?>.Run` 保留非嵌套有名类型约束。成员名布尔分支中的泛型子句保留约束；`||` 分支首次引入的变量不能向外逃逸，否定不能引入新变量。不复现 Shared 的历史布尔路径拆分及否定通配符例外。

[Shared/Standard 审计](../reviews/2026-10-03-shared-standard-audit.md)保留固定版本证据；[后续确认裁决](../reviews/2026-10-03-shared-standard-decisions.md)记录哪些差异保留、哪些经审查后修复。

## 重要差异

- 新匹配器返回三态结果，不是裸布尔值。
- 后端对象先映射为与后端无关的签名，再匹配。
- AspectN 实现单一区分大小写的语法，不含兼容层。
- 程序集缺失和不支持的元数据形状返回带诊断的 `Unknown`，不是隐式不匹配。
- 声明可见性独立于外层类型可见性。

完整语义及原因见[语言指南](../language/README.md)、[正式语言规范](../superpowers/specs/2026-07-31-aspectn-language-spec.md)及[设计决策](../language/standard-design-decisions.md)。

## 构造中立签名

当前公开名称是 `MethodSignature`、`MethodKind`、`MapMethod`、`AttributeTarget.ForMethod`；历史中间阶段的 Callable 名称不是兼容别名。泛型形参声明现在包含程序集身份：

```csharp
using AspectN.Metadata;

var parameter = new GenericParameterSignature(
    "T", GenericParameterOwnerKind.Type, "Example.Container`1",
    new AssemblyIdentity("Example.Assembly"), position: 0);
var type = new GenericParameterTypeSignature(parameter);
```

尽可能使用映射器生成的签名。必需集合参数不能为 null，可选特性集合默认为空。参数特性要求从零开始的 ParameterIndex，非参数特性要求 null。InheritanceClosure 复制祖先列表，只接受 Unknown 失败。运行时类型系统能力声明所提供操作，不可用元数据仍可导致 Unknown。独立类型模式不携带引用分类；ref 注解使用成员返回/参数位置。
