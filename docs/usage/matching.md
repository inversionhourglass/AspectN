# 编写匹配表达式

<!-- aspectn-usage:v1
{
  "schema_version": 1,
  "capability_ids": [
    "SYN-GRAMMAR-001",
    "GEN-IDENTITY-001",
    "USE-REF-001",
    "USE-ARRAY-001",
    "CROSS-AVAILABILITY-001",
    "CROSS-LOGIC-001",
    "CROSS-RESOURCES-001"
  ],
  "inventory_refs": [
    "INV-0007",
    "INV-0008",
    "INV-0010",
    "INV-0011",
    "INV-0012",
    "INV-0015"
  ]
}
-->

关键字小写且区分大小写。成员表达式使用 CompileMemberExpression，纯类型使用 CompileTypePattern。

| 表达式 | 用途 |
| --- | --- |
| method(public string Example.Service.GetName()) | 普通方法 |
| callable(* Example.Service.*(..)) | 可调用成员，含访问器/运算符等 |
| execution(* Example.Service.*(..)) | 还包括构造器 |
| attr(call Example.MarkerAttribute) | 直接方法目标属性 |
| `System.Collections.Generic.IEnumerable<string>`+ | 声明的类型关系模式 |

选择器/属性/regex 尚未逐能力建档，精确定义依[规范 §6–15](../superpowers/specs/2026-07-31-aspectn-language-spec.md)，不按示例猜测全部适用域。

## 修饰符与类型名称的边界

名称中的 `*` 与前后字符连续组成名称模式，不能因前缀恰好是关键字而拆开：

| 表达式 | 含义 |
| --- | --- |
| `method(static* Run())` | 返回类型名称以 static 开头，不限制方法是否静态 |
| `method(static * Run())` | 静态方法，返回类型任意 |
| `method(static static* Run())` | 静态方法，返回类型名称以 static 开头 |
| `method(!static* Run())` | 返回类型不匹配 static 前缀模式，不表示“非静态方法” |
| `method(!static * Run())` | 非静态方法，返回类型任意 |

例如普通 C# 类型 `staticWidget` 可由 `static*` 匹配；不能因此选中返回 string 的静态方法。`public*`、`private*`、`protected*`、`internal*` 同理。`private protected*` 表示 private 修饰符及 protected 前缀返回类型；完整复合访问性应写 `private protected *`。`public|internal private*` 的备选权限不会消费返回类型前缀。

该边界也适用于属性选择器、限定名、嵌套路径、泛型及相邻类型后缀；`public|internal` 和 `(private protected)|public` 等修饰符写法继续可用。详见[词法与邻接条目](../knowledge/capabilities/syntax/SYN-GRAMMAR-001.md)。

## 泛型

模式变量要写 $T，例如 method($T `Converter.Convert<$T>`($T))。省略 generic clause 对普通名字要求 arity=0；`Name<?>` 才是不限定 arity，裸 * 有单独规则。模式变量与 CLR 声明形参身份不同。

默认 `GenericVariableMode.Declaration` 将 `$T` 作为候选自身声明槽位的别名。`List<$T>` 匹配 `List<>` 的自身形参，不匹配 `List<int>` 或用其他声明形参构造的 `List<U>`。`method($T Service.Echo<$T>($T))` 查找返回及参数引用同一自身方法形参的声明；`Echo<int>` 不会自动变回开放定义。

变量只能直接在类型根、声明类型或方法名的泛型槽位首次声明；已有别名可用于返回、参数、特性或关系。首次从 `IValue<$T>+`、`attr(call TagAttribute<$T>)` 或 `Box<List<$T>>` 捕获类型，需要显式选择 Capture：

```csharp
var capture = new AspectNCompilationOptions(
    GenericVariableMode.Capture,
    new AspectNMatchOptions(maximumBindingAlternatives: ResourceLimit.Finite(512)));
var compilation = new AspectNEngine().CompileTypePattern(
    "List<$T>", compilationOptions: capture);
```

Capture 在预算内保留原通用捕获语义，包括具体类型及多备选筛选。默认每个收集器 256 个备选、每次匹配 50,000 个绑定工作单位；专项预算耗尽时整次匹配为 Unknown/AN3007；不会只保留前几个备选。[边界示例](examples/MatchingBoundaries/Program.cs)可直接运行两种模式及超限对照。编译后的模式固定，全部解析/匹配选项按值进入缓存键。总工作等额度另见[资源配置](resource-limits.md)。

`Owner<string>`.`M<V>` 与 `Owner<int>`.`M<V>` 中 V 的声明可相同，但有效约束查询上下文不同；同名不同程序集或不同重载不能按显示名称合并。详见[泛型身份](../knowledge/capabilities/generics/GEN-IDENTITY-001.md)。

## ref 和数组

method(* Service.Get(ref int)) 精确要求 Ref。省略注解 int 不约束 ref-kind；!ref int 要求非 by-ref。!ref 必须邻接，! ref 是诊断；返回不能用 out/in。[ref 条目](../knowledge/capabilities/type-uses/USE-REF-001.md)说明裸 Type 为什么不能猜分类。

int[] 同时接受 rank-one Vector 和 General；int[*] 是模型元数据记号，没有增加 DSL 语法。身份和名义关系查询仍区分 Shape；规范 regex 文本也按 rank，不能用于另选 General。[数组条目](../knowledge/capabilities/type-uses/USE-ARRAY-001.md)给方向性反例。

逻辑表达式可用 &&、||、! 和括号。Unknown 的否定仍未知；见[结果说明](results-and-diagnostics.md)。
