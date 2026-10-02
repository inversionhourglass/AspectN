# Legacy 与 Standard 的语法差异和设计决策

本文件记录 2026-10-02 定稿的 Legacy / Standard 双语言模式差异全量清单与设计决策，避免后续把这些**有意的设计差异**误判为缺陷。行为依据为当日的实现与探针验证；规范文本见 [normative language specification](../superpowers/specs/2026-07-31-aspectn-language-spec.md)，Legacy 对 Shared.Cecil.AspectN 的兼容政策另见 [legacy-compatibility.md](legacy-compatibility.md)。

差异分三类标注：**[Legacy 独有]**（Standard 无此语法或语义不同）、**[Standard 独有]**（Legacy 冻结不含）、**[同形异义]**（同一写法两个版本含义不同，迁移时最危险）。

## 1. 词法与关键字

| # | 项 | Legacy | Standard | 决策 |
|---|---|---|---|---|
| 1.1 | 关键字大小写 | 全部不敏感（`METHOD`/`EXECUTION`/`PUBLIC`/`TYPE`/`REF` 均合法） | 全部小写、大小写敏感 | 保持两版现状 |
| 1.2 | `async` 糖大小写 | 严格小写（`ASYNC` 按类型名路径报错；Shared `ParseAsync` 区分大小写） | 无 `async` 关键字 | 保持 |
| 1.3 | 数字名称 | 非负整数可作名称（`123`、`method(* 123(..))`） | 拒绝 | Standard 走标准名称规则 |

## 2. 选择器

| # | 项 | Legacy | Standard | 决策 |
|---|---|---|---|---|
| 2.1 | `callable(...)` | AN2002 不可用 | 合法；域 = 非构造 method-like（方法/访问器/事件访问器/运算符/转换） | Standard 新增选择器，保持 |
| 2.2 | `execution(...)` | 域 = All（含构造） | 域 = All，与 Legacy 一致 | 保持 |
| 2.3 | 其余选择器 | `method/property/getter/setter/ctor/cctor/attr/regex/extension` 两版本相同 | 同左 | 保持 |

## 3. 修饰符

| # | 项 | Legacy | Standard | 决策 |
|---|---|---|---|---|
| 3.1 | 复合权限拼写 | 连写 `privateprotected` / `protectedinternal` | 空格短语 `private protected` / `protected internal`；连写给 AN4003 | Standard 特意设计，保持 |
| 3.2 | **[同形异义]** 空格 `private protected` | 两个独立修饰符 = private ∨ protected | 一个复合权限（C# `private protected` 语义） | Standard 保持复合权限设计 |
| 3.3 | 多访问性备选 | 空格并列（OR） | `|` 交替（`public\|internal`）；复合权限参与多选须括号 | 保持 |
| 3.4 | 括号化复合权限 | 报错（走 tuple 路径） | `(private protected)` 合法；`(public)`、`(internal public)` 报错 | 保持 |
| 3.5 | 修饰符集合约束 | 允许多个正向访问性、重复、`static`+`!static` 并存 | 最多一个正向访问性组、禁重复、禁 static 冲突 | 保持 |
| 3.6 | `any` / `none` | 拒绝（Flags 枚举名泄露裁决） | 不在修饰符表，走类型名路径报错 | 保持 |

## 4. async / tasklike（返回类型）

| # | 项 | Legacy | Standard | 决策 |
|---|---|---|---|---|
| 4.1 | `async T` / `async null` / `async void` | Task/ValueTask 返回糖 | 报错（无 `async` 修饰符） | Standard 用 tasklike 替代，删除 async，保持 |
| 4.2 | `tasklike` / `tasklike<T>` | `tasklike` 是普通类型名 | 匹配 `Task`/`ValueTask`；`tasklike<void>` 报 AN4004（裸 `tasklike` 同义） | 保持 |

## 5. 名称与名字模式（2026-10-02 名称模型定稿）

**模型**：类型 `x.y.z.Cls`，方法 `x.y.z.Cls.M`。命名空间可整体省略（= 任意命名空间，含全局）；`..` 是**命名空间列表省略符**（中间任意层，含 0 层），因此 `*..` 前缀要求命名空间至少一段、不匹配全局命名空间；`*` 是唯一缩写特例（任意命名空间 + 任意类型 + 任意方法名/泛型）。

| # | 项 | Legacy | Standard | 决策 |
|---|---|---|---|---|
| 5.1 | **[同形异义]** `*..M` / `System..*`（方法位） | `..` 分隔声明类型，末段缺省 = 通配类型名（Shared 行为） | **报错**："少一段"（声明类型缺类型名段）；成员必须用 `.` 连接声明类型 | Standard 采用标准名称解析 |
| 5.2 | `System..Service.Foo`（`..` 在命名空间中间） | 合法 | 合法（成员仍用 `.` 连接） | 保持 |
| 5.3 | 类型位 `System..*` | 合法：System + 任意中间层 + 任意类型（含 `System.Cls` 本身，`..` 含 0 层） | 同左 | 两版本一致 |
| 5.4 | 类型位 `*..*` | 合法但命名空间至少一段，**不含全局命名空间**类型；要含全局就省略命名空间写 `*` | 同左 | 推论已确认 |
| 5.5 | `*.*` 与 `*`（方法位） | — | `*.*` 不跨嵌套类型（嵌套用 `/`，如 `*/*.*`）；`*`（省略声明类型）任意——两者不严格等价 | 推论已确认，文档写明 |
| 5.6 | 裸 `*..` 匹配全局命名空间 | 特例放行（C6，对齐 Shared） | 无此特例（`*..` 要求 ≥1 段） | Standard 不需要特例 |
| 5.7 | 声明位 `*` / `*..*` = 全通配（含嵌套） | 是（genericOut） | 否：`*..*` 只按命名空间路径，嵌套需 `/` | 随名称模型，Standard 不复刻位置双轨 |
| 5.8 | 类型入口尾部空段 `Example..` / `*..` | 接受（永不匹配的历史行为） | 报错 | 保持 |
| 5.9 | 前导分隔符 `.Foo` / `..Foo` | 拒绝 | 拒绝 | 两版本一致（`c5bc6b1`） |

## 6. 泛型 arity（2026-10-02 定稿）

**Standard 的完整形态表**（适用于一切泛型子句位置：类型模式、声明类型、成员泛型子句、`*` 名称）：

| 形态 | arity |
|---|---|
| 省略（具名 `M`） | 恰好 0 |
| `<!>`（显式） | 恰好 0 |
| `<>` / `<,>` / `<,,>` | 恰好 1 / 2 / 3 |
| `<..>` | ≥ 1 |
| `<?>`（新增） | ≥ 0（泛型与非泛型都命中） |
| `M<T,>` / `M<,T>` / `M<T,,U>`（混合空槽） | arity = 槽数，空槽 = Any 实参 |
| 省略（`*`） | ≥ 0（唯一缩写特例） |

| # | 决策 | 说明 |
|---|---|---|
| 6.1 | 省略 = 零泛型（具名） | 直觉优先："`M()` 匹配泛型 M"反直觉；要含泛型写 `M<?>` |
| 6.2 | `<!>` 通用化到 Standard | 与 Legacy `<!>` 语义统一（FixedArity 0），消掉一条差异 |
| 6.3 | 新增 `<?>` = 任意 arity（含零） | 选型 `<?>` 优于 `^`/`#`：留在尖括号范式内、不污染后缀命名空间、家族一致 |
| 6.4 | `*<?>` ≡ `*` | 接受同义写法，不做特判拒绝 |
| 6.5 | `*<!>` = 任意非泛型类型、`*<..>` = 任意泛型类型 | 只匹配命名类型——数组/tuple 无泛型 arity，不在其中（文档写明） |
| 6.6 | void 上一切泛型子句 Standard 拒绝（AN2011） | void 永远非泛型；Legacy 因 `async void` 糖需区分裸 void 与显式子句，保留接受 |
| 6.7 | 混合空槽 Standard 采纳（对齐 Legacy） | `<T,>`=2 槽（T+Any）、`<T,,U>`=3 槽 |
| 6.8 | Legacy 差异保留 | 成员泛型子句/限定名省略 = 任意 arity；genericIn 裸单段名省略 = 零泛型 |

## 7. 泛型变量

| # | 项 | Legacy | Standard | 决策 |
|---|---|---|---|---|
| 7.1 | 隐式变量 | 泛型子句中裸单段名声明变量、按位置绑定（`method(T *<T>(T))`）；ctor 声明位同理（genericOut） | 仅显式 `$T`；裸名永远是类型名 | `$` 更清晰，保持 |

## 8. 类型后缀、tuple、nullable

| # | 项 | Legacy | Standard | 决策 |
|---|---|---|---|---|
| 8.1 | `[` / `?` 邻接 | 必须紧邻（`int []` 拒绝） | 必须紧邻（2026-10-02 收紧，与 Legacy 统一；括号内空格不受限） | 统一 |
| 8.2 | `int[]+` | `+` 下沉元素（≡`int+[]`） | AN2010 拒绝（数组目标） | 保持 Standard |
| 8.3 | `*+` | 零泛型通配名 + 可赋值（合法可命中） | **绑定期拒绝**（AN2010 家族；原求值恒 Unknown 的疣子移除） | 2026-10-02 确认 |
| 8.4 | `int?[]` 的 `?` | 并入元素字面名（匹配 IL 名 `int?` 的数组） | 按 Nullable 结构 | 保持 Standard |
| 8.5 | `(int)` 单元素括号 | 报错（tuple 至少两元素） | 报错（2026-10-02 统一；括号类型只剩 Boolean 分组 `(int\|\|string)`） | 统一 |
| 8.6 | tuple 定义 | `System.Tuple` 和 `ValueTuple` | 仅 `ValueTuple`（`Tuple` 显式写全名） | 保持 |
| 8.7 | `X?` 语义 | 仅 `Nullable<T>` 构造 | 引用类型 `T?`≡`T`、值类型 → `Nullable<T>` | 保持 |
| 8.8 | 裸模式匹配数组元素 | 裸命名/tuple/nullable/可赋值模式剥数组层比元素（`int` 命中 `int[]`；`!int` 与 Standard **结果相反**） | 裸模式不匹配数组，需显式 `[]` | Standard 保持现状（直觉行为） |

## 9. 成员路径与参数

| # | 项 | Legacy | Standard | 决策 |
|---|---|---|---|---|
| 9.1 | `..` 分隔声明类型 | 见 5.1 | 见 5.1 | 见名称模型 |
| 9.2 | `*..+.*`（`+` 紧跟 `..`） | 合法 | 报错（`+` 须跟命名类型） | Standard 不支持，保持 |
| 9.3 | 参数空槽 | `(,int)`/`(int,,s)`/`(int,)` 空位 = 隐式 `*` 参数 | 同左（2026-10-02 采纳，尾逗号不再被忽略：`(int,)` = **2 参数**） | 统一对齐 Legacy |
| 9.4 | ref kind 关键字大小写 | `REF int` 合法（选择器内） | `REF` 报错；独立类型入口 `ref` 两版本均区分大小写 | 保持 |

## 10. ref kind 匹配（2026-10-02 新设计，仅 Standard）

与修饰符哲学一致：**未提及的维度 = 不约束**。

| 模式 | 语义 |
|---|---|
| 未标注（`int`） | 任意 ref kind（by-value 与 by-ref 都命中）——返回位与参数位相同 |
| `ref` / `out` / `in` / `ref readonly` | 精确匹配该 kind |
| `!ref` | 仅非 by-ref（`RefKind.None`）；不提供 `!out`/`!in`（否定作用于 by-ref 整体概念） |
| `!ref ref int` | AN2005 拒绝（矛盾） |
| 返回位 `out` / `in` | 仍非法（InvalidRefKind） |

决策要点：默认不关心是高频诉求；`!ref` = 仅非 by-ref（用户否定的是"按引用传递"概念，不是四种风味之一）；删除了旧"仅通配类型豁免"特判。**注意**：这使 Standard 默认比冻结 Legacy 宽（Legacy 具名模式只匹配 by-value，除非全通配）——属批准差异。regex 规范签名仍渲染 ref（精细工具可区分），与移除 `[async]` 不矛盾（async 是语言已删除的概念，ref 是保留概念）。

## 11. attr 位置

| # | 项 | Legacy | Standard | 决策 |
|---|---|---|---|---|
| 11.1 | 位置词表 | `type`/`exec`/`para`(空格)/`ret`/`*`（不区分大小写） | `type`/`call`/`prop`/`para[n]`/`para[*]`/`ret`/`exec`/`any`/`*`（精确小写）；Legacy 空格形式静默兼容；长形式 AN4005 | 保持 |
| 11.2 | `attr(prop ...)` 无关联属性 | 无 `prop` 目标 | 普通方法候选 → **NotMatched**（确定没有）；访问器但后端缺 `AssociatedProperty` 链接 → **Unknown**（能力守卫；一方后端 Cecil/Roslyn/Reflection 都填链接，实际几乎不触发） | 2026-10-02 细化 |
| 11.3 | `attr(get ...)` / `attr(set ...)` | — | **不新增**；组合习语：`getter(...) && attr(call ...)`（`call` 在访问器候选上即该访问器自身的特性）。将来需要时可作纯增量语法补充 | 2026-10-02 决策 |

## 12. regex 规范签名

| # | 项 | Legacy | Standard | 决策 |
|---|---|---|---|---|
| 12.1 | `[async]` 渲染 | 不渲染 | 2026-10-02 起**不渲染**（asynchrony 经返回类型匹配，不经标志） | 移除 |
| 12.2 | 其余渲染 | ref kind 为 `&` 后缀；参数分隔 `,`；复合权限 `protectedinternal` | ref kind 为 `ref ` 前缀；参数分隔 `, `；复合权限 `protected internal` | 保持 |
| 12.3 | 泛型位置名 T/T1..TN | 总数为 1 时裸 `T` | 同左 | 两版本一致 |

## 13. 公共 API 词汇（2026-10-02）

候选载体类型定名 `MethodSignature`（原 `CallableSignature`）：构造方法/普通方法/getter/setter 在元数据层本质都是方法（对应 MethodDef 行 / Reflection `MethodBase` / Roslyn `IMethodSymbol`），C# 生态通用 "Method" 而非 "Function"，且**不应与 `method()` 选择器关联理解**——选择器是 `MethodKind` 上的过滤器，类型是候选载体。连带更名：`MethodKind`（原 `CallableKind`）、`MapMethod`、`AttributeTarget.ForMethod`、`AttributeTargetKind.Method`。保留：`BoundSelectorKind.Callable`、`SelectorTargetDomain.Callable`（镜像选择器域）。

## 迁移速查（Legacy → Standard）

| Legacy | Standard |
|---|---|
| `method(* Service.Get(..))`（任意泛型） | `method(* Service<?>.Get<?>(..))` 或 `method(* Service.*(..))` |
| `method(* *..StaticMethod(..))` | `method(* *..*.StaticMethod(..))` 或 `method(* StaticMethod(..))` |
| `method(* *..*(..))` | `method(* *(..))` 或 `method(* *.*(..))` |
| `method(async string *(..))` | `method(tasklike<string> *(..))` |
| `method(privateprotected * *(..))` | `method(private protected * *(..))` |
| `method(public private * *(..))`（OR） | `method(public\|private * *(..))` |
| `method(T *<T>(T))` | `method($T *<$T>($T))` |
| `getter(* *..*.Name)` | `getter(* *..*.Name)`（`..` 中间用法不变） |
| 限定名省略子句 = 任意泛型 | `System...List<?>` |
