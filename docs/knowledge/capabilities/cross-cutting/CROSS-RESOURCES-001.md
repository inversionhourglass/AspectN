# CROSS-RESOURCES-001 取消、类型深度、元数与文本预算

<!-- aspectn-capability:v1
{
  "schema_version": 1,
  "id": "CROSS-RESOURCES-001",
  "title": "取消、类型深度、元数与文本预算",
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
    }
  ],
  "scenarios": [
    {
      "id": "S1",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "Parse/Compile / CompiledPattern.Match",
      "conditions": "InputDepthTests、SignatureLimitTests、RegexPatternTests 和 MemberPathTests 的实际边界及取消 fixture；InheritanceClosureLimitTests 的第三方祖先深度/元数、64边界、正常分支顺序和Unknown绑定备选；原C05变量备选用例显式Capture",
      "target": "working-tree",
      "expected_behavior": "结构深度64含根、类型有效元数64和方法自身元数64；解析/编译给诊断，外部中立匹配超限 Unknown；取消传播；核心闭包模式遍历对每个祖先先检查限制，超限为Unknown并保留诊断和备选，继续其他祖先，确定匹配仍为Matched",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-001",
        "EV-20261021-002",
        "EV-20261021-007"
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
        "EV-20261021-005"
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
        "EV-20261021-005"
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
        "EV-20261021-005"
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
        "EV-20261021-005"
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
        "EV-20261014-010"
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
        "EV-20261014-010"
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
        "EV-20261021-007"
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

类型结构最多64层含根；类型泛型有效元数含外层链最多64，方法自身最多64，二者可以各64。解析另有递归入口128、语法树深度256保护。规范正则签名预算65,536个UTF-16字符；匹配/关系声明 CancellationToken 的入口传播取消。

## 输入条件

原生生产者映射与已构造中立签名分开；父/祖先节点、custom modifier 和共享子图按规范计数。相同元素节点在不同深度路径不能跳过更深路径的检查。

## 分层能力

适配器在递归关联、缓存键和位置实参合成前检查原生深度/元数；核心在匹配前检查外部中立输入；格式化器追加前检查预算并遵守取消，正则超时随后才开始。

核心对第三方闭包返回的每个祖先也先运行限制检查。超限祖先不进入模式求值或变量绑定，只保留 Unknown 备选及原限制诊断，再继续其他祖先。正常匹配分支可使结果为 Matched，限制诊断仍保留；后续合取不能因过滤掉某个已知绑定而丢失未知备选。S1 的新增回归与 TYPE-NOMINAL-001/S6 共用 [InheritanceClosureLimitTests](../../../../tests/AspectN.Tests/Evaluation/InheritanceClosureLimitTests.cs)，不推断内置适配器会返回这些图。

## 阶段失败

显式原生映射抛 AspectNLimitException/AN3005或AN3006；HasDirectAttribute 内部原生特性类型映射超限则在查询边界返回 Unknown 并保留原诊断；中立匹配 Unknown及同码；解析保护AN1002、显式泛型子句超槽AN2011；规范文本预算Unknown/AN3001。取消不转成 Unknown/AN3004。

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

## 关联与缺口

CROSS-AVAILABILITY-001 处理事实缺失，SEM-REGEX-001 处理正则编译/超时。未穷举全部线程调度或GC时机；资源测试不等于性能基准结论。

本轮新增detached继承闭包取消反例（S6/S7）：2026-10-13普通Cecil与Fody均出现已确认违约，当前已修复，见[取消Finding](../../../reviews/findings/FND-20261013-001.md)。原已关联候选的执行证据保持其限定范围。

S6/S7 的提前返回吞取消已修复：CecilResolver.GetInheritanceClosure 在关联查找之前检查 Token。普通 Cecil 与 Fody 分别执行关联/脱离关联预先取消及未取消缺关联控制，见 [CecilCancellationTests](../../../../tests/AspectN.Cecil.Tests/CecilCancellationTests.cs)。

Capture 默认每集合 4096 个唯一备选、每次 Match 1,000,000 个累计逻辑工作单位；逐项去重计费、复制前检查。耗尽即整体 Unknown/AN3007，保留诊断和触发跨度，取消优先。它不是墙钟或精确字节配额，不抢占第三方后端/闭包物化。Declaration 固定槽位缓存不使用动态预算。独立后端入口预算断言见 GEN-BINDING-001/S7–S11；兼容资产见 CROSS-COMPAT-001/S3–S4。[压力报告](../../../reviews/2026-10-21-generic-variable-modes.md)不代替整体性能闸门。
