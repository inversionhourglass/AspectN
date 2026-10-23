# SEM-REGEX-001 正则子集、规范签名与超时

<!-- aspectn-capability:v1
{
  "schema_version": 1,
  "id": "SEM-REGEX-001",
  "title": "正则子集、规范签名与超时",
  "category": "semantics",
  "kind": "feature",
  "profile": "feature-v1",
  "norm_refs": [
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
      "section": "15 / 21"
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
      "entry_point": "CompileMemberExpression / CompiledMemberPattern.Match",
      "conditions": "RegexSubsetTests、RegexParserTests 和 RegexPatternTests 的受限子集、语法、格式化、超时用例；RegexReuseTests：跨规则分配、默认/有限/Unlimited超时持有、同值缓存、共享模式并发及复用后取消",
      "target": "working-tree",
      "expected_behavior": "普通分组/类/量词可编译；特殊分组/反向引用等 AN2012；无效语法 AN3002；超时 Unknown/AN3003",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-001",
        "EV-20261022-201",
        "EV-20261023-305"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S2",
      "adapter": "reflection",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapMethod / regex",
      "conditions": "GenericDefinitionMatchingTests 的开放构造声明类型、重复形参和外部方法形参",
      "target": "working-tree",
      "expected_behavior": "规范文本按声明身份和首次位置统一命名；构造签名显示实际实参，不用源码泛型名替代",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-005",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S3",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "CompileMemberExpression",
      "conditions": "RegexSubsetTests 的注释、内联选项、环视、命名/平衡分组、反向引用与嵌套/相减类输入",
      "target": "working-tree",
      "expected_behavior": "编译期 AN2012；无有效模式",
      "contract_status": "excluded",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-001",
        "EV-20261022-201",
        "EV-20261023-305"
      ],
      "unverified_notes": "",
      "exclusion": {
        "reason_code": "explicit_exclusion",
        "reason": "规范仅保留受限语法以使解析/格式可控；普通量词仍可能回溯，因此限制语法不能取消超时",
        "decision_refs": [
          {
            "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
            "section": "15"
          }
        ],
        "failure_behavior": "编译期 AN2012；无有效模式"
      }
    },
    {
      "id": "S4",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "NamedTypeSignature / MethodSignature 构造；CompiledMemberPattern.Match / CanonicalSignatureFormatter",
      "conditions": "GenericSignatureValidationTests拒绝残缺结构；IncompleteGenericArgumentTests和GenericSignatureConstructionTests覆盖合法定义、完整闭合/开放构造及嵌套类型格式化",
      "target": "working-tree",
      "expected_behavior": "槽位结构非法在构造时抛ArgumentException，不进入regex；合法完整构造保留格式化，缺少可编号声明上下文等类型事实仍为Unknown/AN3001",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261020-002",
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
      "capability_id": "GEN-IDENTITY-001"
    },
    {
      "type": "depends_on",
      "capability_id": "CROSS-RESOURCES-001"
    }
  ],
  "open_findings": [],
  "related_findings": [
    "FND-20261013-002",
    "FND-20261020-002"
  ]
}
-->

## 摘要

regex 在一份规范成员签名上做区域性无关的子串搜索，既不是完整 .NET 正则能力，也不是所有源码事实的可逆编码。

## 契约

普通/非捕获分组、普通字符类、分支、锚点、贪婪/懒惰量词可用；其他 (?...) 组、反向引用、嵌套/相减字符类等按规范排除。空白和 # 始终字面。完整签名匹配需写 ^...$。

## 输入条件

候选必须提供生成规范签名所需身份和槽位。泛型形参按位置统一编号，唯一形参为 T，其他为 T1..TN。全局具体类型名恰好为保留编号名时，文本无法区分，改用结构选择器。

## 分层能力

Parser 保留正则原文并识别外层闭合；Binder 检查受限子集并调用 .NET 语法验证，创建带编译配置超时的 Regex 实例随计划保留；Formatter 生成规范签名；Evaluator 复用实例，默认执行单次250ms有限超时的匹配，允许显式 TimeLimit.Unlimited。源 async 标记不包含在该文本内。

## 阶段失败

不支持结构 AN2012、无效正则 AN3002 都是编译失败；超时为 Unknown/AN3003。真正无法形成规范文本为 Unknown/AN3001；规范文本默认16,384个UTF-16代码单元，额度耗尽为整次Unknown/AN3008，支持配置有限值或显式Unlimited，仍受总工作约束。格式化阶段遵守匹配取消，正则超时从格式化结束后计时。

## 例子与边界

```text
regex(GetName)          // 子串搜索
regex(^public .*GetName.*$)
regex((?=GetName))      // 环视被排除
```

转义的 `\(\?x\)` 是字面文本，可用于匹配 `(?x)`，不启用 x 选项。用 `method(...) && attr(...)` 表达结构条件，不能靠 regex 恢复未编码的可空源码注解或数组 Shape。

## 规范依据

语言第 15、21 节。

## 实现与证据

[子集编译](../../../../tests/AspectN.Tests/Binding/RegexSubsetTests.cs)、[规范与超时](../../../../tests/AspectN.Tests/Evaluation/RegexPatternTests.cs)、[开放泛型编号](../../../../tests/AspectN.Reflection.Tests/GenericDefinitionMatchingTests.cs)。超时示例由确定性回归治理，不鼓励运行高成本正则演示。

## 关联与缺口

2026-10-23 RM-19 的实例复用与取舍见[处置记录](../../../reviews/2026-10-23-rm19-regex-reuse.md)。S1/S3 语义、错误与超时随完整矩阵重验，新增配置持有、缓存复用、并发和取消回归；性能数据不替代语义证据。实例及其可复用缓冲随计划引用存活，不承诺固定内存大小；尚未测量复杂回溯和并发吞吐。

泛型身份依 GEN-IDENTITY-001；限制/取消依 CROSS-RESOURCES-001。不承诺文本能区分所有 CLR 身份，更不承诺测试过所有任意正则输入。

S4 当前遵循[构造校验裁决](../../../reviews/2026-10-20-c02-generic-signature-validation.md)：元数或槽位矛盾在构造时拒绝，不再送入 regex 求 Unknown。[旧 Finding](../../../reviews/findings/FND-20261013-002.md)保留当时观察与预期，但相应输入契约已被本次裁决取代。

合法定义、完整闭合/开放构造及嵌套类型的当前对照见 [IncompleteGenericArgumentTests](../../../../tests/AspectN.Tests/Evaluation/IncompleteGenericArgumentTests.cs)和[真实后端回归](../../../../tests/AspectN.Conformance.Tests/GenericSignatureConstructionTests.cs)。无法提供可编号声明上下文、遇到不支持类型形状等事实缺口仍遵守既有 Unknown 规则；不是任何完整槽位都能保证规范文本可用。

2026-10-22资源配置变更按[实施审查](../../../reviews/2026-10-22-configurable-resource-limits.md)重评当前证据。历史报告不改写；新增配置的范围由CROSS-RESOURCES-001/S9—S15区分。未新增或改动N/A记录；没有从缺少某后端场景推断排除。
