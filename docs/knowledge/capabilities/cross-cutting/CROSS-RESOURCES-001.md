# CROSS-RESOURCES-001 分阶段资源配置、预算与取消

<!-- aspectn-capability:v1
{
  "schema_version": 1,
  "id": "CROSS-RESOURCES-001",
  "title": "分阶段资源配置、预算与取消",
  "category": "cross-cutting",
  "kind": "cross_cutting",
  "profile": "cross-cutting-v1",
  "norm_refs": [
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
      "section": "5 / 15 / 21"
    },
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-redesign-design.md",
      "section": "13"
    },
    {
      "path": "docs/reviews/2026-10-21-d01-direct-attribute-limits.md",
      "section": "维护者裁决与取代范围"
    },
    {
      "path": "docs/superpowers/specs/2026-10-22-configurable-resource-limits-design.md",
      "section": "2—6；仅资源策略与公开入口"
    }
  ],
  "scenarios": [
    {
      "id": "S1",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "Parse/Compile / CompiledPattern.Match",
      "conditions": "InputDepthTests、SignatureLimitTests、RegexPatternTests 和 MemberPathTests 的实际边界及取消 fixture；InheritanceClosureLimitTests 的第三方祖先深度/元数、64边界、正常分支顺序和Unknown绑定备选；原C05变量备选用例显式Capture；RegexReuseTests：跨规则分配、默认/有限/Unlimited超时持有、同值缓存、共享模式并发及复用后取消",
      "target": "working-tree",
      "expected_behavior": "默认结构深度64含根、默认类型有效元数64和默认方法自身元数64；解析/编译给诊断，外部中立匹配超限 Unknown；取消传播；核心闭包模式遍历对每个祖先先检查限制，超限为Unknown并保留诊断和备选，继续其他祖先，确定匹配仍为Matched",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-001",
        "EV-20261021-002",
        "EV-20261021-007",
        "EV-20261022-201",
        "EV-20261023-305"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S2",
      "adapter": "cecil",
      "input_mode": "native_member",
      "entry_point": "Mapper / IsAssignableTo / HasDirectAttribute",
      "conditions": "SignatureLimitConformanceTests、ModifierLimitConformanceTests、MappingDepthTests 的实际后端输入和小栈隔离探针；D01直接特性查询边界见ATTR-TARGETS-001/S21",
      "target": "working-tree",
      "expected_behavior": "显式原生映射超限 AspectNLimitException 带 AN3005/3006；祖先超限不遮蔽其他已知匹配；不伪装为普通不可用事实；直接特性查询内部映射超限返回Unknown并保留原AN3005/AN3006，显式Mapper异常通道不变",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-006",
        "EV-20261021-005",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S3",
      "adapter": "roslyn",
      "input_mode": "source_symbol",
      "entry_point": "Mapper / IsAssignableTo / HasDirectAttribute",
      "conditions": "SignatureLimitConformanceTests、ModifierLimitConformanceTests、MappingDepthTests 的实际后端输入和小栈隔离探针；D01直接特性查询边界见ATTR-TARGETS-001/S24",
      "target": "working-tree",
      "expected_behavior": "显式原生映射超限 AspectNLimitException 带 AN3005/3006；祖先超限不遮蔽其他已知匹配；不伪装为普通不可用事实；直接特性查询内部映射超限返回Unknown并保留原AN3005/AN3006，显式Mapper异常通道不变",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-006",
        "EV-20261021-005",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S4",
      "adapter": "reflection",
      "input_mode": "native_member",
      "entry_point": "Mapper / IsAssignableTo / HasDirectAttribute",
      "conditions": "SignatureLimitConformanceTests、ModifierLimitConformanceTests、MappingDepthTests 的实际后端输入和小栈隔离探针；D01直接特性查询边界见ATTR-TARGETS-001/S23",
      "target": "working-tree",
      "expected_behavior": "显式原生映射超限 AspectNLimitException 带 AN3005/3006；祖先超限不遮蔽其他已知匹配；不伪装为普通不可用事实；直接特性查询内部映射超限返回Unknown并保留原AN3005/AN3006，显式Mapper异常通道不变",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-006",
        "EV-20261021-005",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S5",
      "adapter": "cecil_fody",
      "input_mode": "native_member",
      "entry_point": "Mapper / IsAssignableTo / HasDirectAttribute",
      "conditions": "独立 Fody 测试程序集中的 CecilSignatureLimitTests 和取消用例；D01直接特性查询边界见ATTR-TARGETS-001/S22",
      "target": "working-tree",
      "expected_behavior": "相同核心上限在 Fody Cecil 身份下执行；关系取消令牌/解析器取消异常向外传播；直接特性查询内部映射超限返回Unknown并保留原AN3005/AN3006，显式Mapper异常通道不变",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-003",
        "EV-20261021-005",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S6",
      "adapter": "cecil",
      "input_mode": "detached",
      "entry_point": "ITypeSystem.GetInheritanceClosure",
      "conditions": "正常空模块上下文、有效外部NamedTypeSignature、调用前已取消；关联候选/未取消输入及Reflection/Roslyn作对照",
      "target": "working-tree",
      "expected_behavior": "预先取消传播OperationCanceledException；未取消的无原生关联输入保留Unknown/AN3001",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-002",
        "EV-20261014-010",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S7",
      "adapter": "cecil_fody",
      "input_mode": "detached",
      "entry_point": "ITypeSystem.GetInheritanceClosure",
      "conditions": "正常空模块上下文、有效外部NamedTypeSignature、调用前已取消；关联候选/未取消输入及Reflection/Roslyn作对照",
      "target": "working-tree",
      "expected_behavior": "预先取消传播OperationCanceledException；未取消的无原生关联输入保留Unknown/AN3001",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-003",
        "EV-20261014-010",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S8",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "Compile* / CompiledPattern.Match",
      "conditions": "BindingBudgetTests：Capture工作1/2/3边界、数量1/2边界、去重、复制前收费、Fork共享、属性/祖先收集、局部Matched后中止、取消优先、诊断保存、重复/并发重置及8/12/13组压力",
      "target": "working-tree",
      "expected_behavior": "正限额且分支共享每次Match预算，耗尽整次Unknown/AN3007；不截断解集，不吞取消/第三方异常；Declaration无动态环境且不消耗捕获预算",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261021-007",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S9",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "Parse* / Compile* / AspectNCompilation.Create",
      "conditions": "ResourceOptionsTests、ResourceContractTests、ResourceXmlExampleTests、TypeSyntaxParityTests及FrontendProbe：256/257、UTF-16/空白/换行/转义、token/节点/根深度、类型/方法元数、保留解析选项；千层解析/恢复/绑定小栈",
      "target": "working-tree",
      "expected_behavior": "新默认值与配置按值生效；重扫/临时节点继续计量；AN1003/1002/2011；提高或Unlimited跨过旧限制，取消传播",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated",
        "manual"
      ],
      "evidence_ids": [
        "EV-20261022-201",
        "EV-20261022-202",
        "EV-20261023-304"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S10",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "CompiledPattern.Match / StructuralTypeComparer / cache",
      "conditions": "ResourceBudgetTests、BindingBudgetTests、CompiledPatternCacheTests和ResourceContractTests：宽DTO/长文本提前检查、OR资源中止、第三方资源诊断、Capture默认4/8/9组、并发与缓存取消；不含第三方内部计量",
      "target": "working-tree",
      "expected_behavior": "AN3007/3008整次中止；结构失败局部；默认8组256备选允许，9组中止；独立调用互不消耗；配置全字段入缓存，异常/取消不发布",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated",
        "manual"
      ],
      "evidence_ids": [
        "EV-20261022-201",
        "EV-20261022-202"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S11",
      "adapter": "cecil",
      "input_mode": "native_member",
      "entry_point": "Mapper / public query / internal match query",
      "conditions": "NativeResourceBudgetTests Cecil参数行、CecilResourceBudgetTests、MappingDepthTests：工作/文本/特性、独立查询、查询子预算、跨查询三边累计、类型/方法80槽、千层形状和2000层直接特性目标；关系节点含根的逐次重置和普通第三方装饰器作用域对照",
      "target": "working-tree",
      "expected_behavior": "固定映射配置与计划匹配配置分别生效；子映射共享额度并通知外层；AN3008整次中止；提高/不限额度保留结构语义和取消；失败不发布残缺根",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated",
        "manual"
      ],
      "evidence_ids": [
        "EV-20261022-201",
        "EV-20261022-202"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S12",
      "adapter": "cecil_fody",
      "input_mode": "native_member",
      "entry_point": "Mapper / native query",
      "conditions": "独立Fody程序集链接CecilResourceBudgetTests：长命名空间、调用约定列表、600个工作额度的失败发布重试、类型/方法限额独立；既有深度/元数/取消回归",
      "target": "working-tree",
      "expected_behavior": "相同源码在Fody Cecil身份执行这些资源检查；失败发布重试保留关联。未据此宣称全部NativeResourceBudgetTests或80槽同源PE在Fody已执行",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated",
        "manual"
      ],
      "evidence_ids": [
        "EV-20261022-201",
        "EV-20261022-202"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S13",
      "adapter": "reflection",
      "input_mode": "native_member",
      "entry_point": "Mapper / public query / internal match query",
      "conditions": "NativeResourceBudgetTests Reflection参数行、Reflection ResourceBudgetTests、MappingDepthTests：独立策略、查询映射子预算和累计边、80槽、千层数组、万层委托类型前检查；关系节点含根的逐次重置和普通第三方装饰器作用域对照",
      "target": "working-tree",
      "expected_behavior": "映射和匹配独立配置；独立公开查询与内部查询作用域区分；提前检查工作、文本和结构，取消优先；原生API自身限制仍存在",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated",
        "manual"
      ],
      "evidence_ids": [
        "EV-20261022-201",
        "EV-20261022-202"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S14",
      "adapter": "roslyn",
      "input_mode": "source_symbol",
      "entry_point": "Mapper / public query / internal match query",
      "conditions": "NativeResourceBudgetTests Roslyn参数行、Roslyn ResourceBudgetTests：特性/关系/查询子预算、累计边、80槽源符号；129字符错误符号与100空语句方法体扫描；关系节点含根的逐次重置和普通第三方装饰器作用域对照",
      "target": "working-tree",
      "expected_behavior": "错误类型生成文本也受限；源码yield检测扫描计费并传取消；独立查询策略不覆盖计划；子预算资源失败按AN3008传播",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated",
        "manual"
      ],
      "evidence_ids": [
        "EV-20261022-201",
        "EV-20261022-202"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S15",
      "adapter": "roslyn",
      "input_mode": "pe_symbol",
      "entry_point": "Mapper.MapMethod / CompiledMemberPattern.Match",
      "conditions": "NativeResourceBudgetTests.MapMethod_RaisedAndUnlimitedArity_AgreesAcrossNativeViews 的80个类型及方法槽位同源PE；有限80和Unlimited两行",
      "target": "working-tree",
      "expected_behavior": "提高映射和匹配元数后成功，独立查询默认64仍Unknown；不将PE槽位控制外推所有新增资源配置",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated",
        "manual"
      ],
      "evidence_ids": [
        "EV-20261022-201",
        "EV-20261022-202"
      ],
      "unverified_notes": ""
    }
  ],
  "invariant_checks": [
    {
      "invariant_id": "INVLOGIC",
      "applicability": "applicable",
      "condition_ref": {
        "path": "docs/knowledge/profiles.json",
        "section": "invariants/INVLOGIC"
      },
      "reason": "条目包含匹配或关系结果，遵循三态组合；具体覆盖按场景",
      "review_status": "pending",
      "reviewer": null,
      "reviewed_on": null
    },
    {
      "invariant_id": "INVPARITY",
      "applicability": "applicable",
      "condition_ref": {
        "path": "docs/knowledge/profiles.json",
        "section": "invariants/INVPARITY"
      },
      "reason": "涉及适配器映射或关系时只比较同等输入和所需可用事实，不能外推缺失后端行",
      "review_status": "pending",
      "reviewer": null,
      "reviewed_on": null
    },
    {
      "invariant_id": "INVMISSING",
      "applicability": "applicable",
      "condition_ref": {
        "path": "docs/knowledge/profiles.json",
        "section": "invariants/INVMISSING"
      },
      "reason": "候选所需事实或原生关联可能不可用，不能把占位值当作确定事实",
      "review_status": "pending",
      "reviewer": null,
      "reviewed_on": null
    },
    {
      "invariant_id": "INVCANCEL",
      "applicability": "applicable",
      "condition_ref": {
        "path": "docs/knowledge/profiles.json",
        "section": "invariants/INVCANCEL"
      },
      "reason": "条目纳入完整匹配或公开关系入口，其 CancellationToken 必须按横向契约传播",
      "review_status": "pending",
      "reviewer": null,
      "reviewed_on": null
    }
  ],
  "relations": [
    {
      "type": "depends_on",
      "capability_id": "CROSS-AVAILABILITY-001"
    },
    {
      "type": "depends_on",
      "capability_id": "CROSS-LOGIC-001"
    }
  ],
  "open_findings": [],
  "related_findings": [
    "FND-20261013-001",
    "FND-20261021-002",
    "FND-20261021-005",
    "FND-20261021-007"
  ]
}
-->

## 摘要

限制按阶段体现，取消是独立控制流；深度、元数、正则时间与规范文本预算不是同一个开关。

## 契约

解析、映射、匹配分别使用不可变配置和每次操作预算。默认类型深度64含根，含外层类型槽位64和方法自身槽位64互相独立；解析逻辑帧128、树深256、完整源码256。规范文本默认16,384 UTF-16代码单元。全部项目支持正有限值或显式Unlimited，默认值和计数口径见[资源规范](../../../superpowers/specs/2026-10-22-configurable-resource-limits-design.md)及[使用指南](../../../usage/resource-limits.md)。

## 输入条件

原生生产者映射与已构造中立签名分开；父/祖先节点、custom modifier 和共享子图按规范计数。相同元素节点在不同深度路径不能跳过更深路径的检查。

## 分层能力

适配器在递归关联、缓存键和位置实参合成前检查原生深度/元数；核心在匹配前检查外部中立输入；格式化器追加前检查预算并遵守取消，正则超时随后才开始。

核心对第三方闭包返回的每个祖先也先运行限制检查。超限祖先不进入模式求值或变量绑定，只保留 Unknown 备选及原限制诊断，再继续其他祖先。正常匹配分支可使结果为 Matched，限制诊断仍保留；后续合取不能因过滤掉某个已知绑定而丢失未知备选。S1 的新增回归与 TYPE-NOMINAL-001/S6 共用 [InheritanceClosureLimitTests](../../../../tests/AspectN.Tests/Evaluation/InheritanceClosureLimitTests.cs)，不推断内置适配器会返回这些图。

## 阶段失败

显式原生映射抛 AspectNLimitException/AN3005或AN3006；HasDirectAttribute 内部原生特性类型映射超限则在查询边界返回 Unknown 并保留原诊断；中立匹配 Unknown及同码；解析深度AN1002、显式泛型子句超槽AN2011、源码/token/节点AN1003；规范文本及总工作、关系、特性额度耗尽Unknown/AN3008，Capture专项AN3007。资源耗尽中止整次操作，不由成功OR覆盖；真正缺失格式化事实仍AN3001。取消不转成 Unknown/AN3004。

D01 的查询边界由维护者于 2026-10-21 批准，S2–S5 增加对应入口，Roslyn PE 单列于 ATTR-TARGETS-001/S25。当前新证据仅补该查询及显式映射控制，不替代旧小栈、修饰符或关系遍历证据。

## 例子与边界

```csharp
using var cancellation = new CancellationTokenSource();
cancellation.Cancel();
// pattern 为已编译类型模式，signature 为已映射候选。
_ = pattern.Match(signature, typeSystem, cancellation.Token);
// 抛 OperationCanceledException。
```

int 根加64层数组总共65层；不能说“64个数组后缀仍在支持范围内”。同一超深结构的原生映射和中立匹配表现不同，完整可运行例见[边界示例](../../../usage/examples/MatchingBoundaries/Program.cs)。上限是契约边界，不据测试耗时另设性能承诺。

## 规范依据

语言第 21 节、正则第 15 节和架构第 13 节及可用性专题。

## 实现与证据

[核心边界](../../../../tests/AspectN.Tests/Fuzz/SignatureLimitTests.cs)、[原生边界](../../../../tests/AspectN.Conformance.Tests/SignatureLimitConformanceTests.cs)、[小栈探针](../../../../tests/AspectN.Conformance.Tests/MappingDepthTests.cs)。报告记录隔离进程及实际环境。

2026-10-23 的 RM-18 将类型解析算法共享，S9 的当前前端回归见 EV-20261023-304。保留两个入口的节点预留和 Peek 收费顺序，86 项新增对照与 2,160 项有限探针记录所列边界；四个 Core/1000 深度泛型解析及编译形状在 256 KiB 线程通过。历史其他输入保持原范围，不将这些观察扩展为所有恢复形状或取消时序。详情见[共享解析记录](../../../reviews/2026-10-23-rm18-shared-type-parser.md)；默认限额和 N/A 未变。

## 关联与缺口

2026-10-23 RM-19 的 Regex 实例保留遵循编译配置中的超时，不复用其他超时的计划。S1 的正则文本限额、超时Unknown和取消随完整矩阵重验，新增复用后的取消及默认/有限/Unlimited 配置持有回归；[处置记录](../../../reviews/2026-10-23-rm19-regex-reuse.md)与 EV-20261023-305 限定当前执行范围。预算和公开资源配置未变，未新增内存配额。

CROSS-AVAILABILITY-001 处理事实缺失，SEM-REGEX-001 处理正则编译/超时。未穷举全部线程调度或GC时机；资源测试不等于性能基准结论。

本轮新增detached继承闭包取消反例（S6/S7）：2026-10-13普通Cecil与Fody均出现已确认违约，当前已修复，见[取消Finding](../../../reviews/findings/FND-20261013-001.md)。原已关联候选的执行证据保持其限定范围。

S6/S7 的提前返回吞取消已修复：CecilResolver.GetInheritanceClosure 在关联查找之前检查 Token。普通 Cecil 与 Fody 分别执行关联/脱离关联预先取消及未取消缺关联控制，见 [CecilCancellationTests](../../../../tests/AspectN.Cecil.Tests/CecilCancellationTests.cs)。

Capture 默认每集合 256 个唯一备选、每次 Match 50,000 个累计绑定工作单位；逐项去重计费、复制前检查。耗尽即整体 Unknown/AN3007，保留诊断和触发跨度，取消优先。它不是墙钟或精确字节配额，不抢占第三方后端/闭包物化。Declaration 固定槽位缓存不使用Capture预算，仍受总工作额度约束。独立后端入口预算断言见 GEN-BINDING-001/S7–S11；兼容资产见 CROSS-COMPAT-001/S3–S4。[压力报告](../../../reviews/2026-10-21-generic-variable-modes.md)不代替整体性能闸门。

2026-10-22资源配置变更按[实施审查](../../../reviews/2026-10-22-configurable-resource-limits.md)重评当前证据。历史报告不改写；新增配置的范围由CROSS-RESOURCES-001/S9—S15区分。未新增或改动N/A记录；没有从缺少某后端场景推断排除。
