# CROSS-LIFETIME-001 引擎缓存、后端生命周期与并发隔离

<!-- aspectn-capability:v1
{
  "schema_version": 1,
  "id": "CROSS-LIFETIME-001",
  "title": "引擎缓存、后端生命周期与并发隔离",
  "category": "cross-cutting",
  "kind": "cross_cutting",
  "profile": "cross-cutting-v1",
  "norm_refs": [
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
      "section": "5 / 21"
    },
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-redesign-design.md",
      "section": "12 / 13"
    }
  ],
  "scenarios": [
    {
      "id": "S1",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "AspectNEngine.Compile* / CompiledPattern.Match",
      "conditions": "CompiledPatternCacheTests、CompiledPatternTests 与 PatternVariableTests 的实例LRU、并发候选与诊断输入",
      "target": "working-tree",
      "expected_behavior": "缓存由引擎实例持有，key含编译输入；零容量禁用；匹配绑定/诊断不跨并发候选共享",
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
      "entry_point": "CecilTypeSystem.Dispose / Mapper",
      "conditions": "CecilContextTests、CecilTypeMappingTests.Dispose_CachedTypes 和 CecilTypeSystemTests.Dispose_LeaveOpen",
      "target": "working-tree",
      "expected_behavior": "释放守卫接口/mapper和缓存关联；LeaveOpen保持外部module可用；不把外部对象生存期隐含延长",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261013-005"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S3",
      "adapter": "cecil_fody",
      "input_mode": "native_member",
      "entry_point": "CecilTypeSystem.Dispose / Mapper",
      "conditions": "独立Fody测试程序集的同一生命周期/LeaveOpen输入",
      "target": "working-tree",
      "expected_behavior": "在Fody Cecil程序集身份下验证同一生命周期契约",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261013-006"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S4",
      "adapter": "reflection",
      "input_mode": "native_member",
      "entry_point": "Mapper / Match / collectible AssemblyLoadContext",
      "conditions": "CollectibleAssemblyTests 的已映射和匹配可收集程序集",
      "target": "working-tree",
      "expected_behavior": "弱原生关联不阻止该fixture卸载；已失去关联的开放方法在需要原生能力时返回 Unknown 及缺能力诊断",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261013-008"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S5",
      "adapter": "roslyn",
      "input_mode": "source_symbol",
      "entry_point": "RoslynTypeSystem 与 Compilation 生存期",
      "conditions": "RoslynMappingTests.AdapterLifetime_ReleasingAdapter_ReleasesCompilationGraph",
      "target": "working-tree",
      "expected_behavior": "释放适配器引用后该fixture编译图可被回收",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261013-007"
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
      "capability_id": "CROSS-RESOURCES-001"
    }
  ],
  "open_findings": [],
  "related_findings": []
}
-->

## 摘要

缓存、签名和原生上下文拥有不同生存期；缓存命中不保证跨后端共享，也不保证并发匹配共享可变状态。

## 契约

编译模式可复用，绑定与诊断属于单次求值。引擎实例缓存采用配置容量与LRU，key含源码、入口、正则超时；没有全局静态缓存。Cecil作用域负责模块/解析器生命周期并支持LeaveOpen；Reflection关联保持弱引用；Roslyn适配器持有其Compilation。

## 输入条件

缓存不能把信息较少的引用污染已知定义的事实。跨实例或不同后端的签名在需要原生查询时必须具备对应关联；存活签名本身不等于原生对象仍存活。

## 分层能力

核心缓存编译计划；适配器缓存原生→中立映射及查询关联。原生依赖在单次映射完成前保持存活，结束后不永久锁住可收集图。

## 阶段失败

Cecil释放后调用受守卫入口可抛 ObjectDisposedException；失去原生关联时所需查询按可用性报缺能力。不要把这些情况理解为表达式编译失败。

## 例子与边界

```csharp
using var module = Mono.Cecil.ModuleDefinition.ReadModule(assemblyPath);
using (var system = new AspectN.Cecil.CecilTypeSystem(module,
    new AspectN.Cecil.CecilMappingOptions(leaveOpen: true)))
{
    // 在该作用域中映射并执行需要原生关联的查询。
}
// LeaveOpen 保留调用方持有的 module；system 已被释放。
```

预编译模式可以用于不同候选；后端上下文是否可并发使用不能由模式的并发安全推断。GC/卸载回归证明所选fixture不会被永久关联保留，不承诺即时回收或精确内存数字。

## 规范依据

架构第 12、13 节和公开生命周期接口；核心三态仍按语言第5节。

## 实现与证据

[核心缓存](../../../../tests/AspectN.Tests/Caching/CompiledPatternCacheTests.cs)、[Cecil生命周期](../../../../tests/AspectN.Cecil.Tests/CecilContextTests.cs)、[Reflection可收集程序集](../../../../tests/AspectN.Reflection.Tests/CollectibleAssemblyTests.cs)。

## 关联与缺口

原生关联缺失依 CROSS-AVAILABILITY-001；取消/限制依 CROSS-RESOURCES-001。没有承诺第三方Module/Compilation自身的任意并发行为或固定GC延迟。

S4/S5 的执行证据只覆盖所列卸载/回收断言；没有在单次映射内部触发 GC 来独立验证依赖存活，也没有单独测试仍被持有的 Roslyn 适配器与 Compilation 的存活关系。正文的实现和生命周期要求不据此扩大 verified 范围。
