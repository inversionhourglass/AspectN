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
      "section": "9.1 / 12 / 13"
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
      "entry_point": "AspectNEngine.Compile* / CompiledPattern.Match",
      "conditions": "CompiledPatternCacheTests、ResourceContractTests：全部配置按值缓存、LRU、异常不缓存、锁等待取消及并发未命中隔离；ResourceBudgetTests、BindingBudgetTests：并发和后续匹配独立预算；RegexReuseTests：跨规则分配、默认/有限/Unlimited超时持有、同值缓存、共享模式并发及复用后取消",
      "target": "working-tree",
      "expected_behavior": "实例LRU保持；键含源码、ParseKind、RegexTimeout、模式和两个限额；每Match独立声明事实缓存或Capture绑定/共享预算及诊断，不跨并发候选共享",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-001",
        "EV-20261021-007",
        "EV-20261022-201",
        "EV-20261023-305",
        "EV-20261024-001"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S2",
      "adapter": "cecil",
      "input_mode": "native_member",
      "entry_point": "CecilTypeSystem.Dispose / Mapper",
      "conditions": "CecilOwnershipTests 的默认/空选项借用、显式true/false、真实文件延迟方法体与共享模块；CecilContextTests、CecilTypeMappingTests.Dispose_CachedTypes 和 CecilTypeSystemTests.Dispose_LeaveOpen",
      "target": "working-tree",
      "expected_behavior": "默认LeaveOpen=true借用模块，调用方最终释放；显式false接管并释放原生文件资源；两者均释放适配器缓存并守卫入口；保留模块时可延迟读取和写回，且不影响同模块其他适配器",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-002",
        "EV-20261022-101",
        "EV-20261022-201"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S3",
      "adapter": "cecil_fody",
      "input_mode": "native_member",
      "entry_point": "CecilTypeSystem.Dispose / Mapper",
      "conditions": "Fody独立宿主链接CecilOwnershipTests及既有生命周期测试源码；相同的真实文件延迟方法体、默认借用、显式接管、共享模块、缓存回收和释放守卫输入",
      "target": "working-tree",
      "expected_behavior": "Fody Cecil身份下同样默认借用模块，显式false才接管；释放后模块是否保留不影响适配器入口始终失效的契约",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-003",
        "EV-20261022-101",
        "EV-20261022-201"
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
        "EV-20261014-005",
        "EV-20261022-201"
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
        "EV-20261014-004",
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
      "capability_id": "CROSS-AVAILABILITY-001"
    },
    {
      "type": "depends_on",
      "capability_id": "CROSS-RESOURCES-001"
    }
  ],
  "open_findings": [],
  "related_findings": [
    "FND-20261022-101"
  ]
}
-->

## 摘要

缓存、签名和原生上下文拥有不同生存期；缓存命中不保证跨后端共享，也不保证并发匹配共享可变状态。

## 契约

编译模式可复用，绑定与诊断属于单次求值。引擎实例缓存采用配置容量与LRU，key含源码、入口、正则超时；没有全局静态缓存。Cecil 默认 `LeaveOpen=true` 借用传入模块，调用方或宿主负责其最终释放；显式 `false` 才将模块释放责任交给适配器。两种模式下释放适配器均清空缓存并使受守卫入口失效。Reflection关联保持弱引用；Roslyn适配器持有其Compilation。

## 输入条件

缓存不能把信息较少的引用污染已知定义的事实。跨实例或不同后端的签名在需要原生查询时必须具备对应关联；存活签名本身不等于原生对象仍存活。

## 分层能力

核心缓存编译计划；适配器缓存原生→中立映射及查询关联。原生依赖在单次映射完成前保持存活，结束后不永久锁住可收集图。

## 阶段失败

Cecil释放后调用受守卫入口可抛 ObjectDisposedException；失去原生关联时所需查询按可用性报缺能力。不要把这些情况理解为表达式编译失败。

## 例子与边界

```csharp
using var module = Mono.Cecil.ModuleDefinition.ReadModule(assemblyPath);
using (var system = new AspectN.Cecil.CecilTypeSystem(module))
{
    // 在该作用域中映射并执行需要原生关联的查询。
}
// 默认借用并保留调用方持有的 module；system 已被释放。
```

预编译模式可以用于不同候选；后端上下文是否可并发使用不能由模式的并发安全推断。GC/卸载回归证明所选fixture不会被永久关联保留，不承诺即时回收或精确内存数字。

## 规范依据

架构第 9.1、12、13 节和公开生命周期接口；核心三态仍按语言第5节。

## 实现与证据

[核心缓存](../../../../tests/AspectN.Tests/Caching/CompiledPatternCacheTests.cs)、[Cecil生命周期](../../../../tests/AspectN.Cecil.Tests/CecilContextTests.cs)、[Reflection可收集程序集](../../../../tests/AspectN.Reflection.Tests/CollectibleAssemblyTests.cs)。

默认借用与显式接管由 [CecilOwnershipTests](../../../../tests/AspectN.Cecil.Tests/CecilOwnershipTests.cs) 验证真实文件的延迟方法体读取及模块写回，避免仅修改内存模块导致“原生资源其实已关闭”的假阳性。S2/S3 的[当前证据](../../../reviews/evidence/EV-20261022-101.json)各执行 9 项所有权和生命周期回归，包含既有缓存回收及重复释放守卫；使用独立宿主直接编译原测试源码，不表示当前完整测试矩阵通过。

## 关联与缺口

2026-10-24 的[运行时依赖修复](../../../reviews/2026-10-24-package-runtime-dependencies.md)使缓存键的限额数组显式逐项比较，避免扩展方法绑定到构建工具带入的 Span 重载；原值相等、哈希和取消/并发控制保持。S1 以现有全部限额、LRU及并发/取消回归重验；真实核心包消费者额外重复编译成员及类型表达式以触发缓存命中，不承诺整个命中路径零分配。

2026-10-23 RM-20完成普通Cecil缓存的有限性能评估，保留生产实现。缓存先按原生对象引用查找，跨引用再用稳定身份；键、名称和作用域也复用。公开映射仍有守卫和预算，缓存命中不能解释为整次调用零成本。[评估记录](../../../reviews/2026-10-23-rm20-cecil-cache.md)和INV-0020保留短轮次、输入分布及未测存活内存等限制，不改变场景验证或Fody并发/性能承诺。

2026-10-23 RM-19 使编译计划持有语法验证阶段创建的 Regex 及其超时，后续匹配不依赖进程共享的静态正则缓存。S1 新增默认/有限/Unlimited 配置持有、同值缓存复用、并发结果与诊断隔离，以及取消后再次使用的回归；[处置记录](../../../reviews/2026-10-23-rm19-regex-reuse.md)说明存活内存代价。实例及执行缓冲的回收随计划和引擎缓存引用，不承诺即时回收或固定大小。

原生关联缺失依 CROSS-AVAILABILITY-001；取消/限制依 CROSS-RESOURCES-001。没有承诺第三方Module/Compilation自身的任意并发行为或固定GC延迟。

S4/S5 的执行证据只覆盖所列卸载/回收断言；没有在单次映射内部触发 GC 来独立验证依赖存活，也没有单独测试仍被持有的 Roslyn 适配器与 Compilation 的存活关系。正文的实现和生命周期要求不据此扩大 verified 范围。

更换上下文不迁移原生关联，但无需该关联的完整已知 TypeAttributes 可独立消费；优先级及实际 Reflection 原/新上下文对照由 ATTR-TARGETS-001/S11–S13 和已批准可用性条款治理。该规则不保证已释放 Cecil 上下文入口可调用，也不扩张其他原生关系的脱离关联能力。

Declaration 的声明事实缓存、Capture 的环境和预算均随一次 Match 创建；所有 Fork 共享本次预算。编译选项不可变、模式不能在 Match 时切换；不同模式/限额不会串用缓存。第三方后端的线程安全和回收承诺未扩展。

2026-10-22资源配置变更按[实施审查](../../../reviews/2026-10-22-configurable-resource-limits.md)重评当前证据。历史报告不改写；新增配置的范围由CROSS-RESOURCES-001/S9—S15区分。未新增或改动N/A记录；没有从缺少某后端场景推断排除。
