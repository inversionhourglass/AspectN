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
        "EV-20261014-001"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S2",
      "adapter": "reflection",
      "input_mode": "bare_type",
      "entry_point": "Mapper.MapTypeUse / CompiledMemberPattern.Match",
      "conditions": "ReflectionTypeUseTests：裸 Int32& 参数；将映射结果置入有效中立 Probe 方法，消费省略、!ref、精确四分类与已知类型不匹配",
      "target": "working-tree",
      "expected_behavior": "ByReference 已知且精确 RefKind 不可用；省略注解 Matched，!ref NotMatched，精确分类 Unknown，已知类型失败 NotMatched",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-005"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S3",
      "adapter": "cecil",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapMethod",
      "conditions": "CecilCallableMappingTests 的四类参数与 readonly 返回；InteropRefKindTests 的六个已发射有效 Int32 成员，当前依赖/环境",
      "target": "working-tree",
      "expected_behavior": "成员元数据决定分类；[In] ref 与 [In,Out] ref 不猜 readonly；对应 readonly/location 标记保留参数与返回分类",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-002",
        "EV-20261014-006"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S4",
      "adapter": "roslyn",
      "input_mode": "source_symbol",
      "entry_point": "Mapper.MapMethod",
      "conditions": "RoslynMappingTests 四类参数和 readonly 返回；InteropRefKindTests 的同源有效源码符号",
      "target": "working-tree",
      "expected_behavior": "源码成员上下文保留四类分类；interop 标志不单独证明 readonly",
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
      "id": "S5",
      "adapter": "reflection",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapMethod",
      "conditions": "ReflectionMappingTests.RefKindsAsyncAndSpecialMethods 与 ref 属性；InteropRefKindTests 同一程序集六个有效成员",
      "target": "working-tree",
      "expected_behavior": "运行时成员上下文保留精确参数/返回分类；interop 方向标志不单独改变 readonly 分类",
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
      "id": "S6",
      "adapter": "cecil_fody",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapMethod",
      "conditions": "独立 Fody 测试程序集执行 CecilCallableMappingTests 的四类参数与 readonly 返回；只覆盖该 fixture",
      "target": "working-tree",
      "expected_behavior": "Fody Cecil 身份下参数 Ref/Out/In/RefReadOnly 和 readonly 返回保持；独立方向标志负例见S8",
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
      "id": "S7",
      "adapter": "roslyn",
      "input_mode": "pe_symbol",
      "entry_point": "Mapper.MapMethod / CompiledMemberPattern.Match",
      "conditions": "InteropRefKindTests 的导入 PE 视图：六个 Int32 参数方法；仅此 fixture",
      "target": "working-tree",
      "expected_behavior": "四分类与 interop 负例按实际成员标记保持；对应精确参数模式 Matched",
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
      "adapter": "cecil_fody",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapMethod / CompiledMemberPattern.Match",
      "conditions": "CecilInteropRefKindTests 的真实C#发射Fody PE：[In] ref与[In,Out] ref各六个参数模式；实际原生In/Out/by-ref位先独立断言",
      "target": "working-tree",
      "expected_behavior": "映射RefKind.Ref；int/ref int匹配，out/in/ref readonly/!ref int不匹配，诊断为空；仅这两个fixture",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-003"
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
      "applicability": "applicable",
      "condition_ref": {
        "path": "docs/knowledge/profiles.json",
        "section": "invariants/INVCANCEL"
      },
      "reason": "E 阶段 S2/S7 实际纳入 CompiledMemberPattern.Match，公开入口声明 CancellationToken；原仅 mapper/ref值函数的 N/A 条件不再覆盖整个条目",
      "review_status": "pending",
      "reviewer": null,
      "reviewed_on": null
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

核心 ref 维度、裸 Type 消费和所列原生成员已追加 E 阶段证据。验证范围以各场景条件和报告断言入口为准，不能扩展到所有有效元数据组合。

[边界示例](../../../usage/examples/MatchingBoundaries/Program.cs)和[新增证据](../../../reviews/evidence/EV-20261013-001.json)补充裸 Type 的映射事实、有效 out/ref 成员及部分表达式结果。上述旧报告仅是当时的部分覆盖；E 阶段 S2 的消费断言和 S3–S7 的指定 fixture 已追加新证据。S8 已追加独立Fody方向标志证据；运行身份、12个模式及范围见[本轮审查](../../../reviews/2026-10-13-capability-evidence-and-review.md)。

原 INVCANCEL 的 N/A 与核验范围保存在[历史审查](../../../reviews/2026-10-12-documentation-pilot.md)。E 阶段纳入完整匹配入口后改为 applicable；本轮条件及审查见[扩展记录](../../../reviews/2026-10-13-documentation-expansion.md)。
