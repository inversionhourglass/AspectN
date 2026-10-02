# Legacy 与 Shared.Cecil.AspectN 的兼容性政策

本文件记录 2026-10-01 的用户确认。兼容比较对象为冻结的 Shared.Cecil.AspectN `03a9d1030f92a64449b6220eb45d7198d31653db`。比较表达式接受情况和相同候选的匹配行为，不要求内部代码或 AST 相同。下列明确批准的修正和增强不应再作为待修复的兼容缺陷列举。Standard 与 Legacy 的其他差异另行处理，见 [legacy-standard-differences.md](legacy-standard-differences.md)。

## 保持 Shared 的合法语法和功能

| 表达式 | Legacy 行为 |
|---|---|
| `method(T *(..))` | 未在类型或方法泛型子句中声明的 `T` 是类型名称；不会命中返回 `int` 的方法。`T1`、`T42` 等同样不自动成为变量。 |
| `method(T *<T>(T))` | 泛型子句声明的名字按位置绑定；元数据实际泛型参数名称不必叫 `T`。 |
| `method(* *..*(..))`、`method(* *..StaticMethod(..))` | `..` 保留任意 namespace 路径含义，可分离声明类型和成员；不是方法名称中的字面点号。 |
| `method(* *.StaticMethod(..))` | 合法的通配声明类型路径。 |
| `method(!int *(..))` | 合法的返回类型否定；返回 `int` 不匹配，其他返回类型可以匹配。否定访问权限 / `static` 的语法仍保留。 |
| `method(T *<,T>(..))`、`method(T *<T,>(..))` | 空槽匹配任意类型并占据一个泛型位置；分别把 `T` 绑定到第二、第一参数。多个空槽、类型参数中的空槽同理。 |
| `List<int,>`、`List<,string>` | 独立类型入口和签名中的类型入口均接受；末尾空槽不是尾随垃圾 token。 |
| `method(* 123(..))`、类型 `123` | 名称按文本匹配，包括数字开头、纯数字和超出整数数值范围的名称。这与 C# 能否直接声明该名称无关。 |
| `method(async string *(..))` | 返回 `Task<string>` / `ValueTask<string>` 的方法可匹配，包括未使用 C# `async` 的方法。 |
| `method(async null *(..))` | 匹配非泛型 `Task` / `ValueTask` 返回类型；不检查 `IsAsync`，不匹配 `async void`。 |
| `method(async void *(..))` | 保留 Shared 的 void 返回类型分支；普通 void 方法也可命中，不按 C# async 标记区分。 |
| `method(int *(..))`、`method(!int *(..))` | 裸名称保留 Shared 对数组元素类型的匹配：int 数组会命中前者、被后者排除。写明数组后缀时，维度和层数必须全部匹配。 |
| 独立类型 `*..`、`Example..` | 保留 Shared 的语法接受情况与历史结果：末段类型名为空，这些模式不能命中普通类型。它们不等价于声明类型位置的省略通配名。 |

`async` 是历史返回类型糖。它不表示“仅匹配真正使用 C# async 的方法”。本次明确保留 Legacy 这一语法；没有恢复 Standard 的 async 修饰符。

Shared 对声明类型与其他类型位置使用不同编译路径。`*..*` 在声明类型位置包含嵌套类型；在返回类型、参数和独立类型入口中不会自动包含嵌套类型。`*..` 在后面这三种位置保留接受但不命中的历史行为。不能把声明类型的通配规则统一套到其他位置。声明类型的 `*..+.*` 则把省略末段名解释为通配名，保留合法匹配与可赋值语义。

裸 tuple、nullable、命名类型及可赋值类型模式保留 Shared 对数组元素形状的匹配；只有显式数组后缀约束数组维度和层数。`async T` 仍拒绝 Task 数组。裸 `async void` 的特殊分支不会忽略 `void<>`、`void<..>`、`void<!>` 等显式泛型子句。

Legacy 同时保留 Shared 的后缀组合解释：`int?[]` 中的 `?` 是数组元素字面名称的一部分，会匹配 IL 名为 `int?` 的类型的数组，不匹配 `Nullable<int>[]`。匹配真正的 nullable 数组请使用 `System.Nullable<int>[]`。此规则仅影响 Legacy，没有更改 Standard 的 nullable-array 解释。

`!int` 被拒绝是初始成员解析器 `e63db08` 中 `ParseModifiers` 无条件消费 `!` 的遗漏；没有发现主动禁用类型否定的提交说明。现在只有后续 token 表示修饰符时，`!` 才由修饰符解析器消费。

Rougamo 的[方法匹配文档](https://github.com/inversionhourglass/Rougamo/wiki/%E6%96%B9%E6%B3%95%E5%8C%B9%E9%85%8D#%E7%B1%BBaspectj%E8%A1%A8%E8%BE%BE%E5%BC%8F%E5%8C%B9%E9%85%8D)是 `..` 路径、泛型位置匹配和 async 返回类型糖的外部依据。

## 明确批准的 Shared 修正

| 旧实现行为 / 例子 | 新 Legacy 的规定与原因 |
|---|---|
| `method(* *<T,T>(..))` 可以命中有两个不同泛型参数的方法 | 重复 `T` 复用同一约束，不会匹配两个不同的泛型参数。实际泛型声明不能重复同一名称；不复刻旧的同名位置覆盖行为。 |
| `method(* && !string || int *(..))` 对 `int` 返回方法为 false | 按 `! > && > ||` 求值，相当于 `(* && !string) || int`，结果为 true。Shared 的 `!` 递归消费后续整个类型表达式，实际相当于 `* && !(string || int)`。用户已批准修正这一作用域。 |
| `(int)` 被当作一项 tuple；新版本此前把它当普通 int 分组 | Legacy 现在拒绝并给出诊断，tuple 至少需要两项；不要把漏写元素的表达式默认为 `int`。空 tuple `()` 也拒绝。Boolean 类型分组 `(int || string)`、`!(int || string)` 保留。 |
| `.StaticMethod`、`..StaticMethod` 可以被旧实现接受 | 拒绝前导路径分隔符。使用 `*.StaticMethod`、`*..StaticMethod`，或省略整个声明类型直接写 `StaticMethod`。 |
| `Service +` 可以被旧实现接受 | `+` 必须紧邻其类型目标。 |
| `attr(ret * extra)`、`method(* Type.Foo(int bool))` 忽略部分内部 token | 拒绝额外 token 和缺少参数分隔逗号，返回定位诊断；不生成容易误匹配的模式。 |
| 独立类型 Boolean 表达式，如 `!int || string`，旧入口可能抛异常 | 两种类型入口使用相同的正常 Boolean 运算符优先级，返回可用的模式。 |

可用匹配器之间的结果修正（重复泛型名、Boolean 否定作用域、ref/out/in 参数按 ref kind 匹配）在 `tests/AspectN.LegacyOracle.Tests/approved-differences.json` 逐候选登记，并必须被实际语料消费。接受/拒绝变化由 `LegacyGrammarCompatibilityTests` 与本政策共同覆盖，不能把无效新模式伪装为可用匹配器的 false 结果。

`Service <T>.Run`、`Service.Run <T>` 在 Shared 中本来就报错，原新解析器却接受。此次恢复一致性：`<` 必须紧邻名称，合法写法为 `Service<T>.Run`、`Service.Run<T>`。不能把这一修复记成主动偏离 Shared 的批准差异。

## 2026-10-01 第二轮对照裁决

第二轮对照（[复查报告](../../artifacts/reviews/aspectn-shared-legacy-recheck-c65d686-2026-10-01.md)）发现的差异按下列裁决处理；回归覆盖在 `LegacyGrammarCompatibilityTests`。

### 补齐的兼容遗漏（与 Shared 对齐）

| 表达式 / 场景 | 规则 |
|---|---|
| `attr(type *)`、`attr(* *)` 对声明类型带特性的方法 | Type 目标参与通配 / 复合特性枚举；声明类型特性随候选签名映射（对应旧 Shared 的 `AttributeSignatures.Types`），不再返回 Unknown。 |
| `ctor(*..*())`、`ctor(Service<T1,T2>())` | ctor 声明类型改走 genericOut 规则：声明泛型变量，`*` / `*..*` 视为含嵌套类型的通配。cctor 不套用——Shared 的 cctor 声明类型按 genericIn 处理，维持原状。 |
| `setter(private * *)` 对 public get / private set 的 setter | property / getter / setter 一律按当前 accessor 自身权限求值（旧 `PropertyPattern.IsMatch` 用 accessor 的 `signature.Modifiers`），不取关联属性整体权限。 |
| `int[]+`（含参数、返回位置） | Legacy 接受：`+` 下沉到数组元素（`int[]+` 等价 `int+[]`），保留层数与 rank，与 Shared 的 assignableMatch 元素语义一致。Standard 维持规范 §12.4 的数组目标拒绝。 |
| genericIn 裸单段名：`List`、`Dictionary+`、`*[]` 元素、嵌套实参 `...<string, List>` | 省略泛型子句表示零泛型参数（Shared 的 `SingleNonGenericNamePattern`），不匹配构造类型；限定名（`System.Collections.Generic.List`）省略仍是任意泛型。裸 `void` 例外地保留 Any——Void 恒为零泛型，匹配等价，仅为区分 `async void` 糖与 `void<..>` 等显式子句。声明位置（genericOut）不受影响。 |
| `*..*`、`*..Foo` 对全局命名空间类型 | 裸 `*..` 前缀匹配任意命名空间（含全局），对齐 Shared `NamespacePattern` 的特例；限定前缀（`A..*`）不享受。genericIn 的 `*..*` 仍不自动包含嵌套类型。 |
| `method(* *(..))` 对 ref 返回 | 通配返回 `*` 不区分 ref kind（旧 `ExecutionPattern` 不比较返回 RefKind）。具名返回（`int`）双方都不匹配 ref 返回；Standard 维持精确比较（规范 §11 仅豁免通配参数）。 |

### 明确拒绝（Shared 接受，但属于实现泄露或错误）

| 表达式 | 规则 |
|---|---|
| `method(any * *(..))`、`method(none * *(..))` 及 `!any` / `!none` | `any` / `none` 是旧实现 `ModifierPattern.Flags` 枚举名泄露为修饰符（查表把全部枚举名小写收录；`none` 的 Flags 值为 0，等价于 `any`），不是文档化访问性关键字。Legacy 拒绝。 |
| `method(ASYNC void *(..))`、`method(Async void *(..))` | async 糖严格小写（旧 `ParseAsync` 区分大小写）。大写变体按普通类型名解析并因后续 token 报诊断。Legacy 关键字整体仍大小写不敏感，此为例外对齐。 |
| `int []`、`int ?`（空格后缀）及签名位置对应写法 | `[` 与 `?` 必须与类型模式邻接，对齐 Shared 的类型累积截断。仅 Legacy 生效；Standard 规范 §3.2 未把 `[` / `?` 列入邻接约束，保持接受。数组括号内部的空格（`int[ , ]`）不受影响。 |

### 保留的增强与修复（有意偏离 Shared）

| 表达式 / 场景 | 规则 |
|---|---|
| `method((int,string)[] *(..))`、`(int,string)?` 等 tuple 后缀 | 结构化后缀语义。Shared 在返回位置拒绝、独立入口忽略尾随后缀（匹配裸 tuple），按 Shared 的遗漏处理；Legacy 统一按结构化语义接受。 |
| `List<int || string>` | 泛型实参内的 Boolean 组合是既有 Boolean 规则在此位置的延伸，保留；Shared 抛 `ArgumentException`。 |
| `method(* *(ref int))`、out / in 参数 | 按 ref kind 匹配。Shared 把 `(ref int)` 解析为双槽参数（`ref` 名 + 隐式 Any），永不匹配单参数方法。差分语料逐候选登记（`approved-differences.json`，`LegacyFixture::0x06000013`）。 |
| `int+[]`、`int+?`、`int?+` | 按 AST 逐层组合：`int+[]` 是元素可赋值的数组（不是"名为 int+ 的类型的数组"）、`int+?` / `int?+` 是组合 nullable。Shared 只识别末尾一个 `+` / `?`，更早的后缀拼进字面名称。`int?[]` 的字面名称规则（上轮）维持。 |
| `X?+` 对非 Nullable 候选（如 `Box?+` 对 `int`） | 直接 NotMatched。nullable 目标具体化失败时回退闭包求值（原先仅 Standard），候选按 nullable 分派，不再因目标不可具体化返回 Unknown。 |
| `method(null *(..))` 对 CLR 名为 `null` 的类型（C# `class @null`） | 裸 `null` 作为普通类型名参与匹配（当前实现保持）。Shared 的 `NullTypePattern` 永不匹配、`!null` 恒为 true；不为该极端场景引入硬编码特判。 |

## 保留的增强与括号含义

显式模式变量 `$T`、`ref` / `ref readonly` 返回和 Unicode 名称属于已批准增强，继续保留。

顶层括号指 `(method(* *(..)))`，或者 `(method(...) || method(...)) && !attr(...)`：它对选择器表达式进行逻辑分组，不是返回类型 tuple。新 Legacy 保留该增强。

类型入口的括号用途另有区分：`(int,string)` 是 tuple；`(int || string)` 是 Boolean 类型分组；`(int)`、`()` 因 tuple 元素不足而诊断。泛型中的 `List<int,>` 是一个已指定参数加一个缺省参数，与缺少元素的 tuple 不同。

## 编译异常与诊断的最终使用效果

`ArgumentOutOfRangeException` 会中断 `AspectNCompilation.Create`，调用方拿不到编译结果。没有捕获时，它会继续向上传播，例如终止配置加载或构建；捕获后也只能把它当异常处理，缺少语言层面的稳定诊断。

错误诊断让编译 API 正常返回：`Diagnostics` 包含错误码、消息、源位置，`BoundRoot` 为 null，`TryGetMemberPattern` / `TryGetTypePattern` 为 false。集成方可以定位用户输入、汇总多个错误并按既定策略使构建失败。它不会把非法模式解释为“匹配结果 false”。是否最终停止构建由集成方决定，因此两种情况都可能构建失败，但可观察的错误信息和调用方处理方式不同。

语法已有 Error 时不进入绑定器。此规则修复 `method(int <T> X.Y())`、`method(Service <int> Service.Run())` 和 `property(Service <int> Service.X)` 的负 TextSpan 异常；Standard 和 Legacy 均适用。
