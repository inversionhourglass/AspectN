# Shared 与 Standard 差异裁决

本文件记录用户对 [完整差异审计](2026-10-03-shared-standard-audit.md) 的裁决与实现。
审计和 JSON 证据保留固定版本的原始观察，不随本次修复重写。
当前语言契约以 [语言规范](../superpowers/specs/2026-07-31-aspectn-language-spec.md)
和 [独立设计决策](../language/standard-design-decisions.md) 为准。

## 已确认的设计

| 项目 | 裁决与当前行为 |
| --- | --- |
| N01 | 恢复路径前置 `!`，对声明类型、名称及名称的泛型约束的合取整体取反。修饰符、返回类型、参数与选择器域仍在路径否定之外。 |
| N02 | 支持声明类型与成员名称两处的 Boolean 组合：`(A || B).(Run || Stop)`。声明类型支持数组、nullable、嵌套泛型等结构；不复现 Shared 从右分支抽取名称的历史错误。 |
| N03 | 每个泛型空槽占一个位置，精确元数不丢弃开头空槽。 |
| N04 | 泛型实参数组属于实参节点，`List<int[]>` 不会变成 `List<int>[]`。 |
| N05 | 所有类型位置正常处理 `/` 嵌套路径。 |
| N06 | 泛型实参支持完整类型结构，短名不会缩减能力。 |
| N07 | 别名指向对应 System 类型，并保留显式泛型子句约束。 |
| N08 | 长 ValueTuple 按逻辑元素展开 Rest；显式 CLR 结构使用具名模式。 |
| N09 | 保留当前 Standard 的 `+` 结构白名单。 |
| N10 | 否定全通配匹配空集合，所有位置遵循正常 Boolean 补集，包括新增路径否定。 |
| N11 | 索引器 setter 按关联属性类型匹配。 |
| N12 | 属性名称取关联 Property 元数据，而非裁剪 accessor 方法名。 |
| N13 | `method` 选择普通方法；`callable` 扩展到 accessor、事件、运算符和转换；`execution` 再包含构造。 |
| N14 | 保留 Standard 的 `attr(exec ...)` 目标及历史位置迁移提示。 |
| N15 | 确认 Unicode 空白符增强；名称、分隔符与后缀继续遵守邻接规则。 |
| N16 | 拒绝连续 `!!` 和 `! !`，AN1001 定位第二个符号。显式括号分组 `!(!...)` 仍合法。 |
| N17 | 保留当前正则表达式格式与解析增强。 |
| N18 | 保留显式变量、逻辑分支隔离及绑定错误诊断。 |
| §6.2 | `tasklike` 的结果子句同样必须邻接：`tasklike<int>` 合法，`tasklike <int>` 报错。 |

N01 并不要求重解释 token 或放宽名称：语法树现在有独立的 MemberPath 和
MemberNamePattern 节点，每个方法名叶节点持有自己的泛型子句。
`!Run` 排除零泛型 Run；排除所有元数应写 `!Run<?>`。
名称局部否定可写 `A.!Run`，声明类型局部否定可写 `(!A).Run`。

N02 是实质扩展，不再将声明类型限定为名称 token 路径。
多维数组确有自身的 Get/Set/Address 方法，`int[,].Get` 可匹配它们；
`int?.GetValueOrDefault` 可匹配 Nullable<int> 的实例方法。
声明位置的裸 `*` 采用完整类型通配语义，因此 `*.Run` 现在包括数组、
泛型参数及嵌套类型。若需要单层具名类型约束，写 `*<?>.Run`；
其他具名模式的 namespace、arity 与 `/` 规则没有改变。

保留既有 Standard 的上下文名称规则：声明类型位的 `tasklike<int>+.Get`
仍将 tasklike 视为字面类型名，返回/参数及泛型实参内的 tasklike 继续表示
Task/ValueTask 形状。限定名 `Example.tasklike<int>` 在任何位置都无歧义。
变量不能首次在否定中引入，也不能从 `||` 分支泄漏；Unknown 的否定仍是 Unknown。

## 已修复的实现缺陷

| 项目 | 修复及回归覆盖 |
| --- | --- |
| I01 | property/getter/setter 任意名称分支后出现泛型子句均报 AN2011，范围定位该子句；声明类型泛型仍合法。 |
| I02 | Cecil/Fody 与 Reflection 按有效运算符元数据识别：public、SpecialName、非 RTSpecialName、非方法泛型、非 vararg、无 params，参数仅按值或 in；再验证名称、static/instance、返回形状及参数数量。普通 op_ 方法仍属 OrdinaryMethod。真实程序集在三适配器上验证，含 checked 转换及 C# 14 实例复合赋值/自增。泛型声明类型和合法 in 参数不受排除。 |
| I03 | 只有 `!` 是整个泛型子句时才识别为零元数；`Box<!int>` 和 `Run<!int>` 正常解析首槽否定模式。 |
| I04 | namespace 任意泛型子句报 AN2011，范围定位该子句；`Ns.Outer<int>/Inner<string>` 仍合法。错误子句不会引入变量。 |

C# 14 实例运算符的元数据规则参照
[微软规范](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-14.0/user-defined-compound-assignment)，
并以 SDK 编译的程序集验证。默认 Roslyn 4.12 将这些新实例运算符导入为 Ordinary；
适配器仅在元数据方法具有精确的编译器功能标记、已知名称及正确的 void/参数形状时
补齐 Operator 分类。普通同名方法保持 OrdinaryMethod；版本矩阵不因此升级。

既有适配器边界也保留：Cecil/Reflection 能识别 VB 的 op_Exponent、op_IntegerDivision、
op_Concatenate、op_Like 等合法元数据运算符，C# Roslyn 的元数据导入器将这些方法视为
Ordinary。这是既有后端分类差异，不属于 Shared/Standard 语法差异；本次不删除合法
VB 运算符，也不扩大上述 C# 14 功能标记的适用范围。

Cecil 的准确方法类别依赖可解析的 MethodDefinition。只有 unresolved MethodReference
时无法读取 SpecialName 或 accessor semantics，现有 MethodKind 模型又没有 Unknown；
除构造函数外仍回落到 OrdinaryMethod，不能据此断言真实方法类别。
这项模型边界同样影响 accessor，与类型关系查询的 Unknown 传播不同。
本次以测试固定边界，不扩展公开元数据可用性契约；使用方应提供可解析定义进行域匹配。

## 验证范围

定向回归覆盖路径 Boolean 优先级、泛型元数与参数位置、负路径与正常补集、
变量分支隔离、Unknown、源码诊断范围、泛型首槽否定、数组与 Nullable 的实际方法、
属性名与 namespace 约束，以及三适配器的真实运算符分类。
完整构建、测试、格式化与包验证均在当前 checkout 执行；不依赖 oracle，
不修改相邻仓库或冻结缓存，不迁移 Rougamo，不推送或发布。

最终验证结果：

| 检查 | 结果 |
| --- | --- |
| 默认 Cecil 0.11.5 / Roslyn 4.12.0 | 全量 952 项通过，无失败或跳过，含 .NET Framework、Fody 身份、三适配器及程序集卸载 |
| Cecil 0.11.6 / Roslyn 5.6.0 | 全量同样 952 项通过，保留原版本矩阵 |
| Release 构建 | 零警告、零错误，覆盖 netstandard2.0、net10.0 及 net472 测试项目 |
| 格式化与包验证 | 格式化检查通过；五个包及临时消费者通过 |
| 独立代码审查 | 已修复运算符分类漏项，无剩余阻断问题 |

```powershell
dotnet restore AspectN.sln
dotnet build AspectN.sln -c Release --no-restore
dotnet test AspectN.sln -c Release --no-build --no-restore
dotnet format AspectN.sln --verify-no-changes --no-restore
powershell -NoProfile -File eng/verify-packages.ps1
dotnet test AspectN.sln -c Release -p:CecilVersion=0.11.6 -p:RoslynVersion=5.6.0 --verbosity minimal
```

保留 CI 中固定 Rougamo checkout 的编译检查，本次未在相邻 Rougamo 仓库执行迁移或写入。
