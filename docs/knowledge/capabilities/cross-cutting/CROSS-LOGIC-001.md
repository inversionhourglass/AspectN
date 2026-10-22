# CROSS-LOGIC-001 三态逻辑、诊断及 Boolean 转换

<!-- aspectn-capability:v1
{
  "schema_version": 1,
  "id": "CROSS-LOGIC-001",
  "title": "三态逻辑、诊断及 Boolean 转换",
  "category": "cross-cutting",
  "kind": "cross_cutting",
  "profile": "cross-cutting-v1",
  "norm_refs": [
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
      "section": "5"
    },
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-redesign-design.md",
      "section": "16 公共 API 方向"
    }
  ],
  "scenarios": [
    {
      "id": "S1",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "MatchLogic.And / Or / Not",
      "conditions": "MatchLogicTests 的 T/F/U 完整真值表与带 Unknown 诊断的已知结果",
      "target": "working-tree",
      "expected_behavior": "AND 的 F、OR 的 T 支配；NOT U=U；结果已知时关联诊断仍可保留",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-001",
        "EV-20261021-007",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S2",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "MatchResult.ToBoolean",
      "conditions": "MatchLogicTests 的 False/Throw 策略",
      "target": "working-tree",
      "expected_behavior": "明确 False 策略将 Unknown 转 false；Throw 策略抛 InvalidOperationException",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-001",
        "EV-20261021-007",
        "EV-20261022-201"
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
      "reason": "直接定义三态函数的完整真值表",
      "review_status": "pending",
      "reviewer": null,
      "reviewed_on": null
    },
    {
      "invariant_id": "INVPARITY",
      "applicability": "not_applicable",
      "condition_ref": {
        "path": "docs/knowledge/profiles.json",
        "section": "invariants/INVPARITY"
      },
      "reason": "仅测试核心值函数，未调用后端映射；后端 evaluator 组合由使用者条目治理",
      "review_status": "confirmed",
      "reviewer": "/root/documentation_pilot_review",
      "reviewed_on": "2026-10-12"
    },
    {
      "invariant_id": "INVMISSING",
      "applicability": "not_applicable",
      "condition_ref": {
        "path": "docs/knowledge/profiles.json",
        "section": "invariants/INVMISSING"
      },
      "reason": "值函数接收已形成的 MatchStatus/MatchResult，不读取元数据或原生关联；缺失事实求值另属 CROSS-AVAILABILITY-001",
      "review_status": "confirmed",
      "reviewer": "/root/documentation_pilot_review",
      "reviewed_on": "2026-10-12"
    },
    {
      "invariant_id": "INVCANCEL",
      "applicability": "not_applicable",
      "condition_ref": {
        "path": "docs/knowledge/profiles.json",
        "section": "invariants/INVCANCEL"
      },
      "reason": "这些纯值函数不声明 CancellationToken，不以此豁免完整匹配入口的取消",
      "review_status": "confirmed",
      "reviewer": "/root/documentation_pilot_review",
      "reviewed_on": "2026-10-12"
    }
  ],
  "relations": [],
  "open_findings": [],
  "related_findings": []
}
-->

## 摘要

Unknown 的否定仍未知。确定匹配状态不意味着可以丢掉所有诊断。

## 契约

T=Matched、F=NotMatched、U=Unknown。AND：F 支配；OR：T 支配；NOT U 保持 U。Boolean 转换要求调用方选择 Unknown 策略。

## 输入条件

有效 MatchStatus/MatchResult 值；不要求套用原生程序集字段。

## 分层能力

DSL 组合遵循此核心逻辑；值函数的真值表不能证明全部变量绑定和分支解集。后端映射不在本条目直接证据范围。

## 阶段失败

ToBoolean(Throw) 的 Unknown 抛 InvalidOperationException。三态运算不把 Unknown 自动改成 false。解析/取消/原生映射失败另行治理。

## 例子与边界

T AND U=U；F AND U=F；T OR U=T；F OR U=U；NOT U=U。Combine_UnknownDiagnostic_RemainsOnKnownResult 核验 AND 已知 NotMatched 仍保留 Unknown 诊断。

## 规范依据

[正式规范三态表](../../../superpowers/specs/2026-07-31-aspectn-language-spec.md)是独立预期来源。

机器条款出处见元数据 norm_refs；以来源登记的批准范围为准。

## 实现与证据

实现与回归入口：[MatchLogicTests.cs](../../../../tests/AspectN.Tests/Evaluation/MatchLogicTests.cs)。当前证据按元数据 evidence_ids 定位，未验证范围见每个场景。

## 关联与缺口

不外推到变量绑定、selector 执行顺序、全部诊断去重或调用方取消。

EV-20261012-003 仅补充 NOT U、F AND U 和 Unknown→False 三个观察；未执行 OR、完整真值表、诊断保留或 Throw，不能单独证明 S1/S2 全部行为。完整断言依据 EV-20261012-001；历史报告不改写。

三项 N/A 已由非作者核验；它们仅适用于本条目的纯值函数。[审查记录](../../../reviews/2026-10-12-documentation-pilot.md)保留范围与结果。

2026-10-21 重验 S1/S2，MatchLogic 纯值函数和现有 N/A 适用范围均未改变。Capture 预算中止在整次 Match 边界发生，未完成表达式直接 Unknown/AN3007，不改纯值真值表；运行时回归归 CROSS-RESOURCES-001/S8 和 GEN-BINDING-001/S6。新增预算不把本条目的纯值 N/A 扩展到 DSL 求值器。

2026-10-22资源配置变更按[实施审查](../../../reviews/2026-10-22-configurable-resource-limits.md)重评当前证据。历史报告不改写；新增配置的范围由CROSS-RESOURCES-001/S9—S15区分。本条目S1/S2仍仅覆盖纯值三态与转换，既有N/A范围不变；整次资源中止归资源条目，不扩张本条目的纯值N/A。
