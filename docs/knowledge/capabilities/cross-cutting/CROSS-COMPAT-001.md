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
      "entry_point": "公开 API 反射与程序集引用检查",
      "conditions": "PublicApiTests、ResourceOptionsTests、ResourceContractTests、ResourceApiTests：新三阶段选项、值类型、取消参数、无公开执行上下文、完整缓存字段；旧语言/扩展排除仍由原fixture验证",
      "target": "working-tree",
      "expected_behavior": "核心不暴露后端或内部上下文类型；统一新编译选项签名，旧TimeSpan重载及AspectNLimits移除；AreEqual含取消；默认Declaration不变",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated",
        "manual"
      ],
      "evidence_ids": [
        "EV-20261014-001",
        "EV-20261021-007",
        "EV-20261022-201",
        "EV-20261022-202"
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
        "EV-20261014-003",
        "EV-20261021-007",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S3",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "netstandard2.0 资产的加载及已选原生查询",
      "conditions": "CompatibilityAssetTests/NetFrameworkSmokeTests 在 net10.0 进程；不含Fody兼容资产；NetFrameworkSmokeTests 双模式反射开放/闭合方法与AN3007，实际加载netstandard2.0核心/适配器",
      "target": "working-tree",
      "expected_behavior": "实际加载核心/Cecil/Roslyn/Reflection netstandard2.0 资产，指定反射方法、数组形状和约束查询可执行；所列双模式及预算查询可执行",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-007",
        "EV-20261021-007",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S4",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "netstandard2.0 资产的加载及已选原生查询",
      "conditions": "相同兼容项目实际 net472 进程；Windows已安装.NET Framework运行时；不含Fody兼容资产；NetFrameworkSmokeTests 双模式反射开放/闭合方法与AN3007，实际加载netstandard2.0核心/适配器",
      "target": "working-tree",
      "expected_behavior": "指定公开入口及查询能在该环境执行；不把一次测试等同所有旧运行时和所有表达式支持；所列双模式及预算查询可执行",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-008",
        "EV-20261021-007",
        "EV-20261022-201"
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
        "automated",
        "manual"
      ],
      "evidence_ids": [
        "EV-20261021-003",
        "EV-20261022-201",
        "EV-20261022-202"
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

2026-10-24 的 [O-02 版本策略](../../../reviews/2026-10-24-o02-release-version-policy.md)将五个包的基础版本集中在根 props，正式 tag 必须匹配，prerelease 分支生成一次 preview-时间戳供整次流程复用。对应发布工程盘点 INV-0021；[用户版本说明](../../../usage/lifetimes-and-compatibility.md)与[发布指南](../../../../eng/release-versioning.md)同步。该变更未改变公共 API、目标框架或后端依赖版本，不升级 S1—S5 的场景验证状态、证据或 N/A；整包构建与包元数据核验的实际范围见本轮处置记录，既有消费者缺口单独保留。

S5 补全公开 `ITypeSystem` 的查询侧契约与第三方接入指南。生产源码只更新 XML，未新增公开比较器或改变 API 形状/运行逻辑。记录型后端测试证明核心传参，四视图查询行为证据另见 TYPE-NOMINAL-001/S7–S10、ATTR-TARGETS-001/S17–S20，不外推到任意第三方实现。

本轮Windows证据不代表远端Linux CI已经运行；未新增已发布版本或任意历史依赖版本的证明。Fody的netstandard运行资产、其他宿主版本留待专门矩阵场景。

2026-10-22 的 [RI-02 裁决](../../../reviews/2026-10-22-ri02-fork-ci-triggers.md)将 CI 和 Compatibility 限定为 dev/main 的 push、目标为这两个分支的 PR 及手动运行。fork 的开发分支验证与上游的 PR 验证各自保留；兼容矩阵和所有 job 内容不变。该执行入口调整对应 INV-0014，只取得配置静态校验证据，不扩展 S1—S5 的已验证范围，也不证明当前工作区的产品测试或远端工作流通过。

2026-10-21 选项新增不暴露后端类型，不改变 ITypeSystem。该句为当时的兼容范围；后续资源配置明确移除旧构造器与AspectNLimits，AreEqual增加取消参数，当前入口依新资源规范。默认变量仍Declaration，迁移见[用户说明](../../../usage/migration.md)。Fody 在独立程序集执行共享测试；本轮未新增Fody的netstandard兼容宿主，不外推S3/S4到该组合。S5 的非限定查询契约及无公开比较器决定保持，核心槽位相等不放宽 AreEqual。

2026-10-22 的 [RM-01 文档修订](../../../reviews/2026-10-22-rm01-public-api-xml.md)补全公开 XML 的参数用途、返回状态、现有前置条件和异常；三种后端实现通过 inheritdoc 引用 ITypeSystem 的既有查询契约。对应 S1、S5 的公开说明，没有改动声明、方法体或既有场景状态/N/A；编译成功只验证源码及 XML，不新增兼容宿主或所有后端行为的证据。

2026-10-22资源配置变更按[实施审查](../../../reviews/2026-10-22-configurable-resource-limits.md)重评当前证据。历史报告不改写；新增配置的范围由CROSS-RESOURCES-001/S9—S15区分。未新增或改动N/A记录；没有从缺少某后端场景推断排除。

2026-10-23 的 [RM-22 文档检查](../../../reviews/2026-10-23-rm22-xml-documentation-build-check.md)移除生产库 CS1591 抑制，并补齐 145 个枚举值和 5 个受保护构造器的英文 XML。源码非 XML 行保持一致，API、方法体及 S1—S5 的语义和 N/A 未变；构建检查不升级任何运行时或兼容矩阵的验证状态。公开文档缺失今后由 C# 编译告警视为错误规则阻止构建，内容质量仍依审查流程。
