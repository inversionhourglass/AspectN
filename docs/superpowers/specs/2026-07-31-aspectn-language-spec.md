# AspectN 语言设计

**状态：** 已批准的单一语言契约
**日期：** 2026-07-31；更新于 2026-10-03
**适用范围：** AspectN 1.0 语言设计
**语言：** Standard（AspectN 唯一语言）

## 1. 目的

AspectN 是面向 .NET 类型和可调用成员的模式语言。它解析与后端无关的模式，并针对 Mono.Cecil、Roslyn、Reflection 或未来适配器提供的归一化元数据求值。

语言有两个入口：

- **类型模式：** 匹配一个类型使用位置或类型身份。
- **成员表达式：** 用布尔运算符组合成员选择器。

AspectN 负责匹配，不提供查询投影。求值返回三态匹配结果，不返回变量捕获。

## 2. 单一语言契约

AspectN 直接实现现有 Standard 语法，采用：

- `callable(...)` 选择非构造器的方法类成员，`execution(...)` 同时覆盖构造器。
- `property(...)` 同时选择属性 getter 和 setter。
- `ctor(...)` 和 `cctor(...)`。
- 标准 C# 可见性短语，用 `|` 连接可见性备选。
- 显式 `$T` 模式变量。
- 元组语法表示 C# `ValueTuple`。
- 用 `tasklike` 或 `tasklike<T>` 匹配任务类返回形状，不使用 `async` 修饰符。
- 显式 `ref`、`out`、`in` 和 `ref readonly`。

没有语言选择 API 或兼容模式。`AspectNParser` 公开 `ParseMemberExpression(string source)`、`ParseTypePattern(string source)`。`AspectNEngine` 编译方法接受源码和可选编译选项。缓存键包含源文本、解析入口和正则超时。从 Shared.Cecil.AspectN 迁移是破坏性变化；Rougamo 集成及对应大版本发布属于独立任务。

## 3. 词法规则

### 3.1 大小写

- Standard 关键字小写且区分大小写。
- 类型、命名空间、成员及变量标识符均区分大小写。

### 3.2 空白与附属文本

空白可以分隔记号，但 Standard 的 `private protected`、`protected internal`、`ref readonly` 等复合短语需要保留其组合含义。

名称内部的 `.``/``..``/``/`、泛型起始 `<` 和关系后缀 `+`，只有邻接时才继续该名称；空白终止名称并分隔周围槽位。数组后缀 `[` 与可空后缀 `?` 也必须邻接类型模式。`<...>` 泛型模式列表和数组维数括号（如 `int[ , ]`）内部允许自由空白。

成员签名中的修饰符识别不得截断连续类型名称模式。`method(static* Run())` 的 `static*` 是返回类型名称模式；`method(static * Run())` 的 `static` 才是静态修饰符；`method(static static* Run())` 同时要求静态方法及 `static` 前缀返回类型。`public/private/protected/internal`、否定修饰符和复合访问性中的词遵循相同边界；类型的限定/嵌套路径、泛型及邻接后缀也不能被提前截断。访问性备选的 `|` 保持修饰符分组语义，不要求其两侧增加空白。

结果子句起始 `<` 必须邻接 `tasklike`：`tasklike<int>` 合法，`tasklike <int>` 是错误。成员分隔符 `.` 必须同时邻接声明类型模式及成员名模式，包括其分组括号。

AspectN 1.0 表达式语法不包含注释。

### 3.3 核心标点

| 记号 | 含义 |
|---|---|
| `!` | 逻辑否定 |
| `&&` | 逻辑合取 |
| `||` | 逻辑析取 |
| `|` | 可见性备选分隔符（Standard 修饰符） |
| `(` `)` | 分组、选择器主体、参数列表或元组 |
| `<` `>` | 泛型模式列表 |
| `[` `]` | 数组维数或特性参数索引 |
| `,` | 列表分隔符或数组维数标记 |
| `.` | 命名空间/类型/成员分隔符 |
| `/` | 嵌套类型分隔符 |
| `..` | 限定名中的命名空间省略段、`<..>` 的任意正元数或 `(..)` 的任意参数数量 |
| `*` | 名称或类型通配符 |
| `+` | 类型关系后缀 |
| `?` | 可空后缀；单独用于泛型子句 `<?>` 时表示包括零的任意元数 |
| `$` | 模式变量前缀 |

`/` 是规范嵌套类型分隔符。适配器将 Cecil `/`、Reflection `+` 和 Roslyn 包含类型符号归一为该形式。

### 3.4 标识符、名称模式与字面量

`identifier` 遵循 C# Unicode 类别范围，用于选择器名和模式变量。起始字符接受 `_`、Lu、Ll、Lt、Lm、Lo、Nl；后续字符还接受 Nd、Pc、Mn、Mc、Cf。不包含 UAX #31 的 Other_ID_Start/Other_ID_Continue 扩充，也不包含转义标识符语法。`name-pattern` 是包含字面名称字符和 `*` 的非空元数据名称段；保留标点和空白终止该段。匹配使用序数比较。

连续可空后缀（如 `int??`、`List<int??>`）产生 AN2010。嵌套或分隔的可空使用仍合法，如 `List<int?>`、`int?[]?`。引用注解（含邻接 `!ref`）只能用于成员参数或返回类型槽；在独立类型模式入口产生 AN2005，泛型与布尔类型模式内部也如此。`!`、`ref` 必须邻接，成员类型槽中的 `! ref`，包括中间的制表符或换行，均产生 AN2005。

```ebnf
name-pattern             = ( name-character | "*" ), { name-character | "*" } ;
boolean-literal         = "true" | "false" ;
```

`name-character`、`identifier`、`string-literal`、`integer-literal`、`regex-body` 是词法原语。字符串使用 C# 风格转义，整数是有符号十进制值，布尔字面量是小写 `true`、`false`。

## 4. 布尔表达式

通用表达式语法：

```ebnf
expression          = or-expression ;
or-expression       = and-expression, { "||", and-expression } ;
and-expression      = unary-expression, { "&&", unary-expression } ;
unary-expression    = [ "!" ], primary-expression ;
primary-expression  = selector | "(", expression, ")" ;
```

优先级从高到低：

1. `!`
2. `&&`
3. `||`

相同优先级的运算符从左向右结合，括号覆盖优先级。

连续一元否定（含空白分隔的 `! !`）是错误，在第二个 `!` 处报告 AN1001。该规则适用于成员表达式、类型模式及成员名称模式。显式分组的 `!(!int)` 仍合法。完整通配符的否定始终产生空匹配集，布尔补集没有通配符例外。

## 5. 三态布尔逻辑

求值使用 `Matched`、`NotMatched` 和 `Unknown`。

选择器只查询实际消费的事实。不可用元数据传播 `Unknown`，否定也如此；已知不匹配支配合取，已知匹配支配析取。可用性未知的空集合不能证明不存在。签名契约及示例见[元数据可用性](../../language/metadata-availability.md)。

### 5.1 否定

| 输入 | 结果 |
|---|---|
| Matched | NotMatched |
| NotMatched | Matched |
| Unknown | Unknown |

### 5.2 合取

| 左侧 | 右侧 | 结果 |
|---|---|---|
| Matched | Matched | Matched |
| Matched | NotMatched | NotMatched |
| Matched | Unknown | Unknown |
| NotMatched | Matched | NotMatched |
| NotMatched | NotMatched | NotMatched |
| NotMatched | Unknown | NotMatched |
| Unknown | Matched | Unknown |
| Unknown | NotMatched | NotMatched |
| Unknown | Unknown | Unknown |

### 5.3 析取

| 左侧 | 右侧 | 结果 |
|---|---|---|
| Matched | Matched | Matched |
| Matched | NotMatched | Matched |
| Matched | Unknown | Matched |
| NotMatched | Matched | Matched |
| NotMatched | NotMatched | NotMatched |
| NotMatched | Unknown | Unknown |
| Unknown | Matched | Matched |
| Unknown | NotMatched | Unknown |
| Unknown | Unknown | Unknown |

只有能保留所选求值策略要求的诊断时，实现才可短路。

## 6. Standard 成员选择器

```ebnf
selector =
      callable-selector
    | execution-selector
    | method-selector
    | property-selector
    | getter-selector
    | setter-selector
    | ctor-selector
    | cctor-selector
    | attribute-selector
    | regex-selector ;

callable-selector  = "callable",  "(", callable-signature, ")" ;
execution-selector = "execution", "(", callable-signature, ")" ;
method-selector    = "method",    "(", callable-signature, ")" ;
property-selector  = "property",  "(", property-signature, ")" ;
getter-selector    = "getter",    "(", property-signature, ")" ;
setter-selector    = "setter",    "(", property-signature, ")" ;
ctor-selector      = "ctor",      "(", ctor-signature, ")" ;
cctor-selector     = "cctor",     "(", type-pattern, ")" ;

```

### 6.1 选择器适用域

- `callable(...)` 匹配非构造器的方法类成员：普通方法、属性`/`事件访问器、运算符及转换。
- `execution(...)` 匹配全部方法类成员，包括实例与静态构造器。构造器候选通过其元数据名称（`.ctor``/``.cctor`）及 `void` 返回类型匹配可调用签名。
- `method(...)` 只匹配普通方法。
- `property(...)` 匹配属性 getter 或 setter。
- `getter(...)` 只匹配属性 getter。
- `setter(...)` 只匹配属性 setter。
- `ctor(...)` 匹配实例构造器。
- `cctor(...)` 匹配静态构造器。

任何选择器都不隐含成员有方法体或可以织入。Rougamo 在 AspectN 匹配之后应用其场景专用过滤。

## 7. 可调用签名

```ebnf
callable-signature =
    { modifier },
    type-use-pattern,
    member-path-pattern,
    parameter-pattern-list ;

member-path-pattern =
    [ "!" ],
    [ declaring-type-pattern, "." ],
    member-name-pattern ;

declaring-type-pattern = type-pattern ;
member-name-pattern = member-name-and, { "||", member-name-and } ;
member-name-and = member-name-unary, { "&&", member-name-unary } ;
member-name-unary = [ "!" ], member-name-primary ;
member-name-primary =
      name-pattern, [ generic-pattern-list ]
    | "(", member-name-pattern, ")" ;

type-use-pattern =
    [ return-ref-kind ],
    type-pattern ;

return-ref-kind =
      "ref"
    | "ref", "readonly" ;
```

成员路径包含可选前置否定、可选声明类型模式和布尔成员名称模式。省略声明类型时不写任何内容：`method(* Get(..))` 匹配任意声明类型上名为 `Get` 的成员，没有前导分隔符写法。分隔符 `.`、`..`、`/` 必须保持邻接（第 3.2 节）。

成员分隔符前的布尔声明类型需要分组，如 `(A || B).Run`。名称可独立分组，如 `A.(Run || Stop)`，也可组合为 `(A || B).(Run && !Stop)`。数组和可空类型采用其他类型位置相同的结构规则，如 `int[,].Get`、`int?.GetValueOrDefault`。名称叶节点是带通配字符和可选方法泛型子句的字面元数据名，不是类型别名或结构类型模式。

前置 `!` 否定声明类型、成员名称及该名称泛型子句的完整合取，不否定修饰符、返回`/`属性类型、参数或选择器适用域。因此 `!Run` 是零元数 `Run` 的补集，`!Run<?>` 是任意元数 `Run` 的补集。只否定名称用 `A.!Run`，只否定声明类型用 `(!A).Run`。`!*(..)` 不匹配成员。否定隔离暂定变量绑定，布尔分支按第 13 节的作用域规则处理。

`tasklike` 在成员路径的声明类型名位置仍是字面名，包括 `tasklike<int>+.Get`。在返回`/`参数类型模式、泛型实参及 `ctor`/`cctor` 声明类型模式中，它表示 `Task`/`ValueTask` 形状。因此 `ctor(tasklike(..))`、`cctor(tasklike<int>)` 使用形状匹配。`Example.tasklike<int>` 这类限定字面名在任何位置（包括构造器）都无歧义。

`..` 是命名空间省略段，只能位于命名空间段之间，不能直接位于成员名之前。成员名始终通过 `.` 与声明类型连接。`method(* *..StaticMethod(..))`、`method(* System..*(..))` 因缺少声明类型的类型名称段而错误；应写 `method(* *..*.StaticMethod(..))`，或用 `method(* StaticMethod(..))` 完全省略声明类型。替代写法范围不同：显式命名空间`/`类型路径除非明确表达，否则排除全局或嵌套类型；省略声明类型则包含它们。

整个成员路径的 `*` 匹配任意命名空间、声明类型、成员名及泛型元数。结构化声明类型语法下，`*.*` 同样使用完整类型通配符，包含数组、泛型形参和嵌套声明类型。添加声明类型泛型子句后成为有名模式：`*<?>.*` 排除数组与泛型形参，嵌套类型要求 `/`。其他显式名称继续遵守命名空间与嵌套规则。

Standard 没有 `async` 修饰符，签名中将 `async` 用作修饰符是错误。异步形状通过返回类型匹配。

任务类返回匹配使用类型模式：

```text
method(tasklike<string> Service.Get(..))
method(tasklike Service.Flush(..))
```

`tasklike<T>` 匹配 `Task<T>` 或 `ValueTask<T>`，与方法是否使用 `async` 编译无关。裸 `tasklike` 匹配非泛型 `Task`/`ValueTask`；`tasklike<void>`、`tasklike<System.Void>` 被拒绝并附迁移诊断。自定义可等待类型不在 1.0 `tasklike` 语法范围内。

历史 `async` 返回简写不受支持，通用 `MethodSignature.IsAsync` 元数据仍可供调用方使用。

## 8. 属性签名

```ebnf
property-signature =
    { modifier },
    type-use-pattern,
    member-path-pattern ;
```

类型位置表示属性类型，不是 setter 方法的 CLR 返回类型。

属性名称采用与方法相同的布尔和路径否定语法。任何属性名称叶节点上的泛型子句都是错误（AN2011），包括 `<!>` 或布尔分支内部的子句；声明类型的泛型子句仍合法。

`property(...)` 对关联属性元数据求值，选择 getter 和 setter 方法。统一模型分别表示属性与访问器。

## 9. 构造器签名

```ebnf
ctor-signature =
    type-pattern,
    parameter-pattern-list ;
```

`ctor` 永不匹配静态构造器；`cctor` 永不匹配实例构造器。

## 10. 修饰符

Standard 识别：

- `public`
- `protected`
- `internal`
- `private`
- `protected internal`
- `private protected`
- `static`
- `!static`
- `!public` 等否定可见性

```ebnf
modifier =
      accessibility
    | accessibility-alternation
    | "!", accessibility
    | "static"
    | "!", "static" ;

accessibility =
      single-accessibility
    | compound-accessibility ;

accessibility-alternation =
      alternation-atom, "|", alternation-atom, { "|", alternation-atom } ;

alternation-atom =
      single-accessibility
    | "(", compound-accessibility, ")" ;

single-accessibility =
      "public"
    | "protected"
    | "internal"
    | "private" ;

compound-accessibility =
      "protected", "internal"
    | "private", "protected" ;
```

一个 Standard 签名最多出现一项正向可见性或一组 `|` 备选。`|` 连接正向备选，候选可见性等于其中任一项即匹配。复合可见性与 `|` 组合时必须加括号。为可读性也允许单独的复合可见性加括号；单个可见性的 `(public)`，或不构成复合短语的 `(internal public)` 是错误。空白不连接可见性备选，必须用 `|`：

```text
method(private|internal * *(..))
method((private protected)|(protected internal)|public * *(..))
```

否定只作用于一个可见性短语，`!` 不能与 `|` 组合：

```text
method(!protected internal * *(..))
```

`private protected` 和 `protected internal` 各表示一个复合可见性；备选用 `|` 表达。历史无空格拼写报告 AN4003，并给出带空格的替代短语。

## 11. 参数模式与引用分类

```ebnf
parameter-pattern-list =
    "(", [ ".." | parameter-slot, { ",", parameter-slot } ], ")" ;

parameter-slot =
    [ ref-annotation ],
    type-pattern ;

ref-annotation =
      ref-kind
    | "!ref" ;

ref-kind =
      "ref"
    | "out"
    | "in"
    | "ref", "readonly" ;
```

规则：

- `(..)` 匹配任意数量和类型的参数。
- `()` 匹配零参数。
- 显式列表匹配精确数量与顺序。
- 逗号之间或之后的空槽隐含 `*` 参数：`(int,)` 匹配两个参数（`int` 加任意），`(,int)` 匹配（任意加 `int`）。
- 省略引用注解不约束引用分类，参数`/`返回槽同时匹配按值和引用候选。`ref`/`out`/`in`/`ref readonly` 精确匹配对应种类；`!ref` 只匹配非引用（按值）候选。`!ref` 不能与显式引用分类组合，没有 `!out``/``!in`，否定作用于整体引用概念。
- 返回槽支持 `ref`、`ref readonly`、`!ref`；`out` 和 `in` 对返回值无效。

`params`、`scoped`、指针和函数指针无法用 Standard 1.0 表达，适配器可以为未来谓词保留相应事实。

## 12. 类型模式

### 12.1 结构

括号对布尔类型表达式分组，如 `(A || B)`。单独 `(int)` 被拒绝，应写 `int`。元组至少两个元素，因此 `()`、`(int,)` 无效。独立 `ParseTypePattern` 接受纯类型模式；`ref`/`out`/`in`/`ref readonly`/`!ref` 属于成员返回`/`参数的类型使用位置。

```ebnf
type-pattern =
    type-or-pattern ;

type-or-pattern =
    type-and-pattern, { "||", type-and-pattern } ;

type-and-pattern =
    type-unary-pattern, { "&&", type-unary-pattern } ;

type-unary-pattern =
    [ "!" ], type-primary-pattern ;

type-primary-pattern =
      type-atom-pattern, { type-suffix }
    | "(", type-pattern, ")" ;

type-atom-pattern =
      named-type-pattern
    | tuple-type-pattern
    | tasklike-type-pattern
    | pattern-variable
    | "*" ;

named-type-pattern =
    [ namespace-pattern, namespace-separator ],
    type-name-segment,
    { "/", type-name-segment } ;

namespace-pattern =
    name-pattern, { namespace-separator, name-pattern } ;

type-name-segment =
    name-pattern, [ generic-pattern-list ] ;

namespace-separator = "." | ".." ;

tuple-type-pattern =
    "(", type-pattern, ",", type-pattern,
    { ",", type-pattern }, ")" ;

tasklike-type-pattern =
    "tasklike", [ "<", type-pattern, ">" ] ;

pattern-variable =
    "$", identifier ;

type-suffix =
      array-rank-specifier
    | "?"
    | "+" ;

array-rank-specifier =
    "[", { "," }, "]" ;
```

类型级布尔运算采用与成员表达式相同的优先级和三态逻辑。

绑定器核验后缀顺序及适用性。数组维数说明符每个逗号使维数加一；`[]` 为一维。

### 12.2 命名空间与名称

- `*` 在单个名称段内匹配零个或多个字符。
- `..` 匹配零个或多个命名空间段。
- 省略命名空间匹配任意命名空间。
- `/` 分隔嵌套类型。
- Standard 使用区分大小写的序数匹配。

命名空间段不能有泛型子句，在子句处报告 AN2011。`Ns<int>.Owner` 无效，`Ns.Outer<int>/Inner<string>` 合法。嵌套类型使用 `/`，不能把泛型类型放在命名空间段中。

示例：

```text
Service
MyCompany.Services.*Service
MyCompany..Internal..*Service
Outer/Inner
```

### 12.3 基础类型别名

Standard 识别：

```text
bool byte sbyte short ushort int uint long ulong
char float double decimal string object void
```

每个别名映射到对应 `System` 类型。

### 12.4 类型关系

后缀 `+` 匹配有名类型自身，或任何已声明子类型`/`实现，包括运行时合成的接口实现，如 SZ 数组的泛型集合接口：

```text
System.IDisposable+
*Provider+
```

`+` 的目标按结构校验：移除分组括号后，必须是有名类型模式（允许通配名称和显式泛型子句），或有名类型外包一层可空模式（`X?+`）。其他目标结构均以绑定诊断拒绝：数组、嵌套或交错的可空包装、裸通配符、元组、布尔组合、模式变量、`tasklike`，以及被其他后缀隔开的第二个 `+`。规则只约束目标节点自身：有名目标泛型实参中的数组、通配符和元组仍合法，如 `List<int[]>+`；`int+?` 是可空包装关系模式，不是关系目标，不受影响。

变体转换（数组协变、泛型协变`/`逆变）不属于 `+`。可将 `+` 放到类型实参或数组元素内部，组合表达所需匹配：

```text
Base+[]                     matches Derived[]
IEnumerable<object+>        matches IEnumerable<string>
```

无法解析必需基类或接口信息时产生 `Unknown`。

### 12.5 数组

```text
int[]
int[,]
string[][]
```

除非通配模式明确移除约束，否则维数和嵌套必须完全匹配。

Standard 数组模式约束维数及元素模式。`int[]` 同时接受 Vector（`int[]`、SZ）和 General 一维数组（`int[*]`、非 SZ）；`[*]` 是元数据记号，不是新增 DSL 语法。公共 `ArrayTypeSignature.Shape` 为身份与名义关系查询保留该区别。嵌套数组和泛型实参递归比较形状。数组到数组的名义查询允许元素相同的 Vector→General 形状转换，不允许反向转换，也不增加 CLR 元素协变。见[数组形状](../../language/array-shape.md)。

### 12.6 可空性

Standard 规则：

- `T` 解析为不可空值类型时，`T?` 匹配 `System.Nullable<T>`。
- `T` 解析为引用类型时，`T?` 等同于 `T`。
- 不匹配引用类型的可空注解。
- 始终可以显式写 `System.Nullable<T>`。

### 12.7 元组

Standard：

```text
(int, string)
method(($T, decimal) Service.M<$T>())
```

只匹配 `System.ValueTuple`。长元组按结构归一，调用方无需表达运行时 `TRest` 嵌套。

匹配 `System.Tuple` 时显式写：

```text
System.Tuple<int, string>
```

使用有名泛型类型模式显式匹配 `System.Tuple`。

## 13. 泛型

### 13.1 元数

Standard 使用：

| 模式 | 含义 |
|---|---|
| `Name` | 名为 Name 的非泛型类型/成员，省略子句即零元数 |
| `Name<!>` | 显式零元数 |
| `Name<>` | 元数 1，实参任意 |
| `Name<,>` | 元数 2，实参任意 |
| `Name<,,>` | 元数 3，实参任意 |
| `Name<..>` | 任意正元数，至少一个 |
| `Name<?>` | 任意元数，包括零；泛型和非泛型均可 |
| `Name<T1,T2>` | 精确实参模式 |
| `Name<T,>`/`Name<,T>` | 元数 2，一项精确、一项通配 |
| `Name<T,,U>` | 元数 3，中间槽通配 |

`<!>`、`<?>` 可用于所有泛型子句位置（类型、声明类型、成员泛型子句）及通配名称。带子句的 `*` 绑定为有名通配模式，不是完整类型通配符：不匹配数组和泛型形参候选，单段模式不跨越嵌套类型（第 7 节）。从 CLR 看，`ValueTuple<...>` 是普通有名泛型类型，同其他构造泛型一样由 `*<?>``/``*<..>` 匹配。没有子句的 `*` 仍是唯一完整通配符，也是唯一匹配数组和嵌套的简写。

特殊名称通配符 `*` 不带泛型子句时匹配任意元数，这是唯一简写例外。其他名称省略子句均为零元数；包含泛型候选应写 `Name<?>`。

`void` 不含泛型形参，其任何泛型子句（`void<>`、`void<..>`、`void<!>`、`void<?>`）都以绑定诊断拒绝。

```ebnf
generic-pattern-list =
    "<", ( "!" | ".." | "?" | generic-slot, { ",", generic-slot } ), ">" ;

generic-slot =
    [ type-pattern ] ;
```

空通配列表 `<>` 表示元数一；全逗号列表的元数是逗号数加一；至少有一项带类型实参的列表按槽位数确定元数，每个空槽占一个任意类型实参位置。

只有 `!` 后紧接闭合记号时才识别零元数形式（忽略子句内部空白）。`Box<!>` 是零元数；`Box<!int>` 是包含布尔模式 `!int` 的一个泛型实参。首槽和后续槽采用相同类型模式语法。

#### 中立泛型签名的构造不变量（2026-10-20 维护者裁决）

维护者明确要求“应在构造时拒绝结构不合法的签名”。本条取代此前将残缺泛型实参列表送入匹配并返回 Unknown 的规则；历史证据保留原始结论，不能继续作为当前输入契约。

- `NamedTypeSignature` 的空实参列表表示定义；非空列表必须等于身份包含链上的泛型总元数，不能少槽、多槽、包含 null，或给零元数类型提供实参。提供 `OriginalDefinition` 时，它必须是同一身份的未构造定义。
- `MethodSignature` 的声明形参必须为方法形参，位置从零连续且等于列表索引。非空实参列表必须与已知声明元数一致；提供原始定义时，原始定义必须未构造，当前声明槽位或构造实参必须保持其元数，包括零元数。没有声明槽位也没有原始定义时，完整实参列表自身确定方法元数。
- 违反这些不变量时，构造器抛 `ArgumentException` 并指出有关参数，不进入匹配。合法定义的零构造实参不等于残缺列表；`Box<int,T>` 等开放构造具有全部槽位，允许其中包含泛型形参节点。
- 这是不依赖后端的结构校验，不验证声明是否存在、泛型约束是否满足或每个槽位是否属于可加载的 CLR 类型。所需类型事实不可用时可在完整槽位中保留 `UnsupportedTypeSignature`，匹配仍按所需事实决定三态结果。

裁决背景、取代范围和当前证据见 [C02 修复记录](../../reviews/2026-10-20-c02-generic-signature-validation.md)。

### 13.2 模式变量

Standard 变量以 `$` 开头：

```text
$T
$Result
$TKey
```

示例：

```text
method($T Service.Convert<$T>($T))
```

按 [2026-10-21 已批准设计](2026-10-21-generic-variable-modes-design.md)，变量模式在编译时由不可变的 `AspectNCompilationOptions.GenericVariableMode` 决定。两种模式共用 Standard 语法和作用域规则。成员签名中，声明类型与方法泛型子句先于返回`/`属性类型和参数绑定、求值，与文本顺序无关。

**Declaration（默认）：** `$T` 是原始 Match 候选自身声明泛型槽位的别名，不捕获任意类型。首次出现只能直接占据独立类型模式根、成员声明类型或方法名泛型子句的一个槽位；根的数组/可空包装及嵌套类型的外层槽位也可确定定位。关系目标、特性类型、嵌套实参、元组元素或 tasklike 结果内不能首次声明变量（AN2006），已有别名仍可在那里引用。例如 `Box<$T>` 可声明，`Box<List<$T>>` 和 `IValue<$T>+` 不可首次声明。

每个别名固定来源、声明层级和槽位；引用按 owner kind、owner ID、owner assembly、position 严格比较。类型槽位沿包含链展开，方法槽位独立。声明处的实际实参也须是该自身形参，因此 `Box<int>`、借用其他声明形参的开放构造或 `Echo<int>` 不会因存在 OriginalDefinition 自动匹配。确定的具体实参为 NotMatched，所需声明事实不可用时保留 Unknown。遍历祖先或特性不重新解释别名；每次匹配只缓存唯一声明事实。OR、NOT、特性及祖先枚举组合三态与诊断，不创建或复制动态绑定备选集。

**Capture（显式）：** 泛型子句内正向出现建立动态绑定，可捕获声明形参、具体类型和其他受支持签名；重复引用要求当前 `ITypeSystem.AreEqual` 的严格类型相等。保留从特性或关系目标首次捕获及多备选筛选，受第 21 节的有限预算约束。

规则：

- 普通 `T`/`Result` 是类型名，不是隐式变量。
- 返回`/`属性类型及方法`/`构造器参数类型只能引用外层类型、方法泛型子句或可见合取作用域已声明的变量。这些类型使用内部的嵌套泛型子句不声明变量。
- 单独 `$T` 不能声明变量。泛型声明实参不能否定变量，即使名称已可见；`method($T Service.M<!$T>())`、`method($T Service<$T>.M<!$T>())` 无效。`Box<!int>` 等具体否定实参仍合法。
- 返回或参数引用可被否定，`method(!$T Service.M<$T>())` 合法。整个类型模式的否定可以引用现有变量，如 `Box<$T> && !Box<$T>`。
- 合取共享可见别名；Capture 共享动态绑定环境。
- 每个 OR 分支隔离新声明，分支内声明不能向外逃逸；Capture 同时隔离暂存绑定。
- Capture 的匹配及未知备选在 OR、继承`/`特性枚举后保留，未知约束继续跟随后续合取；仅变量键、签名实例和确定性均一致的备选可合并。去重减少唯一备选数，但处理重复项仍消耗工作预算。
- 否定不带出内部暂存绑定；Declaration 只对固定引用的三态取反。
- 变量不能首次在否定下引入。
- 公共 `MatchResult` 不返回绑定。

原先依赖具体类型捕获或间接首次捕获的调用必须显式选择 Capture；默认变化属于维护者批准的语义迁移，元数据映射器仍保留实际构造实参。

## 14. 特性

特性选择器使用位置短名称：

```ebnf
attribute-selector =
    "attr", "(", attribute-position, type-pattern, ")" ;

attribute-position =
      "type"
    | "call"
    | "prop"
    | "para", "[", ( integer-literal | "*" ), "]"
    | "para", ( integer-literal | "*" )
    | "ret"
    | "exec"
    | "any"
    | "*" ;
```

```text
attr(type AttributeType)
attr(call AttributeType)
attr(prop AttributeType)
attr(para[0] AttributeType)
attr(para[*] AttributeType)
attr(ret AttributeType)
attr(exec AttributeType)
attr(any AttributeType)
```

语义：

- `type`：声明类型的直接特性。
- `call`：当前方法的直接特性，候选为访问器时表示访问器方法自身。
- `prop`：候选是属性访问器时，表示关联属性的直接特性。非访问器候选确定没有属性位置，返回 `NotMatched`；后端缺少访问器属性关联时返回 `Unknown`。
- `para[n]`/`para n`：参数 `n` 的直接特性。
- `para[*]`/`para *`：任意参数的直接特性。
- `ret`：返回位置的直接特性。
- `exec`：当前可调用成员的直接特性；候选为属性访问器时还包括关联属性的直接特性。
- `any`/`*`：任意支持的直接位置。

没有 `get`/`set` 特性位置。访问器范围的特性用组合表达，因为访问器候选上的 `call` 已表示该访问器自身特性：

```text
getter(string Name) && attr(call ObsoleteAttribute)
```

此前的长位置名 `callable`/`property`/`parameter`/`return` 被拒绝，迁移诊断 AN4005 给出短名称替代。

特性匹配不实例化特性对象。继承特性语义不在 Standard 1.0 范围内。

`attr` 使用 CustomAttribute 记录。元数据标志及其他表编码不是特性记录，即使 Reflection 将其公开为伪特性，或源代码使用特性写法也如此。排除 `Serializable`、`ComImport`、`StructLayout`、`DllImport`、`PreserveSig`、`MethodImpl`、`In`、`Out`、`Optional`、`MarshalAs`、`DefaultParameterValue` 编码。但这些类型的真实 PE CustomAttribute 数据仍可见，包括同一元数据也导致伪特性合成的情况。普通标记特性及真实 readonly 标记记录仍可见。数组声明类型不继承元素类型的特性。

## 15. 正则表达式

```ebnf
regex-selector = "regex", "(", regex-body, ")" ;
```

`regex-body` 保留原始字符。闭合分隔符是在处理转义原子、普通字符类和嵌套分组之后，未配对的外层 `)`。支持的正则子集为：

- 字面量、字符转义、`.`、`\d``/``\w``/``\s``/``\p{L}` 等字符类别及其补集。
- 普通字符类、范围和否定，如 `[a-z]`、`[^\s]`。字符类内的字面 `[`、`]` 必须转义为 `\[`、`\]`。
- 普通分组 `(...)`、非捕获分组 `(?:...)`、分支 `|`、锚点、贪婪`/`懒惰量词 `*`、`+`、`?`、`{m}`、`{m,}`、`{m,n}`。

其他所有 `(?...)` 分组被拒绝：注释、内联或局部选项（含 `(?x)`、`(?i)`）、命名/平衡分组、条件、环视和原子分组。反向引用（`\1` 到 `\9`、更长数字形式、`\k<name>`、`\k'name'`）及嵌套/相减字符类也被拒绝。类外以 `1`–9 开头的数字转义始终被拒绝，包括有歧义的八进制形式；字面字符编码使用 `\xNN` 或 `\uNNNN`。字符类开头未转义的 `]` 和 `[[]` 字面括号惯用写法不支持。空白及 `#` 始终保持字面字符。

不支持的结构产生编译期 `AN2012`（`UnsupportedRegexFeature`）；转义或类内看似结构的字面文本可以接受，如 `regex(\(\?x\))` 匹配字面 `(?x)`。子集校验后由 .NET 核验正则语法，无效模式产生编译期 `AN3002`（`InvalidRegex`）。两类错误均不产生编译成员模式。复杂选择逻辑应使用结构选择器与 `&&``/``||``/``!`。

正则用与区域性无关的 `Regex.IsMatch`，在单一规范成员签名上做子串搜索。完整签名匹配使用 `^...$`：

```text
<accessibility> [static] <return-type> <declaring-type>.<member-name><generic-part>(<parameters>)
```

编译器 `async` 标记不属于规范签名，返回形状用 `tasklike` 匹配。引用分类呈现在受影响参数`/`返回类型之前。嵌套类型使用 `/`。除定义签名中的声明泛型形参之外，类型名完全限定；形参按位置改名为 `T1..TN`：先声明类型形参（最外层从左到右，然后嵌套层），再方法形参。同一个形参在声明类型子句、方法泛型子句和使用位置中均呈现相同名称。构造签名呈现实际类型实参。

开放构造声明类型按从左到右顺序递归访问实际实参，包含有名泛型实参及数组元素。按首次出现为每个不同类型形参声明分配一个位置，再追加方法形参。例如 `Container<List<T>>` 保留 `List<T>` 内的 `T`，而 `Pair<T,T>` 两次使用同一名称。另有方法形参时名称为 `T1`、`T2`。闭合实参不占形参位置。

按位置编号是有意设计：源码重命名泛型形参时模式仍有效，作者只需知道各类型及方法的元数。总形参数量（类型加方法）恰好一个时，唯一形参呈现为裸 `T`；其他情况为 `T1..TN`。裸 `T` 和 `T` 加数字为该编码保留。完全限定名恰为这些记号的具体类型（仅可能在全局命名空间中），正则无法将它与位置形参区分；此类成员使用结构选择器。

正则求值默认单次250ms有限超时，可通过 AspectNMatchOptions.RegexTimeout 配置正有限值或显式 TimeLimit.Unlimited；超时产生局部 `Unknown`/`AN3003`。普通量词仍可能造成高成本回溯，限制语法不构成耗时保证。规范文本和总工作额度独立生效，见第21节与[资源规范](2026-10-22-configurable-resource-limits-design.md)。

## 16. 封闭语义契约

不支持自定义选择器、谓词和匹配语义。选择器名称是第 6 节内置名称，区分大小写。非限定未知名和 `vendor.feature(...)` 等限定名均在解析时产生 `AN2001`（`UnknownSelector`）。恢复保留已消费记号，在下一个外层表达式边界继续，不解释自定义实参。

引擎及编译 API 没有扩展注册表参数。此前的扩展诊断 `AN2008` 已退休，不重新分配。

## 17. 诊断

Standard 诊断包含以下稳定类别：

- 词法错误。
- 缺少或意外记号。
- 未知选择器或谓词。
- 无效目标域。
- 无效修饰符组合。
- 无效引用分类。
- 无效泛型子句，包括 `void` 的任意泛型子句。
- 重复或非法作用域变量。
- 超出 Int32 范围的无效参数索引。
- 后端能力缺失。
- 解析引用失败。
- 不支持的正则特性（`AN2012`）和无效正则语法（`AN3002`），均为编译错误。
- 正则超时。

存在替代写法时，迁移诊断提供可执行的替代建议：

```text
privateprotected -> private protected
protectedinternal -> protected internal
tasklike<void> -> tasklike
attr(callable ...) -> attr(call ...)
attr(property ...) -> attr(prop ...)
attr(parameter ...) -> attr(para ...)
attr(return ...) -> attr(ret ...)
```

## 18. 一致性

现有 Standard 回归套件和跨适配器案例定义一致性。解决方案不依赖对照实现。AN4003、AN4004、AN4005 保持迁移诊断；已移除选择器模式诊断 AN2002 不重新分配。

## 19. Standard 示例

```text
method(public static string MyCompany.Services.UserService.Get*(..))

callable(!public * *..*.*(..))

execution(public string MyCompany.Services.*.*(..))

property(public string MyCompany.Models.User.Name)

getter(* *..*.CreatedAt)

setter(* *..*.UpdatedAt)

ctor(MyCompany.Services.UserService(..))

cctor(MyCompany.Services.UserService)

method(tasklike<string> *..*.*Async(..))

method($T Converter.Convert<$T>($T))

method(* Service.Copy(ref int, out string))

method((int, string) Service.GetPair())

attr(para[*] System.ComponentModel.DataAnnotations.RequiredAttribute)

method(* Service.*(..)) && !attr(call ObsoleteAttribute)
```

## 20. 设计原因与迁移

[设计决策](../../language/standard-design-decisions.md)解释名称模型、泛型元数、通配符族、引用分类及结构后缀。[迁移指南](../../migration/shared-cecil-to-aspectn.md)说明从 Shared.Cecil.AspectN 迁移的破坏性变化及移除的公共 API。

## 21. 实现限制与诊断呈现

资源策略由[分阶段资源规范](2026-10-22-configurable-resource-limits-design.md)定义，取代本节先前的固定策略。解析、原生映射和匹配分别使用不可变的 AspectNParseOptions、AspectNMappingOptions、AspectNMatchOptions。配置支持正有限值和显式 Unlimited，省略/null 使用默认值；取消及合法性约束独立生效。

解析默认完整源码256个UTF-16代码单元、非EOF token 1,024、创建节点2,048、逻辑帧128、最终树深度256。三阶段类型深度默认64含根，有效类型槽位默认64含外层，方法自身槽位独立默认64。数组、包装、构造实参和包含类型按既有结构口径展开；声明者、原始定义和修饰符身份分别检查，不因身份循环重复展开。词法前瞻重扫和临时节点计入实际消耗。解析资源用AN1003，深度用AN1002，泛型条款用AN2011。

映射默认累计工作100,000、扫描特性1,024、每个生成文本16,384。一次最外层Map调用共享额度，检查先于受控分配和文本生成。显式映射超限抛AspectNLimitException；查询内部超限转Unknown并保留诊断，继续遵守D01。深度/元数为AN3005/AN3006，其他资源为AN3008。

匹配默认总工作250,000、关系节点1,024、边4,096、特性记录1,024、每个规范文本16,384；Capture每收集器256个不同备选、整次50,000绑定工作单位。节点、边、记录、绑定及字符处理累计计费，重复和去重仍有工作成本。Declaration不创建动态绑定环境，仍消耗总工作。查询内映射共享子预算，同时消耗外层预算。独立公开查询使用类型系统实例的queryOptions；Match内部查询使用编译计划配置，不叠加独立查询默认值。

AN3005/AN3006保留C05：超限祖先留诊断并继续合法祖先，已知分支可决定Matched。AN3007（Capture专项）和AN3008（其他资源）中止整次操作，局部Matched不能覆盖。正则默认单次250ms，可显式不限；超时AN3003保持局部三态。规范文本超限改为AN3008；真正缺少位置声明等格式化事实仍为AN3001。取消优先抛OperationCanceledException，不吞任意外部异常，不提供OOM恢复。

自身深度相关算法使用显式工作栈；Unlimited仍受外部运行时/元数据库限制。第三方ITypeSystem不公开接收预算上下文，Core只计量可观察的自身工作和返回结果消费，无法抢占或精确计量第三方内部执行。

公共诊断集合是不可变快照。诊断码、严重度、跨度和消息相同的精确重复项去重。顺序为跨度起点（无位置诊断最后）、序数诊断码、严重度、跨度长度，再序数消息。同一码/跨度但不同消息仍独立保留。

成员表达式根解析完成后，连续剩余 token 作为一个尾部恢复区域，只产生一条 AN1001。跨度从第一个剩余 token 的起点延伸至最后一个剩余 token 的终点，包含中间空白，不包含首尾空白。扫描仍执行词法分析、资源计量和取消检查；资源限制中止扫描时，保留已扫描部分的尾部诊断及限制诊断。缺失语法项和独立词法错误继续分别报告，不按相同位置统一合并，也不改变上述诊断快照去重规则。

编译选项采用(GenericVariableMode = Declaration, AspectNMatchOptions? = null)，移除旧重载和AspectNLimits。解析/编译入口接收可选解析选项及取消令牌，AreEqual增加取消令牌。全部配置字段按值进入编译缓存键，只发布完成结果；不同Match和独立查询各自计数，取消或异常不缓存。
