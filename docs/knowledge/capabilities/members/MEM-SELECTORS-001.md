# MEM-SELECTORS-001 方法、构造器、属性和访问器选择域

<!-- aspectn-capability:v1
{
  "schema_version": 1,
  "id": "MEM-SELECTORS-001",
  "title": "方法、构造器、属性和访问器选择域",
  "category": "members",
  "kind": "feature",
  "profile": "feature-v1",
  "norm_refs": [
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
      "section": "6 / 7 / 8 / 9 / 10"
    },
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-redesign-design.md",
      "section": "8.3"
    }
  ],
  "scenarios": [
    {
      "id": "S1",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "CompiledMemberPattern.Match",
      "conditions": "CallableSelectorTests、ConstructorSelectorTests、PropertySelectorTests、ModifierMatcherTests 的中立成员",
      "target": "working-tree",
      "expected_behavior": "method 只普通方法；callable 包括非构造器方法类；execution 包括构造器；property/getter/setter 按关联属性，ctor/cctor 域互斥",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-001",
        "EV-20261023-303"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S2",
      "adapter": "cecil",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapMethod / MapProperty",
      "conditions": "CecilCallableMappingTests/CecilAccessorMappingTests 的实际 fixture",
      "target": "working-tree",
      "expected_behavior": "分别保留普通方法、运算符、转换、构造器、属性与事件访问器分类及关联属性",
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
      "adapter": "reflection",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapMethod / MapProperty",
      "conditions": "ReflectionMappingTests 的实际方法、构造器、索引器和事件访问器",
      "target": "working-tree",
      "expected_behavior": "按实际元数据映射方法类与属性；属性选择不是按 setter 的 void 返回值",
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
      "id": "S4",
      "adapter": "roslyn",
      "input_mode": "source_symbol",
      "entry_point": "Mapper.MapMethod / MapProperty",
      "conditions": "RoslynMappingTests 的方法/属性/索引器/事件符号；InstanceOperatorMappingTests 实际操作符",
      "target": "working-tree",
      "expected_behavior": "保留实际声明分类；仅像运算符的名称不是操作符分类证明",
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
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "CompileMemberExpression",
      "conditions": "边界示例中的字段和事件声明表达式",
      "target": "working-tree",
      "expected_behavior": "无有效模式且存在编译诊断",
      "contract_status": "excluded",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-009"
      ],
      "unverified_notes": "",
      "exclusion": {
        "reason_code": "dsl_not_expressible",
        "reason": "选择器集合只承诺所列方法类和属性范围；事件访问器不等于事件声明选择器",
        "decision_refs": [
          {
            "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
            "section": "6"
          }
        ],
        "failure_behavior": "无有效模式且存在编译诊断"
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
      "capability_id": "USE-REF-001"
    },
    {
      "type": "depends_on",
      "capability_id": "TYPE-NOMINAL-001"
    },
    {
      "type": "related_to",
      "capability_id": "CSH-METADATA-001"
    }
  ],
  "open_findings": [],
  "related_findings": []
}
-->

## 摘要

选择器先决定候选适用域，再匹配修饰符、名称、类型和参数；名称相似不能代替 MethodKind。

## 契约

普通方法用 method，非构造器的方法类含属性/事件访问器、运算符及转换用 callable，全部方法类用 execution。property 对关联属性求值并覆盖 getter/setter，属性类型不是 setter 的 CLR void 返回类型。成员可匹配不代表有方法体或可用于某种织入。

## 输入条件

候选为 MethodSignature 或由适配器映射的有效方法类。独立 PropertySignature 和访问器关联有区别；关联不可用不是普通方法“没有关联”。声明的 public 与包含类型有效可见性也不同，匹配使用已承诺元数据事实。

## 分层能力

Cecil/Reflection/Roslyn 建立 MethodKind 与属性关联；核心选择器不猜名称约定。真实实例操作符按语言/CLI 分类，伪造 op_ 名的普通方法仍属普通方法。

## 阶段失败

属性名的泛型子句 AN2011；选择器不适用域确定 NotMatched；访问器关联缺失而被消费时 Unknown。field/event 声明选择器不在语法内。

## 例子与边界

```text
method(public string GetName())
callable(* *(..))
execution(* *(..))
property(string Service.Name)
setter(string Service.Name)
ctor(Service(int))
cctor(Service)
```

setter 示例按属性 string 匹配；不是 `setter(void ...)`。ctor 只实例构造器，cctor 只静态构造器。`callable` 可接受事件 add/remove，不能据此筛选独立 event 声明。

## 规范依据

语言第 6–10 节；属性可用性与模型职责见架构第 8.3 节。

## 实现与证据

[选择器测试](../../../../tests/AspectN.Tests/Evaluation/CallableSelectorTests.cs)、[属性测试](../../../../tests/AspectN.Tests/Evaluation/PropertySelectorTests.cs)、[操作符一致性](../../../../tests/AspectN.Conformance.Tests/InstanceOperatorMappingTests.cs)。后端 PE 特殊边界见 CSH-METADATA-001。

2026-10-23 的 RM-17 将修饰符单元素数组判断改为直接判断，并改用索引遍历。S1 的选择器与修饰符回归由 EV-20261023-303 在当前实现重验；原证据保留历史快照。局部已知事实判断的零分配回归与完整 Match 的性能观察范围不同，详见[处置记录](../../../reviews/2026-10-23-rm17-matching-allocations.md)。不增加语义、公共 API 或整体性能承诺。

## 关联与缺口

ref 条件依 USE-REF-001，名称/嵌套和关系依 TYPE-NOMINAL-001；未添加方法体/可织入的普遍判定能力。生成的局部方法、事件 raiser 等要按具体后端行审查。
