# CROSS-AVAILABILITY-001 元数据可用性与独立事实

<!-- aspectn-capability:v1
{
  "schema_version": 1,
  "id": "CROSS-AVAILABILITY-001",
  "title": "元数据可用性与独立事实",
  "category": "cross-cutting",
  "kind": "cross_cutting",
  "profile": "cross-cutting-v1",
  "norm_refs": [
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
      "section": "5"
    },
    {
      "path": "docs/language/metadata-availability.md",
      "section": "元数据可用性"
    }
  ],
  "scenarios": [
    {
      "id": "S1",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "CompiledMemberPattern.Match",
      "conditions": "MetadataAvailabilityTests 的 Service.Run：Accessibility 或单个属性等事实不可用，其他事实已知",
      "target": "working-tree",
      "expected_behavior": "只查询消费的事实；未知不等于否；已知失败可支配 AND，已知成功可支配 OR",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-001"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S2",
      "adapter": "cecil",
      "input_mode": "reference",
      "entry_point": "Mapper / HasDirectAttribute / CompiledMemberPattern.Match",
      "conditions": "CecilUnavailableMetadataTests 的未解析引用、Int32 引用形状、六类 resolver 失败和取消；CecilSignatureLimitTests 指定原生边界",
      "target": "working-tree",
      "expected_behavior": "保留独立名称/形状；消费缺失定义事实 Unknown/AN3004；取消传播，指定深度/元数超限异常不伪装缺事实",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-002"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S3",
      "adapter": "roslyn",
      "input_mode": "source_symbol",
      "entry_point": "Mapper.MapType / IsAssignableTo",
      "conditions": "UnavailableAncestorTests 的 C:Missing,I 源符号；SignatureLimitConformanceTests 的指定 Roslyn 结构/元数输入",
      "target": "working-tree",
      "expected_behavior": "已知接口匹配不被未知基类掩盖；未满足关系 Unknown；取消传播；指定原生超限拒绝",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-004",
        "EV-20261014-006"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S4",
      "adapter": "reflection",
      "input_mode": "bare_type",
      "entry_point": "Mapper.MapType / IsAssignableTo / GetInheritanceClosure",
      "conditions": "ReflectionMappingTests 的 GetInterfaces 抛 TypeLoadException 的 TypeDelegator；ReflectionPartialClosureTests 的不可读接口与已知基类；不包含无法形成 Type 的缺依赖输入",
      "target": "working-tree",
      "expected_behavior": "所需关系读取失败 Unknown/AN3004，已知基类匹配仍可决定结果；不承诺任意 MapMethod.Getter 异常都可恢复",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-005",
        "EV-20261014-006"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S5",
      "adapter": "cecil_fody",
      "input_mode": "reference",
      "entry_point": "Mapper / HasDirectAttribute / Match",
      "conditions": "独立 Fody 测试程序集的 CecilUnavailableMetadataTests 和 CecilSignatureLimitTests",
      "target": "working-tree",
      "expected_behavior": "在 Fody Cecil 身份下核验同一已列引用/读取恢复、取消和原生边界输入",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-003"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S6",
      "adapter": "cecil",
      "input_mode": "detached",
      "entry_point": "HasDirectAttribute",
      "conditions": "AttributeAvailabilityTests 的手工有效 method/return/property/parameter 目标；Attributes/ReturnAttributes 不可用且带 AN3004",
      "target": "working-tree",
      "expected_behavior": "三后端分别返回 Unknown 并保留读取诊断；不要求存在原生关联，不代表完整事实签名一概 Unknown",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-006"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S7",
      "adapter": "roslyn",
      "input_mode": "detached",
      "entry_point": "HasDirectAttribute",
      "conditions": "AttributeAvailabilityTests 的手工有效 method/return/property/parameter 目标；Attributes/ReturnAttributes 不可用且带 AN3004",
      "target": "working-tree",
      "expected_behavior": "三后端分别返回 Unknown 并保留读取诊断；不要求存在原生关联，不代表完整事实签名一概 Unknown",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-006"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S8",
      "adapter": "reflection",
      "input_mode": "detached",
      "entry_point": "HasDirectAttribute",
      "conditions": "AttributeAvailabilityTests 的手工有效 method/return/property/parameter 目标；Attributes/ReturnAttributes 不可用且带 AN3004",
      "target": "working-tree",
      "expected_behavior": "三后端分别返回 Unknown 并保留读取诊断；不要求存在原生关联，不代表完整事实签名一概 Unknown",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-006"
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
      "reason": "缺失事实的查询结果组合遵循 AND/OR/NOT",
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
      "reason": "后端比较须在事实同等可用条件下进行",
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
      "reason": "该条目直接定义缺失事实处理",
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
      "reason": "CompiledMemberPattern.Match 声明取消且入口预检传播；试点当前不专门验证取消",
      "review_status": "pending",
      "reviewer": null,
      "reviewed_on": null
    }
  ],
  "relations": [
    {
      "type": "related_to",
      "capability_id": "CROSS-LOGIC-001"
    }
  ],
  "open_findings": [],
  "related_findings": [
    "FND-20261011-001"
  ]
}
-->

## 摘要

默认 Complete 代表手工传入值已知；显式 Unknown 和 unavailable placeholder 有独立含义。

## 契约

Availability 标记占位事实。空 unavailable 属性列表不证明没有属性。手工中立签名默认 Complete；Accessibility.Unknown 仍未知。参数声明位置必须等于零起列表下标，不自动重排。

## 输入条件

已构造的有效中立签名，或生产者允许的有效原生输入。未能加载原生 Type 与已有 Type 上读取失败不是同一场景。

## 分层能力

DSL 消费事实；模型提供不可变 availability；核心保留独立可观察结果；读取/关联由后端决定。不将损坏人工对象默认归为有效输入。

## 阶段失败

读取失败 AN3004；缺少原生关联 AN3001。原生超限抛 AspectNLimitException，外部中立匹配超限 Unknown。取消传播；无效参数可抛参数异常。这些不是统一 Unknown 通道。

## 例子与边界

Service.Run 返回 Int32 且只有 Accessibility 未知：method(public * Service.Run(..)) 与 method(!public * Service.Run(..)) 均 Unknown；method(public string Service.Run(..)) 为 NotMatched。

## 规范依据

[元数据专题](../../../language/metadata-availability.md)提供每种签名的适用事实。

机器条款出处见元数据 norm_refs；以来源登记的批准范围为准。

## 实现与证据

实现与回归入口：[MetadataAvailabilityTests.cs](../../../../tests/AspectN.Tests/Evaluation/MetadataAvailabilityTests.cs)。当前证据按元数据 evidence_ids 定位，未验证范围见每个场景。

## 关联与缺口

试点中立查询和不可变性保留历史证据；E 阶段为具体引用、源码祖先、Reflection 读取失败、Fody 和 detached 特性目标追加精确场景。生命周期另由 CROSS-LIFETIME-001 治理。H1 损坏 Cecil 输入保留未修复处置，不外推真实程序集风险。

[边界示例](../../../usage/examples/MatchingBoundaries/Program.cs)验证已知空特性集合与不可用空占位值、参数位置构造约束，以及选定输入的原生/中立深度超限形态。[反馈记录](../../../reviews/2026-10-13-documentation-feedback.md)登记其范围；这些旧边界示例没有单独证明原宽场景，也没有验证 H1。E 阶段已将 S2–S4 条件明确为实际执行的具体输入，并按新增报告标记；没有保留隐含全源码/PE或任意读取失败的承诺。

范围和剩余输入缺口见[扩展记录](../../../reviews/2026-10-13-documentation-expansion.md)；不变量适用性由非作者按本轮实际入口核验。

类型特性完整快照的优先级已由维护者于2026-10-14裁决；完整已知事实、精确原生回退、无法枚举的缺列表模式分别见 ATTR-TARGETS-001/S11–S13。Roslyn PE 标记依赖缺失的 ReturnAttributes 不可用、独立 RefKind 仍可知见 ATTR-TARGETS-001/S14。直接 HasDirectAttribute API 的原生关联前置条件不因此改变。
