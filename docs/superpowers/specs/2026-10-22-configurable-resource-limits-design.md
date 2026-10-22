# AspectN 分阶段资源限制契约

## 1. 批准来源与取代范围

本规范落实用户于 2026-10-21 批准的[实施计划](../../plans/2026-10-21-configurable-resource-limits.md)。取代语言规范第 21 节和双变量模式设计中固定资源阈值、旧编译选项签名及文本超限诊断的规定；Declaration/Capture 语言语义和 C01—C07、D01、U01 的其他裁决不变。历史报告保留其当时输入、默认值和结果，不自动成为新配置的证据。

## 2. 公开配置与入口

配置对象不可变，只保存策略。`ResourceLimit.Finite(long)` 接受正数；`ResourceLimit.Unlimited` 显式关闭该项。`TimeLimit.Finite(TimeSpan)` 接受大于零且不超过 2,147,483,646 毫秒的超时；`TimeLimit.Unlimited` 显式关闭正则超时。默认结构值无效；选项参数省略或为 null 使用默认值。有限额度恰好用完允许完成，再申请一个单位失败。无限额度仍遵守取消、其他额度和签名结构合法性。

| 入口 | 配置与取消 |
| --- | --- |
| `AspectNParser.Parse*` | `(source, parseOptions = null, cancellationToken = default)` |
| `AspectNEngine.Compile*` | `(source, parseOptions = null, compilationOptions = null, cancellationToken = default)` |
| `AspectNCompilation.Create` | `(tree, options = null, cancellationToken = default)` |
| `AspectNCompilationOptions` | `(genericVariableMode = Declaration, matchOptions = null)` |
| Mapper `MapType/MapTypeUse/MapMethod/MapProperty` | 保留原生输入和上下文参数，最后增加可选取消令牌 |
| `ITypeSystem` 四个查询 | 普通输入及可选取消令牌，包括 `AreEqual` |

语法树保存解析配置，后续绑定使用该配置。编译计划固定变量模式和匹配配置，每次 Match 不提供覆盖入口。移除 `AspectNLimits` 和旧编译选项重载，调用者统一迁移。

## 3. 默认值和计数口径

### 3.1 解析与绑定

| `AspectNParseOptions` 属性 | 默认值 | 口径 |
| --- | ---: | --- |
| `MaximumSourceLength` | 256 | 完整输入的 UTF-16 代码单元，含空白、换行和表达式转义 |
| `MaximumTokens` | 1,024 | 实际扫描的非 EOF token；重新扫描继续计数 |
| `MaximumSyntaxNodes` | 2,048 | 实际创建的节点，含 token、EOF 及临时节点 |
| `MaximumParserDepth` | 128 | 解析逻辑帧，根为 1 |
| `MaximumSyntaxDepth` | 256 | 最终语法树深度，根为 1 |
| `MaximumTypeDepth` | 64 | 类型结构深度，根为 1 |
| `MaximumTypeGenericParameters` | 64 | 有效类型槽位，含外层类型 |
| `MaximumMethodGenericParameters` | 64 | 方法自身槽位，与类型额度独立 |

源码长度在散列和解析前检查，不通过裁剪或删空白规避。类型包含链、数组、包装和泛型实参按既有结构口径展开；原始定义、声明者和修饰符身份分别校验，不因身份循环重复展开。

### 3.2 原生映射

| `AspectNMappingOptions` 属性 | 默认值 | 口径 |
| --- | ---: | --- |
| `MaximumTypeDepth` | 64 | 原生类型结构深度 |
| `MaximumTypeGenericParameters` | 64 | 含外层的有效类型槽位 |
| `MaximumMethodGenericParameters` | 64 | 方法自身槽位 |
| `MaximumWork` | 100,000 | 一次最外层映射累计处理规模 |
| `MaximumAttributeRecords` | 1,024 | 累计扫描记录，含被过滤的记录 |
| `MaximumGeneratedTextLength` | 16,384 | 每个生成的类型、缓存键、OwnerId 文本的 UTF-16 长度 |

一次最外层 Map 调用的声明类型、原始定义、返回、参数、特性、修饰符共享额度。Cecil 在 `CecilMappingOptions.Resources` 保存配置，继续保留 `LeaveOpen`；Reflection/Roslyn 在实例构造时接收映射配置。

### 3.3 匹配与查询

| `AspectNMatchOptions` 属性 | 默认值 | 口径 |
| --- | ---: | --- |
| `MaximumTypeDepth` | 64 | 候选和查询涉及的类型结构 |
| `MaximumTypeGenericParameters` | 64 | 含外层的有效类型槽位 |
| `MaximumMethodGenericParameters` | 64 | 方法自身槽位 |
| `MaximumWork` | 250,000 | 预检查、求值、比较、格式化及内置查询累计工作 |
| `MaximumRelationNodes` | 1,024 | 查询新展开节点，不同查询重新累计 |
| `MaximumRelationEdges` | 4,096 | 考察关系边，重复边也计数 |
| `MaximumAttributeRecords` | 1,024 | 扫描记录，含被过滤的记录 |
| `MaximumCanonicalSignatureLength` | 16,384 | 每个规范签名的 UTF-16 长度 |
| `MaximumBindingAlternatives` | 256 | Capture 每个收集器存活的不同备选 |
| `MaximumBindingWork` | 50,000 | 整次 Match 的 Capture 绑定处理工作 |
| `RegexTimeout` | 250 ms | 每次正则匹配；格式化不在该计时范围内 |

工作单位不是毫秒或字节。节点访问、边考察、特性扫描、绑定处理及字符串字符处理在受控工作或分配前收费；专项工作同时消耗所属总工作额度，同一作用域不重复收取同一动作。绑定去重仍消耗工作。Declaration 不创建 Capture 环境，但仍消耗通用工作额度。缓存只省去实际未执行的工作，不能绕过结构检查。

## 4. 作用域、后端与并发

每次 Match 创建内部上下文，所有分支和内置查询共享累计预算。不同调用独立；计数不存入配置、编译计划或适配器的当前操作字段，不使用环境线程状态。内部计数支持原子扣减。

内置类型系统构造时另存 `queryOptions`，仅用于独立公开查询。编译计划内部查询使用计划的匹配配置，不与独立查询配置叠加或取最小值。每次查询内部所有映射共享一个映射子预算，同时消耗外层查询/匹配预算。映射限额仍由实例配置决定。

`TypeSystemQueryContext`、`IBudgetAwareTypeSystem` 和计量映射入口均为内部接口；Core 的公共 API 不引用后端类型。第三方仅实现普通 `ITypeSystem`，收到原取消令牌。Core 对自身工作、派发和返回结果消费计量，并在返回后复查取消；无法精确计量第三方内部遍历或抢占忽略取消的实现，也不能约束其返回前的分配。第三方装饰器调用内置公开查询仍形成独立作用域，不自动接入内部累计预算。第三方返回 AN3007/AN3008 时，Core 传播整次资源中止。

## 5. 失败与取消

| 原因 | 结果 |
| --- | --- |
| 无效限额值或变量模式 | 构造配置时抛 `ArgumentOutOfRangeException` |
| 解析源码、token 或节点额度 | 编译错误 AN1003 |
| 解析/语法/类型深度 | AN1002 |
| 显式泛型条款超槽 | AN2011 |
| 类型深度/元数 | 显式映射抛 `AspectNLimitException`；匹配/查询 Unknown，AN3005/AN3006 |
| Capture 专项额度 | 整次 Unknown/AN3007 |
| 其他累计工作、边、节点、特性或文本额度 | 显式映射抛限额异常；匹配/查询整次 Unknown/AN3008 |
| 正则超时 | 局部 Unknown/AN3003，保留既有三态逻辑 |
| 取消 | 抛 `OperationCanceledException` |

AN3005/AN3006 保留 C05 的局部分支行为：超限祖先留诊断并可继续其他合法祖先。AN3007/AN3008 标记整次资源中止，局部 OR 成功不能覆盖它。查询可返回已知闭包前缀及不完整标记，匹配器不能用资源耗尽前缀给出确定成功。真正缺少格式化事实仍为 AN3001；文本配额耗尽改为 AN3008。诊断包含阶段、选项、限额和已用/请求量，尽可能保存跨度及已有诊断。

预算和发布边界先检查取消，包括编译缓存命中、映射命中和查询返回。资源通道不吞任意第三方异常，不承诺 OOM 恢复。U01 必要签名损坏继续映射失败。

## 6. 执行与缓存

自身依赖输入深度的路径使用显式工作栈，涵盖解析、绑定、求值、具体化、格式化、原生映射、声明身份和修饰符。提高额度或 Unlimited 不依赖有限 CLR 调用栈；外部运行时或原生库自己的限制仍存在。宽输入逐项处理，在受控扩栈、复制、合并和文本分配前检查额度。

编译缓存键按值包含源码、解析种类、全部解析和匹配选项及变量模式。不同实例但相同字段可命中同一条目。只发布完成结果；正常完成但含错误诊断的编译结果可以缓存，异常或取消不进入缓存。并发未命中各自编译和响应取消，完成时竞争发布，不共享调用方令牌。LRU 容量仍由 `AspectNEngineOptions.CacheCapacity` 控制，默认 256，零关闭；不提供无限缓存。

## 7. 验证边界

默认值是处理规模估算，不构成耗时或内存保证。最终验收记录正常回归、各阶段边界、显式提高/无限额度、小栈深输入、并发隔离、第三方边界、实际 XML 示例和四程序集身份；不能从单个后端或输入外推全部组合。报告与场景范围由[知识库](../../knowledge/index.md)维护，性能基线仍为独立任务。
