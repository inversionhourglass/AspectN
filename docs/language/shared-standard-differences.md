# Shared.Cecil.AspectN 与 Standard：已确认的设计差异

本文集中说明已经确认的语言差异、示例及形成差异的原因，供编写表达式和迁移 Shared 使用。
这里的 Standard 指 AspectN 当前唯一的语言契约，不再是可选择的兼容模式。

比较基准：Shared 为 `03a9d1030f92a64449b6220eb45d7198d31653db`；Standard 为完成裁决实现后的
`084dfb6`。这是历史比较基准；当前实现验证的提交与版本矩阵见 [修复记录](../reviews/2026-10-05-review-ds-remediation.md)。整理日期：2026-10-03。后续行为变更应同步本文和语言规范。

原始审计记录的是修复前的 Standard，不能直接当作当前行为。本文结合
[完整审计及证据](../reviews/2026-10-03-shared-standard-audit.md)、
[最终裁决](../reviews/2026-10-03-shared-standard-decisions.md)和
[语言规范](../superpowers/specs/2026-07-31-aspectn-language-spec.md)整理，覆盖此前确认的差异以及 N01–N18、I01–I04。
路径否定已经恢复；连续 `!!` 已决定拒绝；这两项不沿用旧审计中的待选建议。

## 阅读约定

原因分为以下几类，同一项可能同时属于两类：

| 分类 | 含义 |
| --- | --- |
| 功能增强 | 支持更多有效表达式或候选，并明确其语义 |
| bug 修复 | 修正丢失约束、错误解析、错误匹配或异常 |
| 设计统一 | 消除位置、拼写或查询路径造成的双轨规则 |
| 约束收紧 | 对容易误写、无效或没有明确含义的输入给出诊断 |
| 架构取舍 | 明确语言、元数据后端及调用方各自的职责 |

“原因”解释当前已确认的 Standard 选择，不推断 Shared 作者当年的意图。
示例中的简短类型表达式用于类型入口；带 `method(...)` 等选择器的表达式用于成员入口。
“匹配/不匹配”分别指 `Matched` / `NotMatched`；“报错”指解析或绑定失败，不会产生可用的匹配器。
除专门说明的案例外，候选元数据完整，普通名称的声明类型与方法均为非泛型。

## 1. 关键字、名称、空白和 Boolean 语法

### 1.1 关键字统一小写并区分大小写

Shared 的选择器、权限和特性位置一般不区分大小写，历史 `async` 却要求小写。
Standard 的所有关键字统一要求小写；类型名和成员名继续按 ordinal、区分大小写匹配。

| 表达式 | Shared | Standard |
| --- | --- | --- |
| `METHOD(* *(..))` | 可作为 method 选择器 | 不作为内建 method 选择器接受 |
| `method(public * *(..))` | 合法 | 合法 |
| `method(* Run(..))` 与 `method(* run(..))` | 名称不同 | 名称不同 |

**原因：设计统一。** 关键字不再有各自的大小写例外，输入含义更容易预测。
大小写错误不能通过选择旧语言模式绕过。

### 1.2 支持 Unicode 名称，拒绝数字名称

Shared 主要接受 ASCII 字母、数字和下划线，也允许数字开头或全数字名称。
Standard 支持 Unicode 字母及允许的名称续字符，名称不能以数字开头。

例如 `method(* 获取(..))` 可以表示名称为“获取”的方法；`method(* 123(..))` 报错。
这是名称语法增强和约束收紧，并不承诺支持所有任意 CLR 名称：当前 lexer 按 `char` 处理，
覆盖 BMP 字母及允许的组合、连接、格式续字符；没有任意 Unicode scalar 或 CLR 名称的转义语法。

**原因：功能增强、约束收紧。** 允许正常 Unicode 命名，同时把数字字面量和名称区分开。

### 1.3 扩大空白集合，同时统一邻接要求

Shared 识别空格、全角空格、tab、CR、LF。Standard 使用 `char.IsWhiteSpace`，
还接受 NBSP（U+00A0）、VT、FF、EM SPACE（U+2003）等空白。
例如类型入口中的 `int` 后跟 NBSP：Shared 拒绝，Standard 视为合法尾部空白。

增强的是语法间空白，不是任意位置都能插空白：

| 写法 | Standard |
| --- | --- |
| `method (* Run(..))` | 合法，选择器与 `(` 可分开 |
| `List<int>`、`int[]`、`int?`、`Base+` | 合法 |
| `List <int>`、`int []`、`int ?`、`Base +` | 报错，开头或后缀必须邻接 |
| `int[ , ]` | 合法，rank 内部可以有空白 |
| `(A \|\| B).(Run \|\| Stop)` | 分隔点必须邻接两侧模式 |
| `tasklike<int>` / `tasklike <int>` | 前者合法，后者报错 |

Shared 的部分 `+` 写法曾更宽松；Standard 统一要求后缀紧邻。
`tasklike` 是新形状关键字，其结果子句也采用同一邻接规则（§6.2 裁决）。
正则 body 不按这些规则改写，始终保留原文。

**原因：功能增强、设计统一。** 兼容常见文本空白，同时避免空白改变名称和签名槽位的划分。

### 1.4 Boolean 分组、优先级和否定范围统一

Shared 的独立类型入口和签名类型入口存在不同解析路径，签名内的否定可能吞掉后续组合；
顶层成员表达式也没有正常的括号分组能力。
Standard 在成员表达式、类型模式和成员名称中统一采用 `! > && > ||`，同级左结合，括号可分组。

```text
(method(* Run(..)) || method(* Stop(..))) && !attr(exec ObsoleteAttribute)
List<(int || string)>
method(* (A || B).(Run && !Stop)(..))
```

连续 `!!` 和 `! !` 在当前 Standard 中均报 AN1001，定位第二个 `!`；
显式分组的 `!(!int)` 仍合法。Shared 顶层原本也拒绝连续否定，
所以这项历史上的 Standard 增强已经撤回，不能再列为当前保留的差异。

**原因：bug 修复、功能增强、约束收紧。** 分组和优先级保留组合能力；
连续 `!` 没有正向表达收益且容易是手误，因此要求作者明确分组或直接简化。

### 1.5 无效输入不能通过忽略 token 变成有效模式

Shared 的一些分支会接受前导/尾部空路径段、多余 token、缺逗号或不完整结构，
然后不匹配、忽略部分输入或在求值时抛异常。Standard 要求完整消费输入，并给出定位诊断。

例如前导 `.Owner`、尾部 `Owner.`、不完整成员路径 `method(* *..Run(..))` 报错；
参数位置的缺逗号不能被当成正常两个参数。泛型和参数的合法空槽仍保留，tuple 空槽则另有明确限制。

**原因：bug 修复、约束收紧。** 输入中的每个约束都应生效；无法解释的部分不能被静默丢弃。

## 2. 名称路径和签名中的声明类型

### 2.1 namespace、嵌套类型和通配范围明确分工

Standard 中 `.` 连接 namespace/类型以及声明类型/成员，`..` 只表示 namespace 中间的省略，
`/` 表示嵌套类型。Shared 的声明位置存在全局 namespace、全通配及嵌套范围的特殊规则，
其短嵌套类型路径在类型入口又可能被错误拼接。

| Standard 写法 | 范围或含义 |
| --- | --- |
| `Outer/Inner`、`A.B.Outer/Inner` | 按真实嵌套层级匹配；Shared 在 genericIn 位置曾不匹配对应类型（N05） |
| `method(* Run<?>(..))` | 不限制声明类型，包含全局 namespace、嵌套及数组声明类型 |
| `method(* *..*.Run<?>(..))` | 声明类型有 namespace 段，且是未写 `/` 的单层具名类型 |
| `method(* *.Run<?>(..))` | 裸类型 `*` 是完整通配，包含所有声明类型形状 |
| `method(* *<?>.Run<?>(..))` | 任意元数的单层具名声明类型，排除数组、泛型形参和未显式写出的嵌套类型 |

省略 namespace 的具名模式允许全局 namespace；`*..*` 则要求至少一个 namespace 段。
Shared 的某些 `*..` 声明前缀可包含全局 namespace，不能把旧范围直接套到 Standard。
`method(* *..Run(..))` 缺少声明类型名；改成 `*..*.Run` 或直接省略声明类型时，匹配范围不同。

**原因：设计统一、bug 修复。** namespace 省略、嵌套结构和完整通配各有明确含义，
同一类型结构不会因所处签名位置不同而换一套解释。

### 2.2 路径否定保留，但与统一泛型元数规则一起解释

Shared 已有可用的 `!Run`、`!Owner.Run` 和属性路径否定；当前 Standard 已恢复这项能力（N01）。
`!` 对“声明类型匹配且成员名及其泛型约束匹配”的整体取反，而不否定返回类型、参数、修饰符或选择器域。

```text
method(* !Owner.Run<?>(..))
method(int !Owner.Run<?>(string))
getter(* !Value)
```

第一条排除 Owner 上任何泛型元数的 Run；Other.Run 仍可匹配。
第二条还要求普通方法返回 int、具有一个 string 参数，这些要求位于路径否定之外。
局部否定可写 `Owner.!Run<?>`；只否定声明类型可写 `(!Owner).Run<?>`。

区别主要来自泛型规则：Shared 声明/方法位置省略子句通常代表任意元数，
Standard 的字面名称省略子句代表零元数。因此 Standard 的 `!Run` 只排除非泛型 Run；
要排除 `Run` 和 `Run<T>`，应写 `!Run<?>`。

**原因：保留有效能力、设计统一。** 路径否定可以简洁表达排除规则；
独立路径和名称 AST 节点让每个名称的泛型约束都有归属，不需要复现 Shared 的 token 重解释。

### 2.3 声明类型和成员名称各自支持正常 Boolean 组合

Shared 接受过 `(Owner.Run || Owner.Foo)`、`Owner[].Run`、`Owner?.Run` 等签名内部写法，
但接受不等于按预期匹配：历史 Boolean 路径分离只从右分支抽取方法名。
Standard 按用户确认的 N02 扩展为两处独立组合，并支持完整声明类型结构。

```text
method(* (A || B).(Run || Stop)(..))
method(* A.(Run<int> || Run<string>)(..))
method(* int[,].Get(..))
method(* int?.GetValueOrDefault(..))
```

前两条分别组合声明类型、名称及每个名称叶节点自己的泛型约束。
后两条可以匹配多维数组实际的 Get 方法、`Nullable<int>` 的 GetValueOrDefault 方法，
不是把数组或 nullable 后缀忽略后匹配原类型。
旧的 `(Owner.Run || Owner.Foo)` 应按意图改为 `Owner.(Run || Foo)` 或外层选择器组合。

`tasklike` 在成员路径的声明类型位置仍是字面名称；返回、参数、泛型实参及 ctor/cctor 的声明类型模式使用 Task/ValueTask 形状语义。构造器要匹配名为 tasklike 的字面类型时使用限定名，例如 `Example.tasklike`。
例如 `tasklike<int>+.Get` 的声明类型名称是 tasklike，限定名 `Example.tasklike<int>` 更明确。

**原因：功能增强、bug 修复。** 对两个不同匹配维度分别组合，避免从类型分支里猜测成员名；
数组、nullable 等在不同类型位置使用相同结构语义。

### 2.4 属性名和 namespace 不接受泛型子句

Shared 对这些子句有部分解析/匹配行为；旧 Standard 又曾绑定后忽略它们。
当前 Standard 直接报 AN2011，定位无效子句（I01、I04）。

| 写法 | Standard |
| --- | --- |
| `property(* Owner.Value<>)`、`getter(* Owner.Value<!>)` | 报错，属性名称不能携带泛型子句 |
| `property(* Owner.(Value \|\| Other<int>))` | 报错，分支内的属性名也受约束 |
| `property(* Owner<int>.Value)` | 合法，泛型属于声明类型 |
| `Ns<int>.Owner`、`System<>.Int32` | 报错，namespace 不能携带泛型子句 |
| `Ns.Outer<int>/Inner<string>` | 合法，外层和内层都是类型段 |

**原因：约束收紧、bug 修复。** 属性自身没有方法泛型子句，namespace 也没有 CLR 泛型实参。
拒绝比忽略更明确，尤其不能让被忽略的子句引入随后没有实际约束的变量。

## 3. 泛型、变量和类型别名

### 3.1 泛型元数不再依赖名称位置或是否限定 namespace

Shared 的 genericIn/genericOut 使用不同规则：短类型名省略子句通常要求零元数，
限定名、声明类型和方法名省略子句通常允许任意元数。
Standard 在所有合法泛型位置使用以下同一套规则：

| 模式 | Standard 含义 |
| --- | --- |
| `Name`、`Name<!>` | 精确零元数 |
| `Name<>`、`Name<,>`、`Name<,,>` | 精确一、二、三元数，空槽不约束实参类型 |
| `Name<..>` | 任意正元数 |
| `Name<?>` | 任意元数，包含零 |
| `Name<int,>` | 精确二元数，第一槽为 int，第二槽任意 |
| 裸 `*` | 唯一的省略子句仍允许任意元数的名称通配例外 |

例如 `method(* Run(..))` 不匹配 `Run<T>`，应写 `Run<?>` 或指定子句；
`System.Collections.Generic.List` 不匹配 `List<int>`，应写 `List<>`、`List<..>`、`List<?>` 或 `List<int>`。
Shared 的 `<?>` 不具备这套统一的“包含零的任意元数”含义。

**原因：设计统一、功能增强。** 增加 namespace 或移动到返回/声明位置不应改变元数；
零元数、正元数、任意元数分别有明确拼写。

### 3.2 前置空槽保留位置，不丢弃元数

Shared 的 genericIn 曾丢弃开头空槽（N03）；Standard 每个空槽都占一个位置。

| 模式和候选 | Shared | Standard |
| --- | --- | --- |
| `List<,>` 对 `List<int>` | 匹配 | 不匹配，模式要求两槽 |
| `Box<,,>` 对两槽 Box | 匹配 | 不匹配，模式要求三槽 |
| `Dictionary<,string>` 对 `Dictionary<int,string>` | 不匹配 | 匹配，第一槽为任意类型 |

**原因：bug 修复。** 逗号描述的是槽位边界，不能因空槽在开头就少算一个参数。

### 3.3 实参具有完整结构，首槽否定也正常解析

Shared 的短名分支曾拒绝 `List<List<int>>`、`Dictionary<string,List<int>>`、`List<(int,string)>`，
写完整 namespace 后部分形式却能解析；Boolean 实参也可能抛异常。
Standard 不因短名/限定名改变实参语法能力（N06）。

```text
List<List<int>>
List<(int,string)>
List<int || string>
Dictionary<!int,string>
```

I03 已修复：只有整个子句为 `<!>` 时才是零元数；`List<!int>` 是一个否定类型实参，
可匹配 `List<string>`，不匹配 `List<int>`。第一槽与后续槽遵循相同类型 grammar。
`List<!!int>` 仍因连续否定报错，而不是因为首槽不能否定。

**原因：bug 修复、功能增强、设计统一。** 实参是类型模式节点，不能复用一种只处理名称或元数的捷径。

### 3.4 变量必须显式写 `$`，裸名始终是字面类型名

Shared 在泛型声明位置用裸名表示位置变量；Standard 只用 `$T` 表示变量。

```text
method($T Converter.Convert<$T>($T))
```

同一个 `$T` 约束返回类型、方法泛型实参和参数类型相同。
自 2026-10-21 的[双模式设计](../superpowers/specs/2026-10-21-generic-variable-modes-design.md)起，默认 Declaration 只引用候选自身声明槽位；显式 Capture 保留此前既绑定开放形参、也捕获封闭方法具体实参的能力，并受有限预算保护。下节涉及具体实参捕获的历史比较在当前调用中须选择 Capture。
若写 `Run<T>`，Standard 查找字面类型名 T，不会隐式创建变量。

Shared 的单泛型槽虚拟名称编码曾导致 `Echo1<T>` 不匹配，
返回/参数引用该隐式变量又可能抛 NotImplementedException；Standard 的显式变量也修复了这些历史路径。

**原因：设计统一、bug 修复。** 名称和变量不再靠位置猜测，也不会因为候选增加泛型形参而改变裸名含义。

### 3.5 重复变量是等式约束，Boolean 分支隔离

Shared 存在重复名字覆盖和局部绑定不完整的问题。Standard 中：

- 重复 `$T` 要求相等；显式 Capture 的 `Pair<$T,$T>` 匹配 `Pair<int,int>`，不匹配 `Pair<int,string>`。默认模式中它引用第零声明形参，不把不同自身槽位或具体类型视为相同声明。
- `&&` 共享已建立的约束；`||` 分支的首次绑定不能向外泄漏。
- 变量不能首次在否定中引入；已经绑定的变量可用于否定条件。

例如 `method(* Run<$T>(..)) && method($T Run<?>($T))` 共享 `$T`；
`method(* (Run<$T> || Stop)(..)) && method(* Run<$T>(..))` 因从 OR 分支引用变量报错。
方法名局部 Boolean 和声明类型 Boolean 采用同一作用域原则。

**原因：bug 修复、设计统一。** 绑定应表达一致性约束，不能被分支执行顺序或覆盖行为改变。

### 3.6 泛型形参身份由所属声明和位置决定

Shared 以名称构建泛型字典，类型和方法都有同名形参时可能抛 ArgumentException（N18）。
例如 `Base<T>` 中还有 `T Shadow<T>()`，两个 T 是不同声明的形参。
Standard 区分所属声明与位置，能够映射、匹配和格式化这些合法元数据。

**原因：bug 修复。** 名字只用于显示，不应充当跨声明的唯一身份；这不增加新的语言关键字。

### 3.7 别名始终对应 System 类型，显式泛型约束不丢失

Shared 只在部分裸单段分支展开 int/object 等别名；带子句或处于声明/构造位置时可能按字面名查询（N07）。
Standard 的别名在各位置保持一致，且与子句同时生效。

| 模式和候选 | Shared | Standard |
| --- | --- | --- |
| `int<!>`、`int<!>+` 对 System.Int32 | 不匹配 | 匹配 |
| `method(* object+.*(..))` 对 Owner.Run，Owner 继承 object | 不匹配 | 匹配 |
| `ctor(object+(..))` 对 Owner 的构造 | 不匹配 | 匹配 |

但 `int<>+` 不会因展开别名就丢掉“一元数”约束，它不匹配零元数的 int。
`void` 后的任何泛型子句，包括 `<!>` 和 `<?>`，均报 AN2011。
`nint`、`nuint`、`dynamic` 在比较的内建词表中都不是 primitive alias；裸 `null` 也不是空值类型占位符。

**原因：bug 修复、设计统一、约束收紧。** 别名只替换类型身份，不应该改变或抹掉作者写出的约束。

## 4. 数组、nullable、tuple 和可赋值匹配

### 4.1 普通类型模式不剥离候选数组

Shared 的普通命名模式先剥离数组再看元素，泛型实参位置也受影响。
Standard 依据候选原本的结构匹配；裸 `*` 仍是完整通配。

| 模式和候选 | Shared | Standard |
| --- | --- | --- |
| `int` 对 int[] | 可按元素匹配 | 不匹配 |
| `List<int>` 对 `List<int[]>` | 可按实参元素匹配 | 不匹配 |
| `*[]` 对 int[][]、`List<int>`[] | 内层通配曾退化为零泛型名字匹配 | 匹配，外层 rank 为一，元素为完整通配 |

`*<?>` 等带泛型子句的星号是具名类型模式，不是裸 `*`：
它不匹配数组或泛型形参，单段模式也不跨过未写出的嵌套层级。
显式数组模式按 rank 和层级匹配；这一轮审计没有发现数组结构匹配顺序的新差异。

**原因：设计统一、bug 修复。** 命名类型和数组是不同结构，不能为匹配方便而丢弃数组层级。

### 4.2 泛型实参中的数组留在该实参节点

Shared 曾把实参数组的 rank 提升到外层（N04）：

| 模式 `List<int[]>` 的候选 | Shared | Standard |
| --- | --- | --- |
| `List<int[]>` | 不匹配 | 匹配 |
| `List<int>`[] | 匹配 | 不匹配 |

**原因：bug 修复。** 前者是“int 数组的 List”，后者是“`List<int>` 的数组”，必须保留不同 AST 结构。

### 4.3 nullable 语义包含引用类型，并按后缀结构组合

Shared 主要识别 `Nullable<T>`，部分后缀组合中的 `?` 会混入字面名称。
Standard 中，非 nullable 值类型的 `T?` 表示 `Nullable<T>`，引用类型的 `T?` 与 T 具有相同 CLR 身份。

| 模式 | Standard 含义 |
| --- | --- |
| `int?` | `Nullable<int>`，不匹配普通 int |
| `string?` | 与 string 相同；不检查源代码 nullable annotation |
| `int?[]` | nullable int 的数组 |
| `*?` | 保留内部完整通配，按候选引用/nullable 结构求值 |

数组是引用类型；无法确定类型类别时可以得到 Unknown，而不是猜测其引用/值类型性质。

**原因：功能增强、bug 修复、架构取舍。** 模式贴合 CLR 类型身份，
不把各后端可能不同的可空引用注解当成统一身份契约。

### 4.4 tuple 简写只表示 ValueTuple，按逻辑元素匹配

Shared 的 tuple 简写同时包含 System.Tuple 和 System.ValueTuple；Standard 只表示 ValueTuple。
引用 tuple 应显式写 `System.Tuple<int,string>`。

Standard 的 `(int,string)` 匹配 `ValueTuple<int,string>`；空 `()`、单项 `(int,)`、
`(,int,string)`、`(int,,string)`、`(int,string,)` 均不作为有效 tuple 接受。
Shared 曾跳过空元素或尾逗号，把这些多项写法坍缩成两项 tuple（N09）。

对于八个 int 的 ValueTuple，Shared 把 CLR 第八槽 TRest 当成第八项，
匹配 `(int,int,int,int,int,int,int,ValueTuple<int>)`；
Standard 展开 Rest 链，匹配 `(int,int,int,int,int,int,int,int)`（N08）。
要表达原始 CLR 第八槽，使用显式 `System.ValueTuple<...>` 具名模式。
tuple 的数组、nullable 后缀也按结构执行，不会被忽略。

**原因：设计统一、bug 修复。** 简写对应逻辑值 tuple，而不是混合两种家族或泄漏 Rest 存储细节。
tuple 每项是类型，不能照搬参数/泛型中的合法空槽规则。

### 4.5 `+` 使用结构白名单，保留实参和名义继承关系

Shared 的一些 `+` 分支会向元素下沉、混入早先后缀的名字，复杂短泛型又受到解析限制。
Standard 对 `+` 的目标采用已确认的结构白名单（语言规范 §12.4）：

| 写法 | Standard |
| --- | --- |
| `Base+`、`Base<?>+`、`List<int[]>+`、`int?+` | 合法，目标是具名模式或一层具名 nullable |
| `int+[]` | 合法，数组的元素满足 int+ |
| `int+?` | 合法，nullable 包在可赋值模式外 |
| `int[]+`、`*+`、`(A \|\| B)+`、`(int,string)+`、`$T+`、`tasklike+` | 报 AN2010，目标结构不在白名单 |

继承遍历保留泛型替换。例如 Leaf : `Middle<int>`，`Middle<U>` : `Base<List<U>>`，
`Base<List<int>>+` 可匹配 Leaf；Shared 曾因短实参结构不能解析而拒绝该模式（N06）。
Shared 并非完全没有泛型继承替换，简单 `Base2<int>+` 在双方可以一致。

Standard 还匹配 SZ 数组的合成接口：`System.Array+`、`IEnumerable<int>+` 可匹配 int[]，
Shared 的数组剥离会限制这些结果。`int+` 不匹配 int[]。
`+` 不加入数组协变或泛型协/逆变；需要元素约束时应显式把 `+` 写在数组元素或泛型实参内。

**原因：设计统一、bug 修复、架构取舍。** 先明确 `+` 的目标是什么，再按继承/实现关系求值，
不把它扩大成所有 CLR 可赋值转换；具名目标内的复杂实参仍保留完整结构。

### 4.6 否定全通配始终得到空匹配集合

Shared 的类型独立入口 `!*` 已得到空集合，但返回、参数和路径位置有 IsAny 快捷路径，
使否定全通配反而匹配所有候选（N10）。Standard 各位置统一采用正常 Boolean 补集。

| 表达式和候选 | Shared | Standard |
| --- | --- | --- |
| `method(!* *(..))` 对返回 int 的 Run | 匹配 | 不匹配 |
| `method(* *(!*))` 对单个 int 参数方法 | 匹配 | 不匹配 |
| `method(* !*(..))` | 路径也有全通配泄漏 | 不匹配任何普通方法 |

**原因：bug 修复、设计统一。** `!` 的意义不能因所在位置或通配快捷分支而反转。
这是正常三态逻辑的一部分：Unknown 的否定仍是 Unknown。

## 5. 成员域、修饰符、ref、返回形状和特性

### 5.1 method、callable、execution 分层表达成员范围

Shared 的 method 排除属性访问器和构造，但仍包含事件访问器、运算符和转换（N13）。
Standard 明确三层范围：

| 候选类别 | method | callable | execution |
| --- | --- | --- | --- |
| 普通方法 | 是 | 是 | 是 |
| 属性/事件访问器、运算符、转换 | 否 | 是 | 是 |
| 实例及静态构造 | 否 | 否 | 是 |

例如真正的 add_Changed、op_Addition、op_Implicit 不能用 `method(* *(..))` 选中，
应按需求用 callable 或 execution。`ctor` 只选实例构造，`cctor` 只选静态构造；
两者声明类型的泛型约束都遵循统一元数规则，不保留 Shared 的位置例外。

I02 同时修正了适配器误分类：普通 `public int op_Fake()` 或普通名为 op_Implicit 的方法仍在 method 域。
分类依据有效运算符元数据，而不是 `op_` 前缀；保留真正的 checked 转换、C# 14 实例运算符，
也不误排除泛型声明类型和合法 `in` 运算符参数。

**原因：设计统一、功能增强、bug 修复。** 选择器名称明确表达候选域；
是否可以织入是调用方的判断，不能代替语言域定义。

### 5.2 权限使用 C# 短语，替代项用 `|`

Shared 接受 `privateprotected`、`protectedinternal` 和空格分开的权限 OR；
Standard 使用 `private protected`、`protected internal`，旧连写收到 AN4003。
一条签名最多一个正向权限或正向替代组，复合权限作为替代项时必须加括号。

```text
method(private|internal * *(..))
method((private protected)|(protected internal)|public * *(..))
method(!private protected * *(..))
```

第三条否定整个 private protected 权限，不是分别否定两个词。
否定权限不能与 `|` 混用；重复修饰符和 `static` / `!static` 冲突报错。
Shared 枚举名称曾泄漏为 any/none 修饰符，none 零值实际不约束；Standard 不接受这两个修饰符。
未提及的权限/static 维度继续不约束匹配。

**原因：设计统一、约束收紧、bug 修复。** 两个词可能组成一个权限，也可能意图二选一，
用短语和显式 `|` 分开表达；冲突不能靠默认值或执行顺序掩盖。

### 5.3 使用声明权限，不与外层类型权限相交

当前 Standard 没有 CompositeAccessibility 选项。public 方法即使位于 internal 类型内，
仍按该方法的 public 声明匹配 `method(public * *(..))`。
旧兼容方向曾提供权限相交；这项选项已明确删除。
固定 Shared 基线也按成员声明权限处理；这里说明的是 Legacy 选项退役后的边界，
不把共同的默认行为计为新的 Shared/Standard 差异。

属性元数据仍合并 accessor 权限，但 property/getter/setter 选择器检查当前 accessor 的权限。
例如 public getter、private setter 的属性，`property(public * Name)` 只选择 getter。

**原因：架构取舍、设计统一。** 声明权限是明确元数据事实，“整个外部可见范围”需要额外策略，
不再把兼容选项混入成员权限规则；属性与访问器的职责仍保持区分。

### 5.4 ref kind 显式匹配，省略时不限制

Shared 的普通具名参数模式通常不匹配 by-ref；显式 ref/out/in 在部分解析路径还会被误当成多个槽。
Standard 将 ref kind 与类型结构分开，返回值支持 ref/ref readonly，参数支持四种明确 kind。

| 写法 | Standard 含义 |
| --- | --- |
| `method(* Take(int))` | 一个 int 参数，kind 不限制，可为 by-value/ref/out/in/ref readonly |
| `method(* Take(ref int))` | 只匹配 ref int |
| `method(* Take(out int))`、`Take(in int)`、`Take(ref readonly int)` | 各匹配对应 kind |
| `method(* Take(!ref int))` | 只匹配 by-value int |
| `method(ref readonly int Read())` | 匹配 ref readonly 返回 |

返回位置不接受 out/in；`!ref` 的两个 token 必须邻接，`! ref`（含制表符或换行）报 AN2005；`!ref` 不能和显式 kind 合用，也没有 `!out` / `!in` 语法。
参数空槽仍为任意类型且精确计数：`(int,)` 是两槽，`(..)` 是任意参数数目。

**原因：bug 修复、功能增强、设计统一。** 未写出的维度不应隐含限制，
作者需要 by-value 时用 `!ref` 明确表达；by-ref 不应改变类型名或槽位计数。

### 5.5 用 tasklike 表达返回形状，删除 async 返回糖

Shared 的历史 `async T`、`async null`、`async void` 是返回类型糖，并不检查编译器 async 标记。
Standard 删除这套拼写，按意图迁移为：

| 历史意图 | Standard 写法与范围 |
| --- | --- |
| Task/ValueTask 的结果为 int | `method(tasklike<int> Load(..))` |
| 无结果的 Task/ValueTask | `method(tasklike Flush(..))` |
| 实际 void 返回 | `method(void Fire(..))` |

tasklike 同样与编译器 async 标记无关；一个普通方法返回 `Task<int>` 也可匹配。
`tasklike<void>` 报 AN4004，应使用裸 tasklike。
裸 tasklike 不等于 void，自定义 awaitable 不在当前形状语法范围内。
`MethodSignature.IsAsync` 保留为通用元数据，但不是已删除的 async 语法条件。

**原因：设计统一、架构取舍。** 返回形状与编译器实现方式是两个事实，
直接写形状更明确，也无需维护 null/void 三分历史糖。

### 5.6 属性类型、名称来自关联 Property 元数据

Shared 用 getter 返回值或 setter 参数 0 推断类型，再裁剪 get_/set_ 前缀推断属性名。
Standard 使用关联 PropertyType 和 Property.Name。

| 候选与模式 | Shared | Standard |
| --- | --- | --- |
| `string this[int index]` 的 setter，`setter(string Item)` | 不匹配 | 匹配 |
| 同一 setter，`setter(int Item)` | 匹配，误把 index 当属性类型 | 不匹配 |
| 合法 IL 属性 Real 的 getter 叫 get_Alternate，`getter(* Real)` | 不匹配 | 匹配 |
| 同一 getter，`getter(* Alternate)` | 匹配 | 不匹配 |

要匹配访问器方法名称，应使用 `callable(* get_Alternate(..))`；属性选择器的名字位置表示属性名。

**原因：bug 修复、设计统一。** 索引器的 setter 参数 0 是索引，访问器名称也不必遵守 C# 惯例；
已有真实属性元数据时不应再用猜测代替。

### 5.7 特性位置扩展，exec 的旧含义保留

Shared 有 type、exec、para n、para *、ret、*；Standard 保留这些短形式，
增加 call、prop、方括号参数索引和 any。

| Standard 写法 | 查询位置 |
| --- | --- |
| `attr(call MarkerAttribute)` | 当前方法；候选为 accessor 时只查 accessor 方法 |
| `attr(prop MarkerAttribute)` | 关联属性 |
| `attr(exec MarkerAttribute)` | 当前方法；accessor 时再查关联属性，保留 Shared exec 意图 |
| `attr(para[0] MarkerAttribute)` / `attr(para 0 MarkerAttribute)` | 参数 0，两种写法都保留 |
| `attr(para[*] MarkerAttribute)` / `attr(para * MarkerAttribute)` | 任一参数 |
| `attr(type ...)`、`attr(ret ...)`、`attr(any ...)` / `attr(* ...)` | 声明类型、返回值、任一受支持直接位置 |

`getter(* Value) && attr(call MarkerAttribute)` 表达 getter 方法自身的特性；没有独立 get/set 特性位置。
位置查询保持直接特性语义，不实例化特性对象，也不自动增加继承特性规则。
非法索引报告诊断；索引必须为非负 Int32。
长位置名 callable/property/parameter/return 收到 AN4005，改用 call/prop/para/ret。
这项迁移诊断不表示固定 Shared 曾支持全部长名。

**原因：功能增强、设计统一。** 分别表达方法和属性位置，避免改变既有 exec 的范围；
参数旧写法不因新增方括号写法而被删除。

### 5.8 特性精确查询与通配查询采用相同泛型约束

Shared 的限定名称省略子句可匹配泛型特性。Standard 无论走精确直接查询还是模式回退，
具名省略子句都要求零元数（N14）。
假设方法带 `Probe.GenericMarkerAttribute<int>`：

| 模式 | Shared | Standard |
| --- | --- | --- |
| `attr(exec Probe.GenericMarkerAttribute)` | 匹配 | 不匹配 |
| `attr(exec Probe.GenericMarkerAttribute<int>)` | 按对应约束 | 匹配 |
| `attr(exec Probe.GenericMarkerAttribute<?>)` | 没有统一的 AnyArity 语义 | 匹配 |

`GenericMarker*` 也省略为零元数；完整名段为裸 `*` 的通配例外保留任意元数，例如 `attr(exec Probe.*)`。

**原因：设计统一、bug 修复。** 查询优化不能改变语言含义；写得更精确不应意外得到不同泛型范围。

## 6. 正则匹配的文本、解析和运行行为

### 6.1 一个明确的规范签名格式

Shared 使用紧凑逗号、by-ref 的 `&` 和连写复合权限；Standard 使用逗号加空格、ref 前缀和 C# 权限短语。
下面是同一开放泛型方法的已验证文本：

```text
Shared:   public T3 A.B.GenericOuter<T1,T2>.Echo2<T3,T4>(T3,T4)
Standard: public T3 A.B.GenericOuter<T1, T2>.Echo2<T3, T4>(T3, T4)
```

Standard 参数示例为 `ref System.Int32`，而非 `System.Int32&`；
复合权限为 `protected internal`，而非 `protectedinternal`。
全局 namespace 不再多输出点：`GlobalOwner.Run()`，而非 `.GlobalOwner.Run()`（N17）。

双方均按位置重命名泛型形参：只存在一槽时为 T，多槽为 T1..TN，外层类型、内层类型、方法依次编号。
没有新增的“Shared 外层逆序”差异；编译器 async 标记也不在双方的规范正则签名内。

**原因：设计统一、bug 修复。** 固定文本使各后端结果稳定；保留 positional 名称，避免源代码重命名形参破坏正则。
旧 regex 必须检查空格、权限和 ref 文本，不能仅保证仍可解析。

### 6.2 混合数组 rank 的正则文本变化不是结构匹配变化

对于“外层一维数组、元素是二维 int 数组”的候选，历史文本是 `System.Int32[][,]`，
当前规范格式递归元素后输出 `System.Int32[,][]`（N17）。
类型模式按 C# 的层级读法写 `int[][,]`；不能把当前正则签名文本直接复制为该类型模式。
原始对照中的数组结构匹配结果三方一致，变化在正则字符串表示。

**原因：设计统一。** 规范格式有固定元素递归规则；明确区分文本格式和结构，避免误报成数组匹配顺序 bug。

### 6.3 正则采用受限功能子集

Standard 保留正则原文，但只支持字面量和转义、普通字符类与范围、普通分组和 `(?:...)`、
分支、锚点、贪婪及惰性量词。注释、所有内联选项（包括 `(?x)` 和 `(?i)`）、
命名/平衡/条件/原子分组、前后断言、反向引用，以及嵌套或减法字符类均在编译时以 AN2012 拒绝。
类内字面方括号必须转义，不再接受初始字面 `]` 或 `[[]` 约定。
空白和 `#` 始终按字面量处理；非法 .NET 正则语法在编译时以 AN3002 报错。

| 表达式 | 当前 Standard |
| --- | --- |
| `regex((?#[)Run)` | AN2012：不支持注释 |
| `regex((?x)Run)` | AN2012：不支持内联选项 |
| `regex([])])` | AN2012：类内字面 `]` 必须转义 |
| `regex([\])])` | 合法，匹配字面 `]` 或 `)` |
| `regex((?:Run|Stop))` | 合法，支持非捕获分组和分支 |

**原因：语言简化。** 2026-10-10 采纳受限子集，替代此前支持完整 .NET 字符类及注释边界的决定。
外层边界只需要处理转义、普通字符类和分组深度。历史审计文档中的对应正例保留其固定版本事实，
迁移以当前规范第 15 节为准。

### 6.4 固定 culture，默认有限超时

Shared 使用默认 Regex culture；Standard 使用 CultureInvariant，并禁止通过内联选项改变匹配规则。
需要有限范围的大小写替代时可使用明确的字符类，例如 `[iI][nN][tT]32`。
Standard 默认单次正则超时250ms；可通过 AspectNMatchOptions.RegexTimeout 配置正有限值或显式 TimeLimit.Unlimited。超时返回局部 Unknown/AN3003；文本和总工作超限则整次 Unknown/AN3008，详见[资源规范](../superpowers/specs/2026-10-22-configurable-resource-limits-design.md)。
普通量词仍可能引起昂贵回溯，因此功能限制不取消超时。

**原因：设计统一、运行可靠性。** 相同规则不能随机器 culture 改变，昂贵正则不能阻塞整个匹配流程。

## 7. 会影响迁移的架构和元数据差异

以下与迁移有关，但不把它们混同为新增语法糖。

### 7.1 诊断及三态结果替代异常和简单 Boolean

Shared 的解析/求值可能抛 ArgumentException、RegexParseException、NotImplementedException 等，
正常匹配以 Boolean 表示。Standard 解析/绑定失败返回定位诊断；匹配返回 Matched、NotMatched、Unknown。

例如继承模式需要一个缺失程序集的定义时，不能证明“不满足”，应保留 Unknown 和原因。
Unknown 取反仍是 Unknown；Unknown OR Matched 得到 Matched，Unknown AND NotMatched 得到 NotMatched。
取消请求继续以取消异常传播，不转换为不匹配。

真正指针/function pointer 是不支持的类型结构；`Int32*` 仍是名称通配，不是指针声明语法。
对真正 int*，Shared 可能根据 Cecil 名称匹配；Standard 的具名查询可返回 Unknown:AN3001，裸 `*` 仍可匹配。
同样没有 params/scoped 的专用模式语法。

**原因：bug 修复、架构取舍。** “已知不满足”和“没有足够事实”不同；异常或未知不能被当作普通 false。
调用方需要明确自己的 Unknown 策略。

### 7.2 中立签名、生命周期和缓存隔离

Shared 直接依赖 Cecil，静态缓存持有类型/方法；Standard 将 Cecil、Roslyn、Reflection 映射为中立签名，
编译缓存有界且按源文本、解析入口、变量模式及全部解析/匹配配置字段值隔离；只发布完成结果。
适配器缓存受上下文生命周期约束，并保留程序集卸载覆盖。
例如使用 `CecilMappingOptions(leaveOpen: true)` 时，模块仍由调用方关闭。

**原因：架构取舍。** 同一语言不绑定单一后端，不让长期缓存阻止程序集卸载。
Legacy 与语言选择 API 已删除，不再增加模式维度；Rougamo 实际迁移和破坏性大版本发布属于后续任务。

### 7.3 不提供自定义匹配语义

Shared 有公开的任意 type-prefix parser 注册点，但默认 MatcherFactory 的选择器是固定 switch。
Standard 只支持内建选择器，不提供自定义 selector/predicate/matcher 或扩展注册表。
`vendor.feature(...)` 和未知非限定选择器在解析期统一报 AN2001；原 AN2008 保留为空号，不重新分配。

**原因：范围裁决（2026-10-05）。** 当前产品不需要自定义匹配语义，删除此前的固定参数扩展设计和实现。
自建 Shared parser 子类或注册任意前缀的消费者，其自定义语言仍不在内建兼容范围内。

### 7.4 已记录的后端能力边界

- Cecil 的 unresolved MethodReference 使用逐项 MetadataAvailability；类别、权限、特性和 ref 分类不可读时按查询传播 Unknown，
  已知的名称、形状和数量仍可判断。此前默认类别的局限已由 2026-10-05 裁决替代，见 [元数据可用性](metadata-availability.md)。
- Cecil/Reflection 可识别 VB 的 op_Exponent、op_IntegerDivision、op_Concatenate、op_Like 等合法运算符，
  C# Roslyn 的元数据导入器仍把它们视为 Ordinary。这是既有后端分类边界，不是 Shared/Standard 的语法裁决。
- Roslyn 4.12 对 C# 14 实例运算符的分类，由适配器按精确编译器功能标记和有效形状补齐；普通同名方法仍属普通域。

这些边界已有文档及回归覆盖，本次不把它们包装成语言增强，也不扩展公开元数据可用性契约。

## 8. 哪些历史差异已被消除，哪些边界仍相同

| 项目 | 当前状态 |
| --- | --- |
| N01：Standard 丢失路径前置否定 | 已恢复；剩余差异按统一泛型元数和作用域解释 |
| N02：Standard 只支持简单声明名称路径 | 已扩展为完整声明类型模式和成员名称 Boolean；不复现 Shared 的错误路径拆分 |
| N16：Standard 允许连续 `!!` | 已撤回该增强，双方顶层均拒绝；Standard 保留显式括号分组 |
| I01：属性名泛型被丢弃 | 已修复，当前报 AN2011 |
| I02：普通 op_ 方法误分类 | 已修复，按有效元数据分类 |
| I03：首槽 `!type` 被误当零元数 | 已修复，首槽正常否定；`<!>` 仍为零元数 |
| I04：namespace 泛型约束被忽略 | 已修复，当前报 AN2011 |
| 普通名字中的 `*` | 双方仍按零个或多个名称字符匹配，区分大小写 |
| 参数 `()` / `(..)` 与合法空槽 | 仍为零参数 / 任意参数 / 精确占位；不与 tuple 空元素混淆 |
| attr 的直接查询意图、exec 联合 accessor/property | 保留，不增加特性实例化或继承查询 |
| 简单泛型继承替换、泛型位置编号 | 已有能力保留，不声称 Shared 完全不支持 |

## 9. 裁决编号索引与迁移入口

| 原编号 | 本文位置 |
| --- | --- |
| N01 / N02 | 2.2 / 2.3 |
| N03 / N04 / N05 / N06 | 3.2 / 4.2 / 2.1 / 3.3、4.5 |
| N07 / N08 / N09 / N10 | 3.7 / 4.4 / 4.4 / 4.6 |
| N11 / N12 / N13 / N14 | 5.6 / 5.6 / 5.1 / 5.8 |
| N15 / N16 / N17 / N18 | 1.3 / 1.4 / 6 / 3.6 |
| I01 / I02 / I03 / I04 / §6.2 | 2.4 / 5.1、7.4 / 3.3 / 2.4 / 1.3 |

迁移时应同时检查表达式是否有效和实际匹配范围；“能解析”不意味着与 Shared 结果相同。
具体 API、模块生命周期和表达式改写见 [迁移指南](../migration/shared-cecil-to-aspectn.md)。
历史裁决基线 `084dfb6` 的默认和替代依赖组合各有 952 项测试通过；这是当时的测试数，不是当前 HEAD 的数量，也不是审计表达式数。当前验证见 [2026-10-05 修复记录](../reviews/2026-10-05-review-ds-remediation.md)。

## 后端元数据能力边界（2026-10-05）

`IsAsync` 表示编译器标记的异步方法（包括异步迭代器）。Roslyn 源符号的标志和 PE 中的 AsyncStateMachine/AsyncIteratorStateMachine 标记合并；`tasklike<T>` 仍只看返回类型，与该标志无关。普通指针统一显示为 CLI 名称，如 `System.Int32*`，结构匹配仍为 Unknown。

函数指针 owner 身份使用 modified parameter Type 读取调用约定，包含 Cdecl/Stdcall 及 SuppressGCTransition。原始 Type 的空约定列表不能证明 managed。不可读取约定时使用未知标记，并附加模块 MVID 与方法 token 的 `#declaration:` 身份。没有 IsFunctionPointer 的旧 CLR（实测 .NET 7）还可能把函数指针暴露成 IntPtr，因此含这种不可辨形状的 owner 同样保留物理声明身份。它保证同一 PE 声明在不同 mapper 中一致、不同重载不合并，但不能与其他后端的规范 owner 字符串直接比较；旧 CLR 的 IntPtr 类型视图不能恢复原函数指针形状。需要完整 CLI 形状时使用 Cecil/Roslyn，或具备现代反射 API 的运行时。

Reflection 从封闭声明类型取出的泛型方法形参 Type 仍可能隶属开放定义，不携带封闭声明上下文；其名义闭包保留声明约束。Cecil MethodReference 可以保留封闭 declaring-type 上下文，约束遍历会沿传递形参继续替换类型实参。

真实 CLI `.raise` 方法在 Cecil/Reflection 为 EventRaiser；当前 Roslyn C# PE 导入器可将其视为 OrdinaryMethod，且 IEventSymbol.RaiseMethod 为空。无 CompilerFeatureRequired 标记的合法实例复合赋值方法在 Cecil/Reflection 为 Operator，Roslyn 4.12 PE 为 OrdinaryMethod；替代矩阵的 Roslyn 5.6 已识别为 Operator。此边界由 emitted IL 回归固定；不按 raise_/op_ 名称猜测访问器，也不扩大 C# 编译器标记的适用范围。后端和版本选择会影响这些成员的选择器域。

官方反射 API 依据：[GetModifiedParameterType](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.parameterinfo.getmodifiedparametertype?view=net-10.0)、[GetFunctionPointerCallingConventions](https://learn.microsoft.com/en-us/dotnet/api/system.type.getfunctionpointercallingconventions?view=net-10.0)、[IsUnmanagedFunctionPointer](https://learn.microsoft.com/en-us/dotnet/api/system.type.isunmanagedfunctionpointer?view=net-10.0)。具体运行时覆盖见修复记录。
