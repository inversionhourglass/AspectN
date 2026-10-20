# TYPE-NOMINAL-001 名称、嵌套、程序集身份与名义关系

<!-- aspectn-capability:v1
{
  "schema_version": 1,
  "id": "TYPE-NOMINAL-001",
  "title": "名称、嵌套、程序集身份与名义关系",
  "category": "types",
  "kind": "feature",
  "profile": "feature-v1",
  "norm_refs": [
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
      "section": "7 / 12.2 / 12.3 / 12.4"
    },
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-redesign-design.md",
      "section": "8.1 / 8.4 / 8.5"
    },
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
      "section": "21"
    },
    {
      "path": "src/AspectN/TypeSystem/ITypeSystem.cs",
      "section": "AreEqual / IsAssignableTo / HasDirectAttribute"
    }
  ],
  "scenarios": [
    {
      "id": "S1",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "CompileTypePattern / CompiledTypePattern.Match / StructuralTypeComparer",
      "conditions": "NamePatternMatcherTests、MemberPathTests 和 AssignableRelationshipTargetTests 的名称/结构/关系 fixture；核心QueryTargetContractTests记录关系查询目标的非限定身份及递归结构",
      "target": "working-tree",
      "expected_behavior": "序数名称匹配；嵌套用 /；+ 仅合法结构目标，已知关系分支可支配未知",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-001",
        "EV-20261021-003"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S2",
      "adapter": "cecil",
      "input_mode": "reference",
      "entry_point": "Mapper.MapType / IsAssignableTo / GetInheritanceClosure",
      "conditions": "CecilResolvedMetadataTests 和 CecilTypeSystemTests 的引用程序集、已解析定义、名义继承和缺失分支；CecilDetachedArrayTests：公共构造且元素非空的游离/未解析元素/正常Int32引用，Vector、非SZ一维和二维数组；固定关系、闭包、五个泛型接口的同/异元素、原Module优先和预先取消",
      "target": "working-tree",
      "expected_behavior": "解析后保留定义身份，缺失引用保留可知结构和未知；不引入 CLR variance；数组自身Module存在时优先使用，否则回退映射上下文；不依赖元素定义解析即可保留8个固定关系，Vector另含5个精确元素泛型接口；General不合成泛型接口，取消传播",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-002",
        "EV-20261021-004"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S3",
      "adapter": "reflection",
      "input_mode": "native_member",
      "entry_point": "Mapper / IsAssignableTo / TypePattern.Match",
      "conditions": "ReflectionMappingTests 的名义继承、通配关系、组合元素/实参 + 与程序集不同形参",
      "target": "working-tree",
      "expected_behavior": "名义查询与完整 CLR 可赋值不同；组合 + 可表达局部类型关系，身份包含程序集",
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
      "entry_point": "Mapper / IsAssignableTo",
      "conditions": "RoslynMappingTests.TypeSystem_AssignabilityAndCapabilities_ReturnExpected；UnavailableAncestorTests 的已知接口/错误基类",
      "target": "working-tree",
      "expected_behavior": "保留已知接口关系；未满足且闭包不完整时 Unknown",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-004"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S5",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "AspectNCompilation.Create",
      "conditions": "BinderTests/LanguageDecisionTests 的数组关系目标、裸通配目标、命名空间泛型子句",
      "target": "working-tree",
      "expected_behavior": "绑定拒绝，无有效模式",
      "contract_status": "excluded",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-001"
      ],
      "unverified_notes": "",
      "exclusion": {
        "reason_code": "explicit_exclusion",
        "reason": "关系目标结构有明确白名单，命名空间不是泛型类型声明",
        "decision_refs": [
          {
            "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
            "section": "3.2 / 12.2 / 12.4"
          }
        ],
        "failure_behavior": "绑定拒绝，无有效模式"
      }
    },
    {
      "id": "S6",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "CompiledTypePattern.Match / CompiledMemberPattern.Match / GetInheritanceClosure",
      "conditions": "InheritanceClosureLimitTests：受控第三方闭包返回公共构造器建立的结构合法中立祖先；实参深度、包含链深度、声明/实参/包含链元数超限；正常祖先两种顺序、后续变量筛选、否定、成员返回槽、原闭包诊断与取消对照",
      "target": "working-tree",
      "expected_behavior": "超限祖先不进入实际求值或绑定，Unknown/AN3005或AN3006与直接输入一致；继续其他祖先并保留未知备选，正常匹配可使最终Matched且限制诊断保留；64边界及具体目标后端路径保持原行为",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261021-002"
      ],
      "unverified_notes": "此场景验证核心消费第三方闭包，不证明内置适配器会生成超限祖先，也不证明循环或无限输入的行为。"
    },
    {
      "id": "S7",
      "adapter": "cecil",
      "input_mode": "native_member",
      "entry_point": "IsAssignableTo / AreEqual / CompiledTypePattern.Match",
      "conditions": "QueryTargetContractTests 的同源真实程序集：非限定字段、指定完整程序集及错误版本/命名空间/种类/名称、泛型元数/实参、数组实参rank/shape与严格相等控制；嵌套包含链和精确/通配关系模式对照",
      "target": "working-tree",
      "expected_behavior": "查询侧空程序集名/空命名空间/Unknown kind分别不限；指定字段及递归结构保留约束；不符合条件NotMatched，正例Matched，AreEqual保持对称严格，诊断为空",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261021-003"
      ],
      "unverified_notes": "仅列出的字段与输入组合；不外推Fody或任意第三方实现。"
    },
    {
      "id": "S8",
      "adapter": "reflection",
      "input_mode": "native_member",
      "entry_point": "IsAssignableTo / AreEqual / CompiledTypePattern.Match",
      "conditions": "QueryTargetContractTests 的同源真实程序集：非限定字段、指定完整程序集及错误版本/命名空间/种类/名称、泛型元数/实参、数组实参rank/shape与严格相等控制；嵌套包含链和精确/通配关系模式对照",
      "target": "working-tree",
      "expected_behavior": "查询侧空程序集名/空命名空间/Unknown kind分别不限；指定字段及递归结构保留约束；不符合条件NotMatched，正例Matched，AreEqual保持对称严格，诊断为空",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261021-003"
      ],
      "unverified_notes": "仅列出的字段与输入组合；不外推Fody或任意第三方实现。"
    },
    {
      "id": "S9",
      "adapter": "roslyn",
      "input_mode": "source_symbol",
      "entry_point": "IsAssignableTo / AreEqual / CompiledTypePattern.Match",
      "conditions": "QueryTargetContractTests 的同源真实程序集：非限定字段、指定完整程序集及错误版本/命名空间/种类/名称、泛型元数/实参、数组实参rank/shape与严格相等控制；嵌套包含链和精确/通配关系模式对照",
      "target": "working-tree",
      "expected_behavior": "查询侧空程序集名/空命名空间/Unknown kind分别不限；指定字段及递归结构保留约束；不符合条件NotMatched，正例Matched，AreEqual保持对称严格，诊断为空",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261021-003"
      ],
      "unverified_notes": "仅列出的字段与输入组合；不外推Fody或任意第三方实现。"
    },
    {
      "id": "S10",
      "adapter": "roslyn",
      "input_mode": "pe_symbol",
      "entry_point": "IsAssignableTo / AreEqual / CompiledTypePattern.Match",
      "conditions": "QueryTargetContractTests 的同源真实程序集：非限定字段、指定完整程序集及错误版本/命名空间/种类/名称、泛型元数/实参、数组实参rank/shape与严格相等控制；嵌套包含链和精确/通配关系模式对照",
      "target": "working-tree",
      "expected_behavior": "查询侧空程序集名/空命名空间/Unknown kind分别不限；指定字段及递归结构保留约束；不符合条件NotMatched，正例Matched，AreEqual保持对称严格，诊断为空",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261021-003"
      ],
      "unverified_notes": "仅列出的字段与输入组合；不外推Fody或任意第三方实现。"
    },
    {
      "id": "S11",
      "adapter": "cecil_fody",
      "input_mode": "reference",
      "entry_point": "Mapper.MapType / IsAssignableTo / GetInheritanceClosure",
      "conditions": "CecilDetachedArrayTests：公共构造且元素非空的游离/未解析元素/正常Int32引用，Vector、非SZ一维和二维数组；固定关系、闭包、五个泛型接口的同/异元素、原Module优先和预先取消",
      "target": "working-tree",
      "expected_behavior": "数组自身Module存在时优先使用，否则回退映射上下文；不依赖元素定义解析即可保留8个固定关系，Vector另含5个精确元素泛型接口；General不合成泛型接口，取消传播",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261021-004"
      ],
      "unverified_notes": "限定当前Cecil身份及公共构造引用；未证明真实PE自然丢失Module，不等于中立签名脱离原生关联。"
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
      "capability_id": "USE-ARRAY-001"
    },
    {
      "type": "depends_on",
      "capability_id": "GEN-IDENTITY-001"
    },
    {
      "type": "depends_on",
      "capability_id": "CROSS-AVAILABILITY-001"
    }
  ],
  "open_findings": [],
  "related_findings": [
    "FND-20261021-002",
    "FND-20261021-003",
    "FND-20261021-004"
  ]
}
-->

## 摘要

类型身份包含程序集、名称与声明结构；名称通配、模式匹配、身份相等和名义继承是不同操作。

## 契约

`*` 在一段中通配，`..` 在命名空间中跨零或多段，`/` 表示嵌套。省略命名空间不限命名空间，但显式单段有名模式不自动跨嵌套。`+` 查询候选自身与名义祖先闭包，允许已声明和运行时合成的关系，不包含数组元素或泛型变体转换。

## 输入条件

解析后的定义身份与尚未解析的引用身份不同。手工中立签名缺原生关联时可做结构判断；需要原生继承/特性数据时不能仅由显示文本恢复。

## 分层能力

核心比较模型结构，适配器提供原生闭包及解析后身份。转发/引用程序集按实际解析结果治理，不承诺任意显示名相同即跨程序集相等。Fody 独立映射身份另见 CROSS-COMPAT-001。

## 阶段失败

非法 + 目标 AN2010；命名空间泛型子句 AN2011。缺少关联/必需祖先可能 Unknown；已知匹配分支不被另一个未知分支掩盖。匹配不是完整运行时 IsAssignableFrom 的替代。

S6 限定核心消费第三方中立闭包：祖先超过结构深度或有效元数上限时为 Unknown/AN3005或AN3006，保留诊断并继续其他祖先；正常匹配仍可支配未知，未确定的绑定备选在后续合取中保留。检查先于实际模式求值，因此即使只看名称或绑定一个实参就能结束，也不能绕过上限。参见 [C05 修复记录](../../../reviews/2026-10-21-c05-inheritance-closure-limits.md)。

## 例子与边界

```text
Company..*Service
Outer/Inner
System.IDisposable+
Base+[]
System.Collections.Generic.IEnumerable<object+>
```

`IEnumerable<object>+` 不表示泛型协变；将 `+` 放在实参中和放在整体类型后有不同约束。`int[]+` 被拒绝，模型数组方向关系使用 ITypeSystem 查询，参见 USE-ARRAY-001。

## 规范依据

语言第 7、12 节；架构第 8.1、8.4、8.5 节。

## 实现与证据

[名义关系测试](../../../../tests/AspectN.Reflection.Tests/ReflectionMappingTests.cs)、[Cecil 解析身份](../../../../tests/AspectN.Cecil.Tests/CecilResolvedMetadataTests.cs)、[未知祖先](../../../../tests/AspectN.Roslyn.Tests/UnavailableAncestorTests.cs)。场景仅覆盖所列输入。

## 关联与缺口

C07 的原生数组 Module 回退及固定关系由 S2 的普通 Cecil 输入与 S11 的 Fody 输入分别验证。元素引用未解析不影响不依赖其定义的数组关系；没有把原生引用无 Module 写成中立签名无关联，也不外推真实 PE 来源。[修复记录](../../../reviews/2026-10-21-c07-detached-cecil-arrays.md)保留边界。

非限定关系查询契约见 `ITypeSystem.IsAssignableTo` 的公开 XML 和[后端实现指南](../../../usage/backend-implementation.md)。S1 的记录型后端只核对核心传参，S7–S10 分别验证四视图的现有查询行为；不将这些查询规则应用到严格 `AreEqual`，也不据此声称任意第三方已验证。

数组形状依 USE-ARRAY-001；泛型声明身份依 GEN-IDENTITY-001；未知闭包依 CROSS-AVAILABILITY-001。尚未证明所有程序集转发拓扑或自定义解析器组合。
