# USE-REF-001 ref-kind 与 by-reference 事实

<!-- aspectn-capability:v1
{
  "schema_version": 1,
  "id": "USE-REF-001",
  "title": "ref-kind 与 by-reference 事实",
  "category": "type-uses",
  "kind": "feature",
  "profile": "feature-v1",
  "norm_refs": [
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
      "section": "11"
    },
    {
      "path": "docs/language/metadata-availability.md",
      "section": "引用形状 / Reflection 裸 Type"
    }
  ],
  "scenarios": [
    {
      "id": "S1",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "ModifierMatcher.IsRefKindMatch",
      "conditions": "RefKindTests 中 Int32 参数/返回分类已知；只验证 ref 维度",
      "target": "working-tree",
      "expected_behavior": "省略 ref 不约束该维度；!ref 要求非 by-ref；显式 ref/out/in/ref readonly 精确匹配",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261012-001", "EV-20261013-002"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S2",
      "adapter": "reflection",
      "input_mode": "bare_type",
      "entry_point": "Mapper.MapTypeUse",
      "conditions": "单独 by-ref Type，缺少成员参数/返回上下文",
      "target": "working-tree",
      "expected_behavior": "保留 ByReference，不猜精确 RefKind；消费所需未知事实时按三态求值",
      "contract_status": "supported",
      "verification_status": "unverified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [],
      "unverified_notes": "EV-20261013-001 已验证裸 Type 的 ByReference 已知、RefKind 不可用；尚未验证消费精确分类时的完整三态行为"
    },
    {
      "id": "S3",
      "adapter": "cecil",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapMethod",
      "conditions": "有效原生成员参数/返回；interop flags 不单独证明 readonly",
      "target": "working-tree",
      "expected_behavior": "成员上下文决定精确分类；需要相关 readonly/location 标记",
      "contract_status": "supported",
      "verification_status": "unverified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [],
      "unverified_notes": "执行报告和适用性尚未登记；不外推其他环境或输入"
    },
    {
      "id": "S4",
      "adapter": "roslyn",
      "input_mode": "source_symbol",
      "entry_point": "Mapper.MapMethod",
      "conditions": "有效原生成员参数/返回；interop flags 不单独证明 readonly",
      "target": "working-tree",
      "expected_behavior": "成员上下文决定精确分类；需要相关 readonly/location 标记",
      "contract_status": "supported",
      "verification_status": "unverified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [],
      "unverified_notes": "执行报告和适用性尚未登记；不外推其他环境或输入"
    },
    {
      "id": "S5",
      "adapter": "reflection",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapMethod",
      "conditions": "有效原生成员参数/返回；interop flags 不单独证明 readonly",
      "target": "working-tree",
      "expected_behavior": "成员上下文决定精确分类；需要相关 readonly/location 标记",
      "contract_status": "supported",
      "verification_status": "unverified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [],
      "unverified_notes": "EV-20261013-001 仅补充有效 out/ref 成员和省略/精确/否定注解的示例；readonly/location 标记等完整分类仍未登记验证"
    },
    {
      "id": "S6",
      "adapter": "cecil_fody",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapMethod",
      "conditions": "Fody Cecil 身份的有效成员",
      "target": "working-tree",
      "expected_behavior": "遵循同一 ref 分类契约；本轮没有独立 Fody 证据",
      "contract_status": "supported",
      "verification_status": "unverified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [],
      "unverified_notes": "执行报告和适用性尚未登记；不外推其他环境或输入"
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
      "reason": "未知 ref 分类影响求值，组合遵循三态契约",
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
      "reason": "有效成员分类需比较等信息后端",
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
      "reason": "裸 Type 和未解析引用缺精确分类时不能猜测",
      "review_status": "pending",
      "reviewer": null,
      "reviewed_on": null
    },
    {
      "invariant_id": "INVCANCEL",
      "applicability": "not_applicable",
      "condition_ref": {
        "path": "docs/knowledge/profiles.json",
        "section": "invariants/INVCANCEL"
      },
      "reason": "本条目选定 Mapper.MapMethod/MapTypeUse 及 ref 值约束函数不声明取消；完整匹配入口另属横向取消",
      "review_status": "confirmed",
      "reviewer": "/root/documentation_pilot_review",
      "reviewed_on": "2026-10-12"
    }
  ],
  "relations": [
    {
      "type": "related_to",
      "capability_id": "CROSS-LOGIC-001"
    },
    {
      "type": "depends_on",
      "capability_id": "CROSS-AVAILABILITY-001"
    }
  ],
  "open_findings": [],
  "related_findings": []
}
-->

## 摘要

ByReference 和 RefKind 是两个可独立知道的事实；!ref 与精确 ref/out/in 消费不同信息。

## 契约

未写 ref-kind 不限制该维度；!ref 匹配非 by-ref；显式分类精确匹配。返回位置只允许 ref、ref readonly、!ref，out/in 返回约束无效。

## 输入条件

有效中立 TypeUseSignature 或有效原生成员。Reflection 裸 by-ref Type 和 Cecil 无成员定义的 type-use 保留标记但可能缺精确分类。

## 分层能力

DSL 仅在成员参数/返回槽支持 ref 注解；独立 ParseTypePattern 不接受 ref。模型保存 Availability。核心只消费所需事实。适配器不得从单独 Type 或 interop In flag 推断 readonly。

## 阶段失败

! 与 ref 必须邻接；! ref 或独立类型模式中 ref 是 AN2005 解析诊断。未知精确分类不是语法错误，也不一概 NotMatched；其他已知不匹配仍可决定结果。

## 例子与边界

method(* Service.Get(ref int))：Ref 满足、None 不满足。!ref int 对 Ref 不满足；省略注解 int 对 Ref/Out/In 也可满足。RefKindTests 验证 ref 维度，不能单独证明全部成员匹配。

## 规范依据

[可用性契约](../../../language/metadata-availability.md)描述裸 Type 和 interop flags。

机器条款出处见元数据 norm_refs；以来源登记的批准范围为准。

## 实现与证据

实现与回归入口：[RefKindTests.cs](../../../../tests/AspectN.Tests/Evaluation/RefKindTests.cs)。当前证据按元数据 evidence_ids 定位，未验证范围见每个场景。

## 关联与缺口

核心 ref 维度已定位证据；原生成员、裸 Type 缺事实及 Fody 场景须分别执行验证。

[边界示例](../../../usage/examples/MatchingBoundaries/Program.cs)和[新增证据](../../../reviews/evidence/EV-20261013-001.json)补充裸 Type 的映射事实、有效 out/ref 成员及部分表达式结果。S2 的缺事实三态求值与 S5 的完整 readonly 分类尚未覆盖，两者仍为 unverified。

INVCANCEL 的 N/A 已由非作者按选定入口核验；其余适用性检查仍待核。[审查记录](../../../reviews/2026-10-12-documentation-pilot.md)保留范围与结果。
