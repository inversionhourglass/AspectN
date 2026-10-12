# USE-ARRAY-001 数组形状、rank 模式与名义关系

<!-- aspectn-capability:v1
{
  "schema_version": 1,
  "id": "USE-ARRAY-001",
  "title": "数组形状、rank 模式与名义关系",
  "category": "type-uses",
  "kind": "feature",
  "profile": "feature-v1",
  "norm_refs": [
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
      "section": "12.5"
    },
    {
      "path": "docs/language/array-shape.md",
      "section": "Array shape"
    }
  ],
  "scenarios": [
    {
      "id": "S1",
      "adapter": "cecil",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapType / AreEqual / IsAssignableTo / CompiledTypePattern.Match",
      "conditions": "ArrayShapeModelTests 的同一 IL fixture：Vector/General rank-one、嵌套及泛型参数数组",
      "target": "working-tree",
      "expected_behavior": "身份区分 Vector/General；vector→general 同元素同 rank 可匹配，反向不匹配；DSL int[] 同时接受两形状",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261012-002"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S2",
      "adapter": "roslyn",
      "input_mode": "pe_symbol",
      "entry_point": "Mapper.MapType / AreEqual / IsAssignableTo / CompiledTypePattern.Match",
      "conditions": "ArrayShapeModelTests 的同一 IL fixture：Vector/General rank-one、嵌套及泛型参数数组",
      "target": "working-tree",
      "expected_behavior": "身份区分 Vector/General；vector→general 同元素同 rank 可匹配，反向不匹配；DSL int[] 同时接受两形状",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261012-002"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S3",
      "adapter": "reflection",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapType / AreEqual / IsAssignableTo / CompiledTypePattern.Match",
      "conditions": "ArrayShapeModelTests 的同一 IL fixture：Vector/General rank-one、嵌套及泛型参数数组",
      "target": "working-tree",
      "expected_behavior": "身份区分 Vector/General；vector→general 同元素同 rank 可匹配，反向不匹配；DSL int[] 同时接受两形状",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261012-002"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S4",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "AspectNEngine.CompileTypePattern",
      "conditions": "试图用 int[*] 选择 non-SZ 形状",
      "target": "working-tree",
      "expected_behavior": "标准 DSL 不增加 [*] 形状语法，输入不能成功编译",
      "contract_status": "excluded",
      "verification_status": "unverified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [],
      "unverified_notes": "该排除有正式规范依据，本轮没有单独执行 int[*] 的诊断断言",
      "exclusion": {
        "reason_code": "dsl_not_expressible",
        "reason": "DSL 按 rank 匹配；CLI Shape 独立保存在模型，不增加另一套模式语法或 regex selector",
        "decision_refs": [
          {
            "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
            "section": "12.5"
          },
          {
            "path": "docs/language/array-shape.md",
            "section": "Standard type patterns"
          }
        ],
        "failure_behavior": "编译产生诊断而无有效模式；具体诊断需运行相应解析测试确认"
      }
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
      "reason": "元素身份需要时未知可传播；已知形状/rank 不同可决定不匹配",
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
      "reason": "同一 IL 的数组形状经三后端比较",
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
      "reason": "需要元素身份或原生祖先时存在未知事实；shape 保留不等于所有关系已知",
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
      "reason": "IsAssignableTo 和完整匹配入口有 CancellationToken；本轮形状 fixture 未专门验证取消",
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
  "related_findings": []
}
-->

## 摘要

模型保留 SZ Vector 与 non-SZ General，DSL [] 只约束 rank，不能用 regex 绕过形状限制。

## 契约

身份递归比较 Shape，构造器要求合法 Vector/rank。名义数组关系要求同 rank 和元素身份；允许 Vector→General，反向不允许。此契约没有引入 CLR 数组元素协变或泛型 variance。

## 输入条件

有效映射签名或 detached ArrayTypeSignature。模型 int[*] 是 rank-one General 的元数据记号。

## 分层能力

DSL int[] 接受两种 rank-one 形状；模型可区别它们。Cecil IsVector、Roslyn IsSZArray 和 Reflection runtime SZ 事实参与映射。canonical regex 仍按 rank 渲染，不提供额外形状 selector。

## 阶段失败

非法模型 shape/rank 是参数失败；原生映射超深度与中立匹配超深度区分处理。未知元素在需要时传播，已知形状不同不要求读取全部元素。

## 例子与边界

int[]→int[*] 为 Matched，int[*]→int[] 为 NotMatched；AreEqual 区别两形状。DSL int[] 对二者都 Matched。ArrayShapeModelTests 中 Match_RankBasedArrayPattern_AllAdaptersAcceptBothRankOneShapes 可核验。

## 规范依据

[数组专题](../../../language/array-shape.md)解释通用集合接口只有 Vector 的边界。

机器条款出处见元数据 norm_refs；以来源登记的批准范围为准。

## 实现与证据

实现与回归入口：[ArrayShapeModelTests.cs](../../../../tests/AspectN.Conformance.Tests/ArrayShapeModelTests.cs)。当前证据按元数据 evidence_ids 定位，未验证范围见每个场景。

## 关联与缺口

Fody、Roslyn source 数组及资源/取消专门场景不在本轮 IL fixture 证据内。

不变量适用性初始待核，核验主体须为非作者。
