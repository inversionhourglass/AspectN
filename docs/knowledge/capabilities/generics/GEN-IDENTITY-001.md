# GEN-IDENTITY-001 泛型声明身份与闭合查询上下文

<!-- aspectn-capability:v1
{
  "schema_version": 1,
  "id": "GEN-IDENTITY-001",
  "title": "泛型声明身份与闭合查询上下文",
  "category": "generics",
  "kind": "feature",
  "profile": "feature-v1",
  "norm_refs": [
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-redesign-design.md",
      "section": "8.1 / 8.4"
    }
  ],
  "scenarios": [
    {
      "id": "S1",
      "adapter": "cecil",
      "input_mode": "reference",
      "entry_point": "Mapper.MapMethod；AreEqual / IsAssignableTo；CompiledMemberPattern.Match",
      "conditions": "MethodGenericOwnerIdentityTests 中已解析的同名异 assembly 重载、闭合 Owner<string>/Owner<int> 及闭合后同形重载 fixture；不扩展到任意版本",
      "target": "working-tree",
      "expected_behavior": "同一声明形参保留声明身份；闭合类型约束分别按实际上下文求值；不同声明不合并",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261012-002",
        "EV-20261013-003",
        "EV-20261013-009"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S2",
      "adapter": "roslyn",
      "input_mode": "source_symbol",
      "entry_point": "Mapper.MapMethod；同名异 assembly fixture 的 AreEqual / CompiledMemberPattern.Match",
      "conditions": "Roslyn source_symbol：同名异 assembly 重载 fixture 的身份/约束匹配，以及闭合 Owner<string>/Owner<int> 的 OwnerId 对比；未覆盖闭合约束查询或闭合后同形重载",
      "target": "working-tree",
      "expected_behavior": "闭合声明保留 OwnerId；同名异 assembly 参数区分声明，对应 IDisposable/IComparable 约束匹配不串用；不将这些断言外推为闭合约束查询结论",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261012-002",
        "EV-20261013-003",
        "EV-20261013-009"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S3",
      "adapter": "reflection",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapMethod；AreEqual / IsAssignableTo；CompiledMemberPattern.Match",
      "conditions": "MethodGenericOwnerIdentityTests 中已解析的同名异 assembly 重载、闭合 Owner<string>/Owner<int> 及闭合后同形重载 fixture；不扩展到任意版本",
      "target": "working-tree",
      "expected_behavior": "同一声明形参保留声明身份；闭合类型约束分别按实际上下文求值；不同声明不合并",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261012-002",
        "EV-20261013-003",
        "EV-20261013-009"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S4",
      "adapter": "cecil",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapMethod / ITypeSystem.AreEqual",
      "conditions": "结构完整、真实发射且CLR可加载/枚举/调用的两个M<T>(T)声明：返回int/string、约束IDisposable/IComparable；只比较所列原生视图",
      "target": "working-tree",
      "expected_behavior": "不同声明的形参身份应区分，AreEqual应NotMatched；当前OwnerId相同且AreEqual为Matched",
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
      "id": "S5",
      "adapter": "reflection",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapMethod / ITypeSystem.AreEqual",
      "conditions": "结构完整、真实发射且CLR可加载/枚举/调用的两个M<T>(T)声明：返回int/string、约束IDisposable/IComparable；只比较所列原生视图",
      "target": "working-tree",
      "expected_behavior": "不同声明的形参身份应区分，AreEqual应NotMatched；当前OwnerId相同且AreEqual为Matched",
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
      "id": "S6",
      "adapter": "roslyn",
      "input_mode": "pe_symbol",
      "entry_point": "Mapper.MapMethod / ITypeSystem.AreEqual",
      "conditions": "结构完整、真实发射且CLR可加载/枚举/调用的两个M<T>(T)声明：返回int/string、约束IDisposable/IComparable；只比较所列原生视图",
      "target": "working-tree",
      "expected_behavior": "不同声明的形参身份应区分，AreEqual应NotMatched；当前OwnerId相同且AreEqual为Matched",
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
      "id": "S7",
      "adapter": "cecil_fody",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapMethod / ITypeSystem.AreEqual",
      "conditions": "结构完整、真实发射且CLR可加载/枚举/调用的两个M<T>(T)声明：返回int/string、约束IDisposable/IComparable；只比较所列原生视图",
      "target": "working-tree",
      "expected_behavior": "不同声明的形参身份应区分，AreEqual应NotMatched；当前OwnerId相同且AreEqual为Matched",
      "contract_status": "supported",
      "verification_status": "failed",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261013-017"
      ],
      "unverified_notes": "已执行确认与规范预期不同；问题尚未修复，不改写支持承诺。"
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
      "reason": "身份/约束关系与模式查询消费 MatchStatus，不能用纯映射范围豁免三态检查；一般运算依据 CROSS-LOGIC-001，缺失上下文组合尚待取证",
      "review_status": "confirmed",
      "reviewer": "/root/documentation_pilot_review",
      "reviewed_on": "2026-10-12"
    },
    {
      "invariant_id": "INVPARITY",
      "applicability": "applicable",
      "condition_ref": {
        "path": "docs/knowledge/profiles.json",
        "section": "invariants/INVPARITY"
      },
      "reason": "同一编译 fixture 经 Cecil/Roslyn/Reflection 对比声明身份",
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
      "reason": "Cecil 引用可缺失解析定义；当前试点只覆盖已解析 fixture，缺失场景尚需取证",
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
      "reason": "MapMethod 本身无 token，但本条目实际纳入的关系/模式查询入口支持 CancellationToken，因此适用；本轮证据没有执行取消断言",
      "review_status": "confirmed",
      "reviewer": "/root/documentation_pilot_review",
      "reviewed_on": "2026-10-12"
    }
  ],
  "relations": [
    {
      "type": "related_to",
      "capability_id": "CROSS-LOGIC-001"
    }
  ],
  "open_findings": [
    "FND-20261013-003"
  ],
  "related_findings": [
    "FND-20261012-001"
  ]
}
-->

## 摘要

形参身份属于声明，约束查询还需要闭合上下文。相同显示名称不等于相同声明。

## 契约

已批准架构要求保留 original definition 与当前构造上下文。声明身份当前由 owner kind、owner ID、owner assembly 和 position 描述；具体编码是实现细节，不新承诺字符串编码永不变化。解释性泛型指南没有因本次登记自动升级为独立规范。

## 输入条件

已解析方法定义/引用、Roslyn 方法符号或 Reflection MethodInfo。嵌套类型形参的 CLI position 跨包含链展开，方法形参位置从零开始。

## 分层能力

DSL 的 $T 是模式变量，不能当作某个 CLI 声明形参身份。核心保存声明事实，后端分别保留闭合约束查询上下文；Fody 与未执行 Roslyn 模式另待核验。

## 阶段失败

词法/绑定失败属于表达式阶段。原生超限 AN3006 抛 AspectNLimitException；外部中立签名匹配超限返回 Unknown。解析失败时不得伪造新的声明事实。当前声明编码与缺失定义的详细组合尚未验证。

## 例子与边界

`Owner<string>`.`M<V>` 与 `Owner<int>`.`M<V>` 中 V 可属于同一声明，但 where V : T 的有效约束分别消费 string/int。两重载闭合后参数显示相同时仍保留不同声明槽。用户可运行 MethodGenericOwnerIdentityTests；不是按 ToString() 判断相等。

## 规范依据

[泛型实现说明](../../../language/generic-parameter-identity.md)解释 assembly 字段、转发解析与编码。该说明仅提供当前实现背景；来源地位见 sources.json。

机器条款出处见元数据 norm_refs；以来源登记的批准范围为准。

## 实现与证据

实现与回归入口：[MethodGenericOwnerIdentityTests.cs](../../../../tests/AspectN.Conformance.Tests/MethodGenericOwnerIdentityTests.cs)。当前证据按元数据 evidence_ids 定位，未验证范围见每个场景。

S2 的闭合 fixture 只比较 Roslyn OwnerId；闭合上下文的 IsAssignableTo 和模式查询断言只覆盖 Cecil/Reflection。Roslyn 已验证的约束匹配来自同名异 assembly 的另一 fixture，不能混合为闭合场景的证明。

EV-20261012-003 仅补充 Reflection 最小示例中的闭合方法形参身份相等；不单独证明约束查询或重载区分。完整登记范围由 EV-20261012-002 的断言证明，历史报告不改写。

## 关联与缺口

S1–S3的fixture不覆盖缺失解析、全部嵌套/vararg/版本组合或Fody；S4–S7另列CLI重载的已执行范围，不能外推其他组合。原生arity的完整测试另属资源限制盘点。

本轮非作者拒绝了最初的三态/取消 N/A，现改为 applicable；确认的是适用性，不能据此认为缺失事实或取消行为已经验证。[审查记录](../../../reviews/2026-10-12-documentation-pilot.md)保存初始候选与核验结果。

仅返回类型不同的真实CLI重载已在S4–S7执行；[声明身份Finding](../../../reviews/findings/FND-20261013-003.md)记录当前碰撞。未运行Roslyn源码或额外变量/约束串用断言，不据此扩大观察影响。
