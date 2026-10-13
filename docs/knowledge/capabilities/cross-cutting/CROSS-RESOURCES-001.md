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
    }
  ],
  "scenarios": [
    {
      "id": "S1",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "Parse/Compile / CompiledPattern.Match",
      "conditions": "InputDepthTests、SignatureLimitTests、RegexPatternTests 和 MemberPathTests 的实际边界及取消 fixture",
      "target": "working-tree",
      "expected_behavior": "结构深度64含根、类型有效元数64和方法自身元数64；解析/编译给诊断，外部中立匹配超限 Unknown；取消传播",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261013-004"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S2",
      "adapter": "cecil",
      "input_mode": "native_member",
      "entry_point": "Mapper / IsAssignableTo",
      "conditions": "SignatureLimitConformanceTests、ModifierLimitConformanceTests、MappingDepthTests 的实际后端输入和小栈隔离探针",
      "target": "working-tree",
      "expected_behavior": "原生超限 AspectNLimitException 带 AN3005/3006；祖先超限不遮蔽其他已知匹配；不伪装为普通不可用事实",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261013-009"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S3",
      "adapter": "roslyn",
      "input_mode": "source_symbol",
      "entry_point": "Mapper / IsAssignableTo",
      "conditions": "SignatureLimitConformanceTests、ModifierLimitConformanceTests、MappingDepthTests 的实际后端输入和小栈隔离探针",
      "target": "working-tree",
      "expected_behavior": "原生超限 AspectNLimitException 带 AN3005/3006；祖先超限不遮蔽其他已知匹配；不伪装为普通不可用事实",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261013-009"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S4",
      "adapter": "reflection",
      "input_mode": "native_member",
      "entry_point": "Mapper / IsAssignableTo",
      "conditions": "SignatureLimitConformanceTests、ModifierLimitConformanceTests、MappingDepthTests 的实际后端输入和小栈隔离探针",
      "target": "working-tree",
      "expected_behavior": "原生超限 AspectNLimitException 带 AN3005/3006；祖先超限不遮蔽其他已知匹配；不伪装为普通不可用事实",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261013-009"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S5",
      "adapter": "cecil_fody",
      "input_mode": "native_member",
      "entry_point": "Mapper / IsAssignableTo",
      "conditions": "独立 Fody 测试程序集中的 CecilSignatureLimitTests 和取消用例",
      "target": "working-tree",
      "expected_behavior": "相同核心上限在 Fody Cecil 身份下执行；关系取消令牌/解析器取消异常向外传播",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261013-006"
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
  "related_findings": []
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

## 阶段失败

原生映射抛 AspectNLimitException/AN3005或AN3006；中立匹配 Unknown及同码；解析保护AN1002、显式泛型子句超槽AN2011；规范文本预算Unknown/AN3001。取消不转成 Unknown/AN3004。

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
