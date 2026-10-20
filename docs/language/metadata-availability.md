# 元数据可用性

Roslyn 源码映射补充编译器的 async、iterator 和 async-iterator 状态机 CustomAttribute 记录，方式与 readonly 标记相同。只有编译上下文存在对应特性类型时才添加状态机标记。partial 方法实现保留显式特性，不合成编译器在发射方法上省略的标记。迭代器检测针对被映射的可调用成员：嵌套局部函数中的 `yield` 不会将其外层方法标记为迭代器。

中立签名公开不可变的 `MetadataAvailability`。`UnavailableFacts` 指出哪些存储值是占位值，`Diagnostics` 解释读取失败。手工构造的签名默认使用 `MetadataAvailability.Complete`，表示提供的值与空集合均为已知事实。显式的 `Accessibility.Unknown` 即使使用该默认值也仍是未知。

方法和属性的参数集合按声明顺序建立快照。每个 `ParameterSignature.Position` 必须等于其从零开始的列表索引。构造器以 `ArgumentException` 拒绝 null 项、乱序位置、重复及间隙，不重排调用方提供的元数据。

泛型实参的槽位结构也在构造时验证，遵循[语言第 13.1 节](../superpowers/specs/2026-07-31-aspectn-language-spec.md#131-元数)及 [2026-10-20 维护者裁决](../reviews/2026-10-20-c02-generic-signature-validation.md)。声明元数与非空实参列表数量不符，属于结构非法，抛 `ArgumentException`；不通过 `MetadataAvailability` 将它改写为未知事实。合法定义允许空实参列表；完整槽位中无法恢复的类型事实仍可用 `UnsupportedTypeSignature` 表示。

原生映射支持最多 64 层类型结构（根为第一层）、最多 64 个有效类型泛型形参（含外层类型）和独立的最多 64 个方法泛型形参。超限输入在展开前抛出带 AN3005（深度）或 AN3006（元数）的 `AspectNLimitException`。该异常穿过元数据恢复流程传播；超出支持上限不是元数据事实不可用。匹配外部构造的超限中立签名时返回 Unknown 和对应诊断。`AspectNLimits` 公开两个上限。

| 签名 | 适用事实 |
| --- | --- |
| MethodSignature | Name, Kind, Accessibility, IsStatic, IsAsync, Attributes, ReturnAttributes, TypeAttributes, AssociatedProperty |
| PropertySignature | Name, Accessibility, IsStatic, Attributes, Parameters |
| ParameterSignature | Name, IsOptional, Attributes |
| TypeUseSignature | RefKind, ByReference |

Cecil 引用中已有的类型形状、名称、参数和泛型元数，无需解析定义即可使用。方法定义无法解析时，方法种类、可见性、async 标记、直接特性、返回特性和关联属性均不可用。声明类型的特性独立解析。参数定义元数据也标为不可用。使用方必须先检查 `Availability.IsKnown(...)` 才能解释占位值；不可用的空特性列表不能证明不存在特性。

直接 `HasDirectAttribute(AttributeTarget.ForType(...), ...)` 可以查询类型或方法泛型形参声明自身的特性记录。Cecil 对属于类型/方法定义的泛型形参直接读取其 `CustomAttributes`，不要求形参解析为 `TypeDefinition`；存在所查特性记录为 Matched，已知空列表为 NotMatched。仅有引用所属的泛型槽位、拿不到声明记录时仍为 Unknown/AN3004；缺少原生关联时仍为 Unknown/AN3001。缓存分别保留声明与引用的原生关联，映射顺序不改变各自可用事实。读取与类型映射保留现有可恢复异常通道，取消继续传播。此直接 API 不改变成员表达式中 `attr(type ...)` 的声明类型目标。

引用形状与精确引用分类独立：Cecil 签名可保留已知的引用标记，但 `ref`、`out`、`in`、`ref readonly` 之间的区别未知。`ByReference` 已知时可使用 `TypeUseSignature.IsByReference`。`!ref` 消费该标记；精确 ref 修饰符还需要精确分类。没有成员定义元数据时，`CecilSignatureMapper.MapTypeUse` 保留标记，不猜测精确分类。

求值只请求选择器实际消费的事实。

维护者于2026-10-14批准声明类型特性的事实优先规则：成员表达式查询的 `TypeAttributes` 已知且完整时，精确、通配、变量和复合模式均消费候选中的同一完整快照，包括已知空集合；更换类型系统不额外要求原生关联。该事实不可用时，不能解释占位列表。能够具体化的查询尝试 `HasDirectAttribute` 原生点查询，取得确定结果时不附加已恢复事实的缺失诊断；仍不可用时保留原读取诊断与能力缺失诊断，返回 Unknown。

现有 `ITypeSystem` 不提供原生特性列表枚举；缺少完整快照的通配、变量或复合查询若无法取得所需记录，仍返回 Unknown。不能把复合条件拆成多个独立存在性查询：它们可能约束同一条记录。此规则限成员表达式的类型特性消费，不改变直接后端 API 的原生关联前置条件，也不应用继承特性规则。

`ReflectionSignatureMapper.MapTypeUse` 对裸 by-ref `Type` 也保留已知 `ByReference` 并将 `RefKind` 标为不可用，因为仅靠 Type 无法区分四种引用分类。映射成员参数或返回值时保留其精确分类。Reflection 元数据读取失败使用 AN3004；缺失原生关联仍使用 AN3001。

互操作方向标志不意味着 readonly 分类：`[In] ref` 和 `[In, Out] ref` 在 Cecil、Reflection 中仍为 `ref`。`out` 使用仅出方向的元数据；`in` 和 `ref readonly` 分别需要对应的 readonly/location 标记。Reflection 方法泛型形参的使用位置通过弱原生关联，单独保留闭合声明类型的查询上下文，中立声明身份与其分开。外部泛型实参保留自身声明上下文。

程序集依赖缺失时，运行时加载器可能在 Reflection 创建参数 `Type` 之前拒绝方法签名。这是原生输入加载失败，不是适配器关系查询结果。将 Cecil 映射的中立签名传给 Reflection，验证的是脱离原生关联的输入能力处理（AN3001），不能替代已有原生对象上的 Reflection 元数据读取失败测试。

对于未解析的 `External.Widget.Run` 引用：

- `execution(* External.Widget.Run(..))` 可以匹配，因为该选择器接受所有成员种类。
- `method(* External.Widget.Run(..))` 返回未知，因为无法确认普通方法适用域。
- 可见性不可用时，正向和否定的可见性约束都返回未知。
- 在与未知事实构成的合取中，已知不同的成员名、声明类型、返回类型或参数数量仍可给出 `NotMatched`。
- 精确和通配特性选择器均尊重集合可用性。对 `any`/`exec`，已知匹配的特性优先于另一未知目标；不可读取的属性关联不能证明不存在特性。
- 正则匹配需要完整规范签名（名称、可见性、static 标志、返回及参数引用分类），这些事实不可用时返回未知；不要求 async 状态或特性集合。

所有适配器在直接特性 API 接收脱离原生关联的签名时，均遵守其可用性。Cecil 将可恢复的解析/读取失败（程序集解析、IO、错误映像、不支持的元数据、无效操作和无效元数据参数）转换为 `AN3004`。`AN3002` 仍表示无效正则，`AN3003` 仍表示正则超时。取消向外传播。未知事实未附读取诊断时，使用现有的 `AN3001` 能力缺失诊断。

`Parameters` 适用于属性签名：Cecil 从访问器元数据提取索引参数，因此损坏的延迟方法语义可能使集合不可用，空列表只是占位值。Standard 属性选择器不消费该列表，属性名、类型及直接特性仍可使用。诊断对象使用简单元数据名，避免格式化消息时再次触发未保护的延迟读取。已解析方法引用从定义获取参数名。

关系查询保留不可读分支为 Unknown，同时继续其他分支。已知匹配优先于先前的 Unknown；没有分支匹配时保留读取失败。Cecil 既保护 `Resolve()`，也保护延迟节点展开与缓存键读取。数组声明者拥有自己的已知空直接特性集合，不继承元素的特性。

本契约于 2026-10-05 获批，取代 Shared/Standard 比较文档中此前对未解析方法的限制。对于原已明确超出适配器分类覆盖范围的有效元数据形式，本契约不改变其后端支持状态。
