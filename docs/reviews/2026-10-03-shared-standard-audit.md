# Shared.Cecil.AspectN 与单语言 AspectN 的完整差异审计

日期：2026-10-03。状态：**审计结果，新增取舍待用户确认；不是新增的已批准语言规范。**

Legacy 退役已提交为 `1fc3b44`（`refactor(core)!: retire Legacy and keep a single language contract`）。本次审计没有修改解析、绑定、求值或适配器行为。发现的实现缺陷不会以“设计如此”结案。

## 1. 比较对象、方法与证据

比较对象固定为：

| 对象 | 版本 |
| --- | --- |
| Shared.Cecil.AspectN | `03a9d1030f92a64449b6220eb45d7198d31653db` |
| 删除前 Legacy / Standard | `536f85dd2815ce77f145d6dc70260564f734cd4a` |
| 删除后单语言 Standard | `1fc3b44` |

旧构建的 `AssemblyInformationalVersion` 为 `1.0.0+536f85dd2815ce77f145d6dc70260564f734cd4a`，通过隔离的 AssemblyLoadContext 只读加载；没有恢复 oracle 项目、修改其缓存或修改相邻仓库。旧兼容政策与旧差异清单从该提交的 Git 对象读取，并核对了用户所指历史对话的最终审查结果。

方法包括 Shared 全部内建 parser / matcher / type pattern / signature 分支的源码检查，现有 Standard 规范、解析器、Binder、求值器及适配器检查，以及同一候选上的差分探针。覆盖不是只检查表达式能否解析：分别记录接受/诊断、Matched、NotMatched、Unknown 和异常。对于新增发现，用旧 Legacy、旧 Standard 和当前 Standard 再做具名对照，避免把“旧表没写”误称为“以前两版没有差异”。

最终矩阵：952 个基础类型表达式及其在返回/参数位置的组合，5,524 个不同入口/表达式，389 个类型候选，381 个成员候选，共 1,124,998 条比较。候选包含开放/封闭/嵌套泛型、泛型继承替换、混合 rank 数组、长 ValueTuple、ref、各权限、属性、索引器、事件、运算符、构造及特性。开放类型保留在类型入口；不把没有所属泛型形参的开放定义伪装成合法方法返回类型。

矩阵中 554,370 条结果或接受情况不同，Shared 求值异常 6,513 条，当前 Standard 求值异常 0 条。**这些是重复候选组合的行数，不是独立差异或缺陷数量。** 另有 196 个具名复现和实际 C# 程序集的三适配器验证。

原始精简证据见 [evidence.json](2026-10-03-shared-standard-evidence.json)，包含三方结果、旧 Standard 结果、构建来源、正则签名文本及三适配器结果。完整本地探针与 JSONL 位于 `artifacts/shared-standard-audit-20261003/`；它们没有进入解决方案、包或 CI。此次审计覆盖固定内建语法的所有分支及下述组合边界；有限探针不等于枚举无限的表达式和所有可能的 CLR 元数据。

196 个具名复现中，旧 Standard 与当前 Standard 只有 `CompositeAccessibility=true` 的隐藏声明类型案例不同，正是本次明确授权删除的选项。其余复现的接受与求值结果一致。

## 2. 全语法覆盖清单

标记说明：**A** = 已确认的 Standard 决策；**B** = 旧 Shared→Legacy 政策已批准、现 Standard 同样采用的修正/增强；**N** = 此次补充确认项，见第 4 节；**I** = 实现缺口，见第 5 节；**相同** = 本维度未发现独立设计差异。共享修正已经确认的部分不重复请求批准。

| 维度 | Shared | 当前 Standard | 归属 |
| --- | --- | --- | --- |
| 关键字大小写 | 选择器、权限、attr 位置不敏感；历史 async 严格小写 | 全部小写且区分大小写；类型/成员名字保持 ordinal | A |
| 标识符 | ASCII 字母、数字、下划线；可数字开头或全数字 | Unicode 字母/下划线起始及允许的续字符；拒绝数字名称 | A / B |
| Unicode 范围 | Unicode 名称不接受 | 当前 char lexer 接受 BMP 字母及组合/连接/格式续字符；不是任意 CLR 名称或所有 Unicode scalar 的转义语法 | B；精确边界说明 |
| 空白 | 空格、全角空格、tab、CR、LF | `char.IsWhiteSpace`；增加 NBSP、VT、FF、EM SPACE 等 | N15 |
| 名称 token 邻接 | 名字和路径累积遇空白停止 | 同样不能拆开名称、路径或普通泛型开头；选择器名与 `(` 间可有空白 | 相同 |
| 后缀/泛型邻接 | `[`、`?`、普通 `<` 紧邻；历史部分 `+` 宽松 | 普通 `<`、`[`、`?`、`+` 须邻接；rank 内可留空白 | A / B；tasklike 例外见 §6 |
| 单 `*`、`Name*`、`*Name*` | 名段内零或多字符；名字区分大小写 | 同类 ordinal 通配；不会把 `Int32*` 解释成指针类型语法 | 相同；数组剥离另列 |
| `.` / `..` / `/` | namespace 省略、嵌套路径；声明位置有特例 | `..` 仅省略 namespace 段，成员用 `.`，嵌套用 `/` | A |
| 全局/嵌套声明范围 | `*..` 前缀可含全局；genericOut 全通配含嵌套 | `*..*` 要有 namespace 段，显式单段类型路径不跨嵌套；省略声明类型含两者 | A |
| 前导和尾部空路径段 | 前导可接受；类型入口尾段空可接受但不命中 | 前导和尾部空段诊断；拒绝成员位缺声明类型名的 `*..M` | A / B |
| 短嵌套类型路径 | genericIn 的 `/` 路径可能拼错名字，不命中 | 独立/返回/参数位置按嵌套结构匹配 | N05 |
| 成员路径前置 `!` | 有实际可用名称/声明路径否定 | 不接受；旧 Legacy 也不接受 | N01 |
| 声明路径 Boolean/array/nullable 包装 | 某些结构可接受，可能不命中或走不完整分离逻辑 | 成员路径仅名称、泛型、分隔符及声明类型 `+` | N02；规范边界见 §6 |
| 顶层成员 Boolean | `!`、`&&`、`||`；无正常括号分组，双否定不完整 | 正常优先级、分组及连续否定 | B / N16 |
| 类型 Boolean | 两入口不同；签名内否定范围可吞后续表达式 | 两入口统一 `! > && > ||`，三态求值 | B |
| 返回/参数中的 `!*` | IsAny 快捷路径错误，可匹配所有类型 | 正常补集，不匹配任何类型 | N10 |
| 选择器词表 | method/execution/property/getter/setter/ctor/cctor/attr/regex | 同族，并新增 callable；不存在语言选择器模式 | A |
| method 域 | 排除 getter/setter/构造，但仍包含事件访问器、运算符、转换 | 只含普通方法 | N13；普通 op_ 名称另见 I02 |
| callable / execution 域 | 无 callable；execution 包括构造 | callable 含非构造成员；execution 再含构造 | A |
| ctor / cctor | ctor genericOut、cctor genericIn，省略 arity 含位置特例 | 各自匹配实例/静态构造，类型约束执行同一 Standard 规则 | A；不混淆二者 |
| 权限和 static | 连写复合权限、空格 OR、重复/冲突可接受 | C# 复合短语，`|` alternatives，组约束和冲突诊断 | A |
| any / none 修饰符 | enum 名泄漏；none 的零值实际不约束 | 不是修饰符 | B |
| 未提及修饰符维度 | 不约束 | 不约束；成员声明权限，属性选择器仍看当前 accessor 权限 | 相同；Composite 删除为 A |
| 历史 async | async T/async null/async void 三分糖，不检查 compiler async flag | 以 tasklike/result/void 表达，删除 async 糖 | A |
| tasklike | 普通类型名 | Task/ValueTask 返回形状；tasklike<void> AN4004 | A；空白结果子句见 §6 |
| 省略泛型子句 | genericIn 裸单段通常零，限定名/成员/genericOut 通常任意 | 具名零；裸 `*` 任意；无位置双轨 | A |
| `<!>`、`<>`、`<,>`、`<,,>`、`<..>` | 有这些形式，但 genericIn 前置空槽丢计数 | 零/一/二/三/正元数，空槽精确占位 | A / N03 |
| `<?>` | 不具备 Standard 的统一 AnyArity 含义；某些位置可接受但非该语义 | 一切合法泛型子句位置均为任意元数含零 | A |
| 混合泛型空槽 | genericOut 的位置声明可用；genericIn 开头空槽有错误 | 所有位置精确计数、空槽 Any | A / N03 |
| primitive alias 子句/位置 | 某些带子句写法及 genericOut 声明位置按字面 int/object 等名字，不展开 System 别名 | 别名无位置双轨；子句与别名同时生效；void 的任意子句 AN2011 | N07 / A |
| 泛型实参结构 | 短名嵌套泛型、tuple、Boolean 等有不完整解析 | 普通类型模式组合；首槽直接 `!` 尚有缺陷 | B / N06 / I03 |
| 泛型实参数组 | 可能把实参数组 rank 提升到外层 | rank 留在对应实参节点 | N04 |
| 隐式/显式变量 | 泛型声明裸名为位置变量，无 `$T`；单槽与返回变量另有历史异常 | 仅 `$T`，可绑定形参或具体类型；裸名永远字面类型名 | A / B；历史异常见 §7 |
| 重复变量和 Boolean 作用域 | 重复名覆盖、局部绑定行为不完整 | 等式约束；AND 共享，OR 分支隔离，negation 不引入新变量 | A / B |
| namespace 泛型子句 | 子句可拼入 namespace 导致不命中 | 当前忽略子句，甚至绑定未经求值的变量 | I04 |
| 属性名泛型子句 | 有些形式影响匹配 | 当前静默丢弃，违反 property signature 契约 | I01 |
| 裸命名模式与数组 | 剥离候选数组后看元素，包括泛型实参位置 | 普通 named/tuple/nullable 模式不剥数组；裸 `*` 仍全通配 | A |
| 显式数组 rank/层级 | 按 rank/层级；后缀 `*` 内层变零泛型名字通配 | 按结构；内层 `*` 保持全通配，`*[]` 可命中更深层数组 | A；混合 rank 匹配顺序本次一致 |
| nullable | 只看 Nullable<T>；若干后缀更早的 `?` 混入字面名字 | 引用类型 CLR 身份不变；非 nullable 值类型包装；结构组合后缀 | A / B |
| tuple 家族/最低项数 | Tuple 与 ValueTuple；单/空 tuple 可接受 | 仅 ValueTuple，至少两项；System.Tuple 显式写名 | A / B |
| tuple 空槽与尾逗号 | 逗号可被跳过、静默坍缩元素数 | 拒绝缺元素；区别于有效参数/泛型空槽 | N09 |
| 长 tuple Rest | 直接按 CLR 的 8 个参数看 TRest | 将 ValueTuple Rest 链扁平化为逻辑元素 | N08 |
| tuple 后缀 | 返回位置拒绝或独立入口忽略 | array/nullable 后缀正常执行结构语义 | B |
| `+` 目标和组合 | 名字/元素下沉，早期后缀可混入字面名 | 已批准结构白名单；命名目标实参内结构保留；不增加 variance | A / B |
| `+` 闭包替换与数组祖先 | 普通继承支持；复杂泛型解析/数组剥离限制结果 | 保留泛型实参替换，并含 System.Array 与 SZ-array 接口 | N06；数组祖先规则已有 A |
| 参数列表 | `()` 零、`(..)` 任意、空槽 Any；部分缺逗号被忽略 | 同类精确计数和顺序，拒绝缺逗号/多余 token | 相同 / B |
| ref kind | 普通具名比对不匹配 by-ref；显式 ref 参数被误解析为两个槽 | 未标注不约束；显式四 kind 精确、!ref 仅 by-value；out/in 返回非法 | A / B |
| 属性 getter/setter 类型 | getter 取返回类型；setter 取参数 0 | 取关联 PropertyType，索引器 setter 正确 | N11 |
| 属性名称 | accessor 名去 get_/set_ | AssociatedProperty.Name | N12 |
| attr 位置 | type/exec/para n/para */ret/* | 保留旧短形式；另有 call/prop/para[n]/para[*]/any | A |
| attr exec/type/ret 和参数枚举 | 直接声明位置；exec 包含 accessor 及 property | 同类直接位置语义；不实例化或自动查询继承特性 | 相同 |
| attr 泛型和查询路径 | 精确名同样按模式、限定名省略可命中泛型特性 | exact 与 fallback 均按 Standard arity；省略为零 | N14 / A |
| attr 非法索引/长目标 | 解析异常或不接受 | 非负 Int32 索引诊断；长目标 AN4005 | A；诊断 API 见 §7 |
| regex 格式 | 无 async 标记；紧凑逗号、&、连写权限 | spaced 逗号、ref 前缀、C# 权限；两者均位置泛型编号 | A；补充文本差异见 N17 |
| regex lexical/culture | 手写括号扫描，默认 Regex culture，无有限超时 | .NET 分组/类/注释边界，CultureInvariant，有限 timeout→Unknown | N17；超时/诊断已有 A |
| 扩展与 API | 默认 MatcherFactory 固定选择器词表；可注册 type prefix、消费其 token | registry 可提供 qualified selector/predicate，使用固定 scalar/type 参数，不提供任意 type prefix | 已有架构契约；§7 明确边界 |

`nint`、`nuint`、`dynamic` 在本次固定的两个内建词表中均不是 primitive alias；不把它们当作本次新增关键字。Shared 无字符串/整数/Boolean 扩展参数的统一 grammar，Standard 的 scalar grammar 属于固定扩展契约；不能拿外部插件任意定义的 body 当成 Shared 内建语法。

## 3. 已批准的共享修正，不重复确认

删除前 Legacy 已经偏离 Shared，且旧兼容政策明确批准了以下方向：

- Unicode 名称、显式 `$T`、ref/ref readonly 返回及顶层成员括号分组。
- 重复泛型变量采用相同类型约束；类型否定作用域及两入口 Boolean 优先级修正。
- 拒绝空/单项 tuple、前导路径分隔符、非邻接 `+`、多余 token 和参数缺逗号。
- 拒绝泄漏的 any/none 修饰符；历史 async 的特殊大小写不放宽。
- tuple 后缀、泛型实参 Boolean 组合、ref/out/in 参数按 kind 匹配。
- `int+[]`、`int+?`、`int?+` 采用结构语义；裸 null 作为实际类型名。

例如 Shared `List<int || string>` 抛 ArgumentException，旧 Legacy 和两代 Standard 可以编译；这已经批准。下面 N06 只补充此前未逐项说明的短名嵌套泛型、tuple 实参和位置不一致，不把 Boolean 增强重新请求批准。

## 4. 新增差异与待确认取舍

以下 N 项未在旧 Legacy/Standard 差异表中完整体现。除另外注明外，旧 Legacy 和旧/新 Standard 在复现中的结果相同，正是旧两模式对比不能发现的 Shared 差异。建议中的“保持”均为待确认建议，没有写入规范为已批准决定。

### N01：有效的成员路径前置否定没有迁入

Shared 的 `method(* !Run(..))` 对 Run 不匹配、对 Foo 匹配；`method(* !Owner.Run(..))` 对“Owner 声明类型且名称 Run”的合取取反。`getter(* !Prop)` 同样有效。旧 Legacy 和当前 Standard 全部 AN1001。

这是 Shared 的真实可用能力，不能归入 Shared 无效输入。来源是 `NotTypePattern.ToDeclaringTypeMethod` 与 `NotDeclaringTypeMethodPattern.IsMatch`；当前 `SelectorParser.ParseMemberPath` 不接收 BangToken。

**待选：**明确删去签名内部的路径否定、使用外层 Boolean 组合；或者恢复这一能力并明确变量、arity 和声明路径范围。建议采用外层组合、收紧成员路径 grammar，但必须由用户确认。比如 Shared `method(* !Run(..))` 可用 `method(* *(..)) && !method(* Run<?>(..))` 表达名称否定；对照真实 event/operator 时还必须考虑 N13 的域变化，不能声称全语言等价。

### N02：其他声明/成员路径包装的接受范围

Shared 接受 `method(* Owner[].Run(..))`、`method(* Owner?.Run(..))` 及 `method(* (Owner.Run || Owner.Foo)(..))`；复现中均不命中普通 Owner.Run/Foo。旧 Legacy 和 Standard 直接 AN1001。Shared 的 Boolean 路径在分离方法时只从右侧抽取名称，不能理解成常规路径 OR。

**建议确认：**签名中的声明路径只支持名字、泛型、namespace/嵌套分隔符和声明类型 `+`；Boolean 放到完整选择器外层，array/nullable 声明包装不提供。与 N01 的有效否定能力分开裁决。`$T.Run` 三者都拒绝，不是 Shared 曾有的能力。

### N03：genericIn 的前置空槽不再丢失

| 模式和候选 | Shared | Legacy / Standard |
| --- | --- | --- |
| `List<,>` 对 `List<int>` | Matched | NotMatched |
| `Box<,,>` 对两槽 Box | Matched | NotMatched（要求三槽） |
| `Dictionary<,string>` 对 Dictionary<int,string> | NotMatched | Matched |

Shared 的 CompileGenericIn 遇开头 `<` 只在参数栈为空时补 Any，前置空位被丢弃；genericOut 的声明位置并不是这个错误。**建议确认修正：**所有逗号均占位置，纯空/混合空槽采用 Standard 精确元数。

### N04：泛型实参数组不会提升到外层

Shared `List<int[]>` 对 List<int[]> 不匹配、对 List<int>[] 匹配。旧 Legacy / Standard 相反。Shared CompileGenericIn 复用外层 ranks 栈，实参数组后缀落到了外层。

**建议确认修正：**array 属于对应实参 AST 节点；`List<int[]>` 与 `List<int>[]` 严格区分。另一个已批准的数组剥离规则也会影响实参：Shared `List<int>` 可命中 List<int[]>，Standard 不会；该部分不重复确认。

### N05：genericIn 短嵌套类型路径正常工作

`Outer/Inner`、`A.B.Outer/Inner` 对 A.B.Outer/Inner，Shared NotMatched，Legacy / Standard Matched。Shared genericIn 在遇 `/` 后没有正确清理已提取名字/泛型状态，后续名字被重复拼接。声明 genericOut 不应由此推断同样错误。

**建议确认修正：**所有类型位置按 `/` 的明确嵌套层级匹配。

### N06：复杂泛型实参使用完整类型结构

Shared `List<List<int>>`、`Dictionary<string,List<int>>` 和 `List<(int,string)>` 会拒绝；Legacy / Standard 接受。反而 `List<System.Nullable<int>>` 和限定名的 `Dictionary<string,System.Collections.Generic.List<int>>` 可以在 Shared 解析，说明这不是统一禁止嵌套，而是短名编译分支不完整。

`Probe.Base<List<int>>+` 对 Leaf : Middle<int>，Middle<U> : Base<List<U>>，Shared 拒绝解析，Legacy / Standard 正确匹配；简单 `Base2<int>+` 的继承实参替换三者一致。不要误称 Shared 完全没有泛型继承替换。

**建议确认修正/增强：**普通、限定、嵌套泛型、tuple 和 Boolean 实参均保留结构，不因是否写 namespace 而改变语法能力。Boolean 部分已有批准。直接首槽 `!` 的未实现部分见 I03。

### N07：primitive alias 不再依赖子句或声明位置

`int<!>`、`int<!>+` 对 System.Int32：Shared NotMatched，Legacy / Standard Matched。Shared 仅特定裸单段 genericIn 分支展开 alias，显式子句走另一分支后按名为 int 的类型查询。

Shared 的 genericOut 声明类型/ctor 分支连裸 alias 也不展开：`method(* object+.*(..))` 对 Owner.Run（Owner : object），以及 `ctor(object+(..))` 对 Owner 的构造，Shared 均 NotMatched，Legacy / Standard 均 Matched。这不是仅显式子句触发的差异。

**建议确认修正：**别名始终指对应 System 类型，显式子句同时保留 arity/实参约束；不能为了 alias 丢掉子句。`int<?>` 的 Standard 新含义及 void 子句拒绝已经在原决策确认，不重提。

### N08：长 ValueTuple 使用逻辑元素，TRest 不作为第八项

对八个 int 的 ValueTuple（CLR 第八槽是 ValueTuple<int>）：Shared `(int,int,int,int,int,int,int,int)` 不匹配，而 `(int,int,int,int,int,int,int,ValueTuple<int>)` 匹配。Legacy / Standard 恰好相反，因为扁平化了 Rest 链。

**建议确认：**tuple sugar 按 C# 逻辑元素；若要观察 CLR 第八槽则写显式 `System.ValueTuple<...>` named pattern。只匹配 ValueTuple、排除 System.Tuple 已经确认。

### N09：tuple 中的空槽/尾逗号不被静默删除

Shared `(int,string,)`、`(,int,string)`、`(int,,string)`、`(,int,string,)` 均按两元素 tuple 匹配 ValueTuple<int,string>。Legacy / Standard 全部 AN1001。

**建议确认收紧：**tuple 每项必有类型，不允许空元素或尾逗号。有效的参数/泛型空槽继续保留。旧政策只逐项确认空 tuple 和单项 tuple，未完整说明这种多项坍缩。

### N10：返回/参数的否定全通配真正取补集

Shared `method(!* *(..))` 对返回 int 的 Run 为 Matched；`method(* *(!*))` 对单参数 int 方法也 Matched。Legacy / Standard 均 NotMatched。根因是 NotTypePattern.IsMatch 的 `InnerPattern.IsAny || !...`。

独立入口 `!*` 对 int 三者都 NotMatched，它用的是外层 NotTypeMatcher，不能从该入口推断签名内也正确。路径 `method(* !*(..))` 又有类似 IsAny 泄漏，但 Standard 本身不接受该路径，属于 N01 的语法裁决。

**建议确认修正：**否定全通配匹配空集合，所有位置遵循正常 Boolean 补集。

### N11：索引器 setter 按属性类型匹配

属性 `string this[int index]` 的 setter 是 set_Item(int,string)。Shared `setter(string Item)` 不匹配、`setter(int Item)` 匹配；Legacy / Standard 恰好相反。Shared PropertyPattern 使用 setter 的参数 0，把 index 类型当成属性类型。

**建议确认修正：**使用 AssociatedProperty.PropertyType，getter/setter 查询同一属性类型；当前 accessor 的权限约束保持已确认规则。

### N12：属性名称以关联 Property 元数据为准

有效 IL 可把属性 Real 的 getter 命名为 get_Alternate：Shared `getter(* Real)` 不匹配、`getter(* Alternate)` 匹配；Legacy / Standard 相反。

**建议确认：**property/getter/setter 的名字位置是属性名，按 PropertyDefinition/PropertyInfo/IPropertySymbol 的名称；要筛选 accessor 方法名可用 callable 与相应名称。普通 C# 的惯常 get_Prop 命名不会暴露此差异。

### N13：method 确实只选择普通方法

真实 add_Changed、op_Addition、op_Implicit 对 `method(* *(..))`：Shared Matched，Legacy / Standard NotMatched。Shared MethodPattern 只排除属性访问器和构造，未排除事件访问器、运算符及转换；旧表没有把这项 Shared→Legacy 的域缩小列出。

**建议确认既有 Standard 设计：**method 普通方法；callable 扩展到这些非构造成员；execution 再包含构造。普通名字恰为 op_Fake 的方法应仍属普通域，不在本项允许排除，见 I02。

### N14：精确泛型特性查询也采用零元数省略

方法带 GenericMarkerAttribute<int>。Shared `attr(exec Probe.GenericMarkerAttribute)` 为 Matched；旧 Legacy 和 Standard 都 NotMatched。Shared 走类型模式，限定名省略=任意元数；Legacy 的精确直接查询先收窄到零元数，已不是完整 Shared 行为。

Standard 的精确名与部分名称通配（如 GenericMarker*）省略子句均为零元数，具名查询包含泛型需明确 `<>` / `<..>` / `<?>` / `<int>`。裸 `*`（包括完整名段 Probe.*）仍沿用 AnyArity 例外，整体 `attr(exec *)` 也是全通配。Legacy 的通配 fallback 又与其精确查询不同，旧表已说明。

**建议确认：**保持 Standard 查询路径一致；补充确认 Shared 的精确查询也需要迁移，不能只给通配表达式添加泛型子句。

### N15：更完整的 Unicode 空白

`int` 后接 U+00A0 NBSP：Shared 拒绝，Legacy / Standard 接受并匹配 int。同类还包括 VT、FF、U+2003 等 char.IsWhiteSpace 接受的字符。

**建议确认增强：**所有常规语法间空白按 char.IsWhiteSpace；名字/后缀的邻接要求仍看原始源码位置，regex body 则原样保留。此前 Unicode 名称增强的批准没有逐项涵盖空白集合。

### N16：连续一元否定合法

`!!method(* *(..))` / `! !method(* *(..))`：Shared 拒绝，Legacy / Standard 正常编译并恢复双否定结果。

**建议确认增强：**一元运算可递归，空白不改变否定逻辑。顶层括号分组、类型否定优先级已有批准；这里补充 Shared 顶层只消费一次 `!` 的限制。

### N17：旧表没有列出的正则差异

| 子项 | Shared | Legacy / Standard | 建议 |
| --- | --- | --- | --- |
| 全局 namespace | `public System.Int32 .GlobalOwner.Run()`，多一个前导点 | `public System.Int32 GlobalOwner.Run()` | 保持没有空 namespace 前导点 |
| 混合 rank 的文本顺序 | 对外层一维/内层二维候选输出 `System.Int32[][,]` | 输出 `System.Int32[,][]` | 保持现 canonical 元素递归文本，文档写明该文本与类型模式的 rank 顺序不是同一读法 |
| 正则 culture | tr-TR 下 `regex((?i)int32)` 不匹配含 Int32 的签名 | 匹配；使用 CultureInvariant | 保持 culture invariant |
| 注释和初始字面 `]` | `regex((?#[)Run)` 拒绝；`regex([])])` 抛 RegexParseException | 两者接受并匹配 Run 签名 | 按 .NET regex 分隔规则消费注释/字符类 |

第二项只指 regex 字符串，**不是本次发现的数组匹配顺序变化**：同一候选在 `int[][,]` / `int[,][]` 下三方的结构匹配结果一致。该例候选的 Cecil FullName 是 int[,][]，C# 的类型模式读法为 int[][,]；不可用一个字符串代替结构。

泛型位置名并没有“Shared 外层逆序”的新差异。对外层两槽、内层一槽及方法两槽的实际签名检查后，Shared 仍最终按 T1/T2/T3/... 排列；内部倒序收集被 SortGenericParameter 抵消。已经确认的逗号空格、ref 前缀、复合权限渲染仍在 A 类。

### N18：同名的类型与方法泛型形参不再导致异常

Base<U0> 内的 Shadow<U0>() 返回方法形参 U0，Shared ParseMethod 因 ToDictionary 按名称建 genericMap 抛 ArgumentException；Legacy / Standard 按 owner/position 身份区分并正常匹配。这是合法泛型遮蔽元数据，C# 可用同名形参声明（有警告）。

**建议确认修正：**形参身份采用所属声明与位置；名字仅用于显示。这是元数据/求值能力差异，未增加语法关键字。

## 5. 四处实现缺口，不能批准为设计差异

### I01：属性名称泛型子句被静默丢弃

`property(* Prop<>)`、`Prop<..>`、`Prop<int>` 在 Shared 不匹配非泛型 Prop，当前却均编译并匹配；`Prop<$T>` 也被忽略。`<!>` 偶然与零元数结果相同不代表约束被执行。

`Binder.BindPropertySignature` 调用 BindMemberPattern 时将泛型输出传给 `out _`（当前 Binder.cs:363）。语言规范 §8 的 property-signature 最后只有 name-pattern，没有属性自身 generic clause。

**应修复：**对属性名称后的泛型子句诊断；保留合法的泛型声明类型路径。此项删除前两模式就存在，用户所指上次最终审查也已指出，并非此次 Legacy 删除引入。

### I02：普通 op_ 名称在 Cecil/Reflection 被错判

实际 C# `public int op_Fake() => 0;` 和 `public int op_Implicit() => 0;` 都是普通方法。对同一编译程序集及 `method(* *(..))`：

| 候选 | Shared | Standard Cecil | Standard Reflection | Standard Roslyn |
| --- | --- | --- | --- | --- |
| 普通 op_Fake | Matched | Operator / NotMatched | Operator / NotMatched | OrdinaryMethod / Matched |
| 普通 op_Implicit | Matched | Conversion / NotMatched | Conversion / NotMatched | OrdinaryMethod / Matched |

两适配器按名字前缀分类（CecilSignatureMapper.cs:374–381、ReflectionSignatureMapper.cs:434–441），Roslyn 按实际 MethodKind。不能以 N13 的真正运算符域决定掩盖这一不一致。

**应修复：**依据实际 operator/conversion 元数据分类，并用同一真实程序集做三适配器回归。名称只是必要条件，普通同名方法必须保留 OrdinaryMethod。

### I03：首泛型槽的否定被误识别为 `<!>`

`List<!int>`、`List<!*>`、`Dictionary<!int,string>`、`List<!!int>` 当前 AN1001；`List<(!int)>`、`List<(!*)>` 和第二槽 `Dictionary<int,!string>` 能编译。Shared 对这些结构也有不完整解析，但共同拒绝不能证明 Standard 的拒绝是设计。

两解析器见首 BangToken 就进入 ExplicitZeroArity（TypePatternParser.cs:254、SelectorParser.cs:781），没有先判断它是否真正组成 `<!>`。规范 §13.1 generic-slot 是 type-pattern，§12.1 明确允许 unary !。

**应修复：**只把真正独占子句的 `<!>` 识别为零元数，其他 `!` 交给类型一元表达式；同步独立与成员类型入口。

### I04：namespace 段的泛型子句被绑定后忽略

`System<>.Int32`、`System<..>.Int32`、`System<int>.Int32` 当前均编译并匹配 int；Shared 不匹配。`method(* Probe<int>.Owner.Run(..))` 同样无视 Probe<int>。`System<$T>.Int32` 甚至绑定了后续不求值的变量。

Binder 为所有 name segment 绑定子句；TypePatternEvaluator 只在实际类型段检查元数/实参，namespace 匹配只看段名（Binder.cs:700，TypePatternEvaluator.cs:487、527、588–624）。

**应修复/澄清边界：**实际被解析为 namespace 的段不能静默携带 CLR 泛型约束。建议给诊断；嵌套泛型类型必须按 `/` 表达。规范的 named-type EBNF 目前把带子句的 segment 用于所有段，也应收敛，不能仅改文字宣称“约束忽略”为有效语义。

## 6. 规范还需同步的两个边界

1. §7/§8 声明路径 EBNF 使用完整 `type-pattern`，但当前 ParseMemberPath 只解析名字路径和 `+`，不支持该非终结符承诺的所有 Boolean、变量、array、nullable 形态。N01/N02 裁决后，应使用准确的 declaring-name/member-path grammar，或实现裁决保留的结构。
2. `tasklike <int>` 当前合法，普通 `List <int>` 非法。tasklike 的 `<...>` 是结果子句，不是 generic-pattern-list，现有邻接规定不足以自动认定这是缺陷。该差异属于 Standard 新 keyword 的具体范围，建议允许结果子句空白并明确写进规范；若希望统一邻接，也需要明确裁决。

## 7. 诊断、元数据与扩展边界

这些不是漏列的内建语法糖，但会影响迁移的最终效果：

- Shared 解析/求值可能抛 ArgumentException、RegexParseException、NotImplementedException 等；Standard 编译失败返回定位诊断及不可用 pattern，匹配失败的能力问题可为 Unknown。保留 AN4003/AN4004/AN4005，不能把报错或异常伪装成 NotMatched。
- Shared 单泛型槽 `method(* Echo1<T>(..))` 可以解析但不匹配 Echo1<X0>；其虚拟名字单槽编码不一致。`method(T Echo1<T>(T))` 又可能因 GenericParameterTypePattern.AssignableMatch 未实现而抛 NotImplementedException。Legacy 已修复，Standard 迁移为 `$T`；这些是 Shared 的旧错误，不表示 Standard 应恢复隐式变量。
- 指针/function pointer、params/scoped 没有 Standard 专用语法。`Int32*` 是名字通配；对真正 int*，Shared 可能按 Cecil 名字得到 Matched，Standard named 查询为 Unknown:AN3001；裸 `*` 仍 Matched。这属于“不支持结构”的三态契约，不能把通配的星号重新解释成指针声明。
- Standard `+` 保留泛型替换，并按名义闭包含 SZ-array 合成接口：`System.Array+`、`System.Collections.Generic.IEnumerable<int>+` 对 int[] 是 Matched，Shared / Legacy 因数组剥离为 NotMatched。此规则已在 Standard 规范 §12.4 明确；`int+` 命中 int[] 的 Shared 行为也由已批准不剥数组规则替代，不再另要批准。variance 不加入。
- Shared 的 type cache 按 Scope/文本、method cache 按 MethodDefinition 且静态持有；Standard 使用规范元数据身份、有限编译缓存、adapter 生命周期和卸载约束。缓存隔离、取消、Unknown 传播保留现有 Standard 测试。
- Shared `Parser.TypePrefixParsers` 是公开的任意类型前缀解析扩展点；默认 MatcherFactory 是固定 switch，没有注册自定义选择器的 API。当前基线没有内建注册的额外前缀，Rougamo 当前源码亦未另注册该列表。Standard 的 registry 新增 qualified selector/predicate 扩展，参数是 string/integer/Boolean/type；不会向插件暴露任意类型前缀、语法解析或新标点。Shared 消费方若自建 AbstractPatternParser 子类，则是另一套入口，需独立迁移，不能宣称自动语法兼容。
- CompositeAccessibility 的删除已经明确授权。成员声明权限和属性 accessor 权限约束继续保留；通用 MethodSignature.IsAsync 继续是元数据，但不会在结构选择器中成为已删除的 async 条件。

## 8. 源码入口与复现

主要根因可从下列入口交叉核对：

| Shared 源码（固定 03a9d103） | 当前 AspectN 源码 |
| --- | --- |
| Tokens/TokenSourceBuilder.cs；AbstractPatternParser.cs | Parsing/Lexer.cs；Parsing/Parser.cs |
| Patterns/Parsers/Parser.cs、Execution/Property/Ctor/AttributePatternParser.cs | Parsing/SelectorParser.cs；Parsing/TypePatternParser.cs |
| Patterns/DefaultTypePattern.cs、GenericPatternFactory.cs | Binding/Binder.cs；Metadata/GenericParameterNamer.cs |
| Patterns/NotTypePattern.cs、NotDeclaringTypeMethodPattern.cs | Evaluation/TypePatternEvaluator.cs；MemberPatternEvaluator.cs |
| Patterns/Array/Nullable/Tuple/CompiledTypePattern.cs | Evaluation/TypePatternEvaluator.cs；Cecil/Roslyn/Reflection type systems |
| Patterns/PropertyPattern.cs、GenericNamePattern.cs、MethodPattern.cs | Evaluation/MemberPatternEvaluator.cs；三个 SignatureMapper |
| Patterns/AttributePattern.cs；SignatureParser.cs | Evaluation/AttributePatternEvaluator.cs；adapter attribute mapping |
| PatternParser.ParseMatchPattern；Matchers/RegexMatcher.cs；TypeSignature.cs | Parsing/Lexer.LexRegexBody；Evaluation/CanonicalSignatureFormatter.cs |

Shared 路径根为相邻仓库 `rougamo-eco/Shared.Cecil.AspectN`；AspectN 核心路径根为 `src/AspectN`。公开选择器匹配器及 And/Or/Not/Cached 包装也已检查，没有另一个内建 token/关键字族隐藏在 wrapper 中。

本地探针命令：

```powershell
dotnet run --project artifacts/shared-standard-audit-20261003/Audit.csproj -c Release --no-restore
```

它只链接固定 Shared 的源码读取，读取本仓库 Release 产物和历史构建，并在 artifacts 内写候选及结果。不会注册到 CI 或使正式 build/test/format 依赖 Shared/oracle。完整生产验证对应 `1fc3b44`：Release build 0 warnings/errors、837 tests 全通过、format --verify-no-changes 通过、五包验证与本地消费者检查通过；1,121→837 的减少来自 Legacy/oracle 用例退役。

## 9. 需要用户确认的结果

- N01 有真实能力被丢失：是否明确退役内部名称/声明路径否定，改用外层 Boolean 组合。
- N02：是否明确限制签名声明路径结构，并收敛当前过宽的 EBNF。
- N03–N18：是否认可所列 Shared 历史错误修正、增强和元数据/正则规则，保留当前 Standard 的相应行为。
- §6 的 tasklike 结果子句空白：是否明确允许该 keyword 的空白特例。

I01–I04 为约束丢失、解析冲突或跨适配器不一致，应修复而非要求批准成设计。此次保持现状并给出复现，是为了让确认针对真实可审查的差异，不在用户裁决前改变语言契约。
