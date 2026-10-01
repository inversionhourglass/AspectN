# Legacy 与 Shared.Cecil.AspectN 的兼容性政策

本文件记录 2026-10-01 的用户确认。兼容比较对象为冻结的 Shared.Cecil.AspectN `03a9d1030f92a64449b6220eb45d7198d31653db`。比较表达式接受情况和相同候选的匹配行为，不要求内部代码或 AST 相同。下列明确批准的修正和增强不应再作为待修复的兼容缺陷列举。Standard 与 Legacy 的其他差异另行处理。

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

两项可用匹配器之间的结果修正（重复泛型名、Boolean 否定作用域）在 `tests/AspectN.LegacyOracle.Tests/approved-differences.json` 逐候选登记，并必须被实际语料消费。接受/拒绝变化由 `LegacyGrammarCompatibilityTests` 与本政策共同覆盖，不能把无效新模式伪装为可用匹配器的 false 结果。

`Service <T>.Run`、`Service.Run <T>` 在 Shared 中本来就报错，原新解析器却接受。此次恢复一致性：`<` 必须紧邻名称，合法写法为 `Service<T>.Run`、`Service.Run<T>`。不能把这一修复记成主动偏离 Shared 的批准差异。

## 保留的增强与括号含义

显式模式变量 `$T`、`ref` / `ref readonly` 返回和 Unicode 名称属于已批准增强，继续保留。

顶层括号指 `(method(* *(..)))`，或者 `(method(...) || method(...)) && !attr(...)`：它对选择器表达式进行逻辑分组，不是返回类型 tuple。新 Legacy 保留该增强。

类型入口的括号用途另有区分：`(int,string)` 是 tuple；`(int || string)` 是 Boolean 类型分组；`(int)`、`()` 因 tuple 元素不足而诊断。泛型中的 `List<int,>` 是一个已指定参数加一个缺省参数，与缺少元素的 tuple 不同。

## 编译异常与诊断的最终使用效果

`ArgumentOutOfRangeException` 会中断 `AspectNCompilation.Create`，调用方拿不到编译结果。没有捕获时，它会继续向上传播，例如终止配置加载或构建；捕获后也只能把它当异常处理，缺少语言层面的稳定诊断。

错误诊断让编译 API 正常返回：`Diagnostics` 包含错误码、消息、源位置，`BoundRoot` 为 null，`TryGetMemberPattern` / `TryGetTypePattern` 为 false。集成方可以定位用户输入、汇总多个错误并按既定策略使构建失败。它不会把非法模式解释为“匹配结果 false”。是否最终停止构建由集成方决定，因此两种情况都可能构建失败，但可观察的错误信息和调用方处理方式不同。

语法已有 Error 时不进入绑定器。此规则修复 `method(int <T> X.Y())`、`method(Service <int> Service.Run())` 和 `property(Service <int> Service.X)` 的负 TextSpan 异常；Standard 和 Legacy 均适用。
