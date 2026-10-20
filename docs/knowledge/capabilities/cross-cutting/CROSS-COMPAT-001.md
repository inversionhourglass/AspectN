# CROSS-COMPAT-001 公开 API、程序集身份与兼容资产

<!-- aspectn-capability:v1
{
  "schema_version": 1,
  "id": "CROSS-COMPAT-001",
  "title": "公开 API、程序集身份与兼容资产",
  "category": "cross-cutting",
  "kind": "cross_cutting",
  "profile": "cross-cutting-v1",
  "norm_refs": [
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
      "section": "2 / 16 / 18"
    },
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-redesign-design.md",
      "section": "5 / 14.2 / 16"
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
      "entry_point": "公开 API 反射与程序集引用检查",
      "conditions": "PublicApiTests、SyntaxTreeTests、SignatureTests 和 ClosedLanguageTests 的公开表面",
      "target": "working-tree",
      "expected_behavior": "核心不暴露后端类型、不引用适配器程序集；没有语言选择器或自定义扩展参数",
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
      "adapter": "cecil_fody",
      "input_mode": "native_member",
      "entry_point": "CecilTypeSystem / Mono.Cecil 程序集标识",
      "conditions": "FodyCecilIdentityTests 的适配器引用、实际ModuleDefinition程序集与MapType",
      "target": "working-tree",
      "expected_behavior": "Fody Cecil公钥token为1ca091877d12ca03；与普通Cecil不同身份；不能同时引用两种适配器",
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
      "id": "S3",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "netstandard2.0 资产的加载及已选原生查询",
      "conditions": "CompatibilityAssetTests/NetFrameworkSmokeTests 在 net10.0 进程；不含Fody兼容资产",
      "target": "working-tree",
      "expected_behavior": "实际加载核心/Cecil/Roslyn/Reflection netstandard2.0 资产，指定反射方法、数组形状和约束查询可执行",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-007"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S4",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "netstandard2.0 资产的加载及已选原生查询",
      "conditions": "相同兼容项目实际 net472 进程；Windows已安装.NET Framework运行时；不含Fody兼容资产",
      "target": "working-tree",
      "expected_behavior": "指定公开入口及查询能在该环境执行；不把一次测试等同所有旧运行时和所有表达式支持",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-008"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S5",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "ITypeSystem XML API / CompiledPattern.Match",
      "conditions": "核心QueryTargetContractTests的记录型第三方后端；公开XML编译及后端实现指南复核；不新增公开比较器",
      "target": "working-tree",
      "expected_behavior": "具体关系/精确特性目标按公开契约传递空程序集和Unknown kind，省略命名空间不限；递归结构保留，AreEqual仍严格；生产源码仅补XML注释",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261021-003"
      ],
      "unverified_notes": "记录型后端仅证明核心传参，不验证任意第三方匹配实现；文档一致性另经独立复核。"
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
      "capability_id": "CROSS-LIFETIME-001"
    },
    {
      "type": "depends_on",
      "capability_id": "CROSS-AVAILABILITY-001"
    }
  ],
  "open_findings": [],
  "related_findings": [
    "FND-20261021-003"
  ]
}
-->

## 摘要

库目标框架、测试宿主、原生程序集身份与发布包版本分别记录；一个通过的宿主不能代表整个兼容矩阵。

## 契约

当前库提供 netstandard2.0/net10.0；正式.NET Framework使用下限为4.7.2。Cecil与Cecil.Fody暴露同一适配器API但针对不同强名称程序集，不能同时引用；FodyHelpers使用者选Cecil.Fody。核心公开API保持后端无关及单一Standard语言。

## 输入条件

矩阵目标为当前工作树，具体环境和NuGet依赖记录在报告。net472测试跑在实际安装的兼容Framework运行时上，不意味着安装并逐个测试了每个历史CLR版本。

## 分层能力

核心/适配器分程序集，Shared源码编译到各后端的AdapterInternals，不是另一个公开核心程序集。现代宿主特意加载netstandard资产，避免把net10资产通过误记为兼容资产通过。

## 阶段失败

程序集身份混用或缺依赖属于调用方加载/引用配置问题；不保证会变成MatchStatus.Unknown。发布版本落后工作树时，必须按对应release来源重新判断，不能沿用当前文档。

## 例子与边界

```xml
<!-- 普通程序集分析：选择 AspectN.Cecil。 -->
<PackageReference Include="AspectN.Cecil" Version="目标发布版本" />
<!-- FodyHelpers 项目：改选 AspectN.Cecil.Fody，不能并列引用。 -->
```

使用库目标是netstandard2.0；运行测试宿主可能是net10.0或net472。SDK可编译net472引用程序集，不等于该机器能运行.NET Framework测试。

## 规范依据

架构第5、14.2、16节；单一语言与扩展边界依语言第2、16节。

## 实现与证据

[兼容资产测试](../../../../tests/AspectN.NetFramework.Tests/CompatibilityAssetTests.cs)、[Fody身份测试](../../../../tests/AspectN.Cecil.Fody.Tests/FodyCecilIdentityTests.cs)、[核心公开API](../../../../tests/AspectN.Tests/PublicApiTests.cs)。

## 关联与缺口

S5 补全公开 `ITypeSystem` 的查询侧契约与第三方接入指南。生产源码只更新 XML，未新增公开比较器或改变 API 形状/运行逻辑。记录型后端测试证明核心传参，四视图查询行为证据另见 TYPE-NOMINAL-001/S7–S10、ATTR-TARGETS-001/S17–S20，不外推到任意第三方实现。

本轮Windows证据不代表远端Linux CI已经运行；未新增已发布版本或任意历史依赖版本的证明。Fody的netstandard运行资产、其他宿主版本留待专门矩阵场景。
