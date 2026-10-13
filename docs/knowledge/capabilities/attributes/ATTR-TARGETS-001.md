# ATTR-TARGETS-001 直接特性目标、真实记录与伪特性

<!-- aspectn-capability:v1
{
  "schema_version": 1,
  "id": "ATTR-TARGETS-001",
  "title": "直接特性目标、真实记录与伪特性",
  "category": "attributes",
  "kind": "feature",
  "profile": "feature-v1",
  "norm_refs": [
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
      "section": "14"
    }
  ],
  "scenarios": [
    {
      "id": "S1",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "CompiledMemberPattern.Match / HasDirectAttribute",
      "conditions": "AttributePatternTests 的 type/call/prop/para/ret/exec/any 与泛型特性中立输入",
      "target": "working-tree",
      "expected_behavior": "只查询直接目标；call 不包含属性位置，exec 对访问器包含关联属性；不可用位置不当作空",
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
      "entry_point": "Mapper / attr / HasDirectAttribute",
      "conditions": "AttributeMetadataConformanceTests、CustomAttributeRecordTests、MetadataConsistencyTests 的同源/PE fixture；不含 Fody",
      "target": "working-tree",
      "expected_behavior": "真实 CustomAttribute 记录跨视图保留；编码标志形成的伪特性排除，真实同类型记录仍保留；查询不实例化对象",
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
      "entry_point": "Mapper / attr / HasDirectAttribute",
      "conditions": "AttributeMetadataConformanceTests、CustomAttributeRecordTests、MetadataConsistencyTests 的同源/PE fixture；不含 Fody",
      "target": "working-tree",
      "expected_behavior": "真实 CustomAttribute 记录跨视图保留；编码标志形成的伪特性排除，真实同类型记录仍保留；查询不实例化对象",
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
      "entry_point": "Mapper / attr / HasDirectAttribute",
      "conditions": "AttributeMetadataConformanceTests、CustomAttributeRecordTests、MetadataConsistencyTests 的同源/PE fixture；不含 Fody",
      "target": "working-tree",
      "expected_behavior": "真实 CustomAttribute 记录跨视图保留；编码标志形成的伪特性排除，真实同类型记录仍保留；查询不实例化对象",
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
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "CompileMemberExpression",
      "conditions": "AttributeExclusionTests：get/set及callable/property/parameter/return旧位置名；参数含括号/空格、索引/通配写法，并执行支持语法对照",
      "target": "working-tree",
      "expected_behavior": "get/set编译失败且有AN1001；旧位置名有AN4005、不能取得有效模式；短名及getter/setter与attr(call ...)组合可编译",
      "contract_status": "excluded",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261013-013"
      ],
      "unverified_notes": "",
      "exclusion": {
        "reason_code": "explicit_exclusion",
        "reason": "Standard位置短名已固定；访问器自身用getter/setter选择器与call位置组合，旧长名只提供迁移诊断",
        "decision_refs": [
          {
            "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
            "section": "14"
          }
        ],
        "failure_behavior": "get/set编译失败且有AN1001；旧位置名有AN4005、不能取得有效模式；短名及getter/setter与attr(call ...)组合可编译"
      }
    },
    {
      "id": "S6",
      "adapter": "cecil",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapMethod / attr(type/call ...)",
      "conditions": "AttributeInheritanceTests：同源/同PE的带标记基类、仅继承的override、显式直接标记的override；原生inherit:true均能看到标记，inherit:false独立证明直接记录",
      "target": "working-tree",
      "expected_behavior": "只继承且无直接记录时NotMatched；基类和显式标记派生类Matched，诊断为空；不把Inherited=true当作直接记录",
      "contract_status": "excluded",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261013-014"
      ],
      "unverified_notes": "",
      "exclusion": {
        "reason_code": "explicit_exclusion",
        "reason": "语言第14节只承诺直接CustomAttribute记录，不执行运行时特性继承规则；继承标志不改变直接记录位置",
        "decision_refs": [
          {
            "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
            "section": "14"
          }
        ],
        "failure_behavior": "表达式可编译；仅继承的声明类型/override方法在此fixture为NotMatched，不是编译失败或Unknown"
      }
    },
    {
      "id": "S7",
      "adapter": "roslyn",
      "input_mode": "source_symbol",
      "entry_point": "Mapper.MapMethod / attr(type/call ...)",
      "conditions": "AttributeInheritanceTests：同源/同PE的带标记基类、仅继承的override、显式直接标记的override；原生inherit:true均能看到标记，inherit:false独立证明直接记录",
      "target": "working-tree",
      "expected_behavior": "只继承且无直接记录时NotMatched；基类和显式标记派生类Matched，诊断为空；不把Inherited=true当作直接记录",
      "contract_status": "excluded",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261013-014"
      ],
      "unverified_notes": "",
      "exclusion": {
        "reason_code": "explicit_exclusion",
        "reason": "语言第14节只承诺直接CustomAttribute记录，不执行运行时特性继承规则；继承标志不改变直接记录位置",
        "decision_refs": [
          {
            "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
            "section": "14"
          }
        ],
        "failure_behavior": "表达式可编译；仅继承的声明类型/override方法在此fixture为NotMatched，不是编译失败或Unknown"
      }
    },
    {
      "id": "S8",
      "adapter": "reflection",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapMethod / attr(type/call ...)",
      "conditions": "AttributeInheritanceTests：同源/同PE的带标记基类、仅继承的override、显式直接标记的override；原生inherit:true均能看到标记，inherit:false独立证明直接记录",
      "target": "working-tree",
      "expected_behavior": "只继承且无直接记录时NotMatched；基类和显式标记派生类Matched，诊断为空；不把Inherited=true当作直接记录",
      "contract_status": "excluded",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261013-014"
      ],
      "unverified_notes": "",
      "exclusion": {
        "reason_code": "explicit_exclusion",
        "reason": "语言第14节只承诺直接CustomAttribute记录，不执行运行时特性继承规则；继承标志不改变直接记录位置",
        "decision_refs": [
          {
            "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
            "section": "14"
          }
        ],
        "failure_behavior": "表达式可编译；仅继承的声明类型/override方法在此fixture为NotMatched，不是编译失败或Unknown"
      }
    },
    {
      "id": "S9",
      "adapter": "roslyn",
      "input_mode": "pe_symbol",
      "entry_point": "Mapper.MapMethod / attr(type/call ...)",
      "conditions": "AttributeInheritanceTests：同源/同PE的带标记基类、仅继承的override、显式直接标记的override；原生inherit:true均能看到标记，inherit:false独立证明直接记录",
      "target": "working-tree",
      "expected_behavior": "只继承且无直接记录时NotMatched；基类和显式标记派生类Matched，诊断为空；不把Inherited=true当作直接记录",
      "contract_status": "excluded",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261013-014"
      ],
      "unverified_notes": "",
      "exclusion": {
        "reason_code": "explicit_exclusion",
        "reason": "语言第14节只承诺直接CustomAttribute记录，不执行运行时特性继承规则；继承标志不改变直接记录位置",
        "decision_refs": [
          {
            "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
            "section": "14"
          }
        ],
        "failure_behavior": "表达式可编译；仅继承的声明类型/override方法在此fixture为NotMatched，不是编译失败或Unknown"
      }
    },
    {
      "id": "S10",
      "adapter": "roslyn",
      "input_mode": "pe_symbol",
      "entry_point": "Mapper.MapMethod / ReturnAttributes / AreEqual",
      "conditions": "只读返回有效PE；无错误的ShadowReader编译声明同名IsReadOnlyAttribute；与原Reflection/Cecil及普通Roslyn PE记录对照",
      "target": "working-tree",
      "expected_behavior": "应保留PE原CA类型的程序集身份；当前错误映射到ShadowReader，与实际标记类型AreEqual为NotMatched",
      "contract_status": "supported",
      "verification_status": "failed",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261013-017"
      ],
      "unverified_notes": "已执行确认与规范预期不同；问题尚未修复，不改写支持承诺。"
    },
    {
      "id": "S11",
      "adapter": "reflection",
      "input_mode": "detached",
      "entry_point": "CompiledMemberPattern.Match / attr(type ...)",
      "conditions": "同一已映射候选TypeAttributes已知；原上下文与新ReflectionTypeSystem；分别精确、通配、复合表达式，带标记/已知空两种候选",
      "target": "working-tree",
      "expected_behavior": "实际新上下文精确表达式Unknown/AN3001，通配/复合消费嵌入事实得确定结果；是否应统一需维护者裁决",
      "contract_status": "pending",
      "verification_status": "unverified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261013-019"
      ],
      "unverified_notes": "已有实际观察，但缺少批准的语义裁决，尚不能登记为符合契约或产品违约。",
      "pending_reason": "批准契约同时涉及直接查询所需原生关联与独立已知事实；这两条在类型特性精确快速路径中的优先级未明确，不自行新增容错承诺。"
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
      "capability_id": "GEN-BINDING-001"
    },
    {
      "type": "depends_on",
      "capability_id": "MEM-SELECTORS-001"
    }
  ],
  "open_findings": [
    "FND-20261013-004",
    "FND-20261013-005"
  ],
  "related_findings": []
}
-->

## 摘要

attr 筛选直接 CustomAttribute 元数据记录，特性写法、元数据标志、运行时伪特性和实例化对象不能混为一谈。

## 契约

type 为声明类型；call 为当前方法；prop 为关联属性；para[n]/para n 为指定零起参数；para[*]/para * 为任意参数；ret 为返回槽；exec 对访问器额外包含属性；any/* 为支持的直接位置。不继承特性，不实例化特性。

## 输入条件

空但已知的列表证明没有记录；不可用空列表不能证明没有记录。泛型特性按类型实参/模式变量匹配。参数下标与声明顺序一致，索引约束不自动重排。

## 分层能力

适配器过滤 flags/table 编码的伪特性，保留真实 PE CustomAttribute，包括与伪特性同类型的真实记录。Readonly 标记可用于分类并同时保留实际记录。数组声明类型不继承元素类型的特性。

## 阶段失败

未知属性关联或目标记录查询在需要时 Unknown；普通方法没有 prop 位置则 NotMatched。非法参数索引和旧长位置名是编译问题；不将错误和未知统一为 false。

## 例子与边界

```text
attr(call MarkerAttribute)
attr(para[0] MarkerAttribute)
attr(exec MarkerAttribute)
getter(string Name) && attr(call MarkerAttribute)
```

`[Serializable]`、`[DllImport]`、`[In]` 等写法可能被编码为 flags/其他表，不是独立真实 CA 记录；不能只凭源码有方括号期待 attr 命中。但 PE 真正写入的同类型 CA 仍可匹配。特性构造器抛异常不应影响纯元数据查询。

## 规范依据

语言第 14 节及可用性专题。排除理由不是“某次反射没读到属性”，而是正式直接记录契约。

## 实现与证据

[核心特性位置](../../../../tests/AspectN.Tests/Evaluation/AttributePatternTests.cs)、[真实记录一致性](../../../../tests/AspectN.Conformance.Tests/CustomAttributeRecordTests.cs)、[不实例化查询](../../../../tests/AspectN.Reflection.Tests/ReflectionMappingTests.cs)。S5的核心语法排除及S6–S9的特性继承排除已分别追加独立执行证据。新出现的PE记录身份问题和detached精确查询差异见S10/S11及关联Finding。

## 关联与缺口

CROSS-AVAILABILITY-001 治理缺失事实，GEN-BINDING-001 治理泛型特性的绑定备选，MEM-SELECTORS-001 治理访问器域。Fody 的具体真实记录一致性未由三后端测试自动推出。
