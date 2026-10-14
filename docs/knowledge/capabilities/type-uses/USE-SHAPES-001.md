# USE-SHAPES-001 可空、元组、任务类与不支持的类型形状

<!-- aspectn-capability:v1
{
  "schema_version": 1,
  "id": "USE-SHAPES-001",
  "title": "可空、元组、任务类与不支持的类型形状",
  "category": "type-uses",
  "kind": "feature",
  "profile": "feature-v1",
  "norm_refs": [
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
      "section": "7 / 11 / 12.6 / 12.7 / 21"
    }
  ],
  "scenarios": [
    {
      "id": "S1",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "CompiledTypePattern.Match",
      "conditions": "NullableTupleTasklikeTests 和 TupleShapeTests 的中立可空、长 ValueTuple、Task/ValueTask、开放定义/不完整实参",
      "target": "working-tree",
      "expected_behavior": "引用 ? 保持身份；值类型 ? 包装 Nullable；元组只接受 ValueTuple 并归一 Rest；tasklike 按返回形状；不完整事实保持 Unknown",
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
      "adapter": "reflection",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapType / MapMethod",
      "conditions": "FunctionPointerMappingTests 与 conformance MetadataConsistencyTests 的普通指针/函数指针表示",
      "target": "working-tree",
      "expected_behavior": "保留 UnsupportedTypeSignature 及规范显示，不把不同未知形状判相等；没有独立结构 DSL",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-005",
        "EV-20261014-006"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S3",
      "adapter": "cecil",
      "input_mode": "native_member",
      "entry_point": "Mapper.MapType / MapMethod",
      "conditions": "CecilFunctionPointerTests、CecilTypeCacheTests 和 CallableMappingTests 的 pinned/sentinel 输入",
      "target": "working-tree",
      "expected_behavior": "指针、函数指针、pinned/sentinel 保留不支持表示；修饰符/规范文本按实际 CLI 保留",
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
      "id": "S4",
      "adapter": "roslyn",
      "input_mode": "source_symbol",
      "entry_point": "Mapper.MapType / MapMethod",
      "conditions": "RoslynMappingTests 的构造元组/可空形状和 conformance 指针显示",
      "target": "working-tree",
      "expected_behavior": "源可空引用身份保持同一 CLR 类型，指针保留规范显示；不承诺保留所有源码注解作为筛选条件",
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
      "entry_point": "ParseTypePattern / ParseMemberExpression → AspectNCompilation.Create",
      "conditions": "BinderTests 的 tasklike<void>/System.Void 成员表达式及 StandardBindingTests 的 void 泛型类型模式",
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
        "reason": "非泛型任务类应写裸 tasklike；void 没有泛型参数，语法不接受这些变体",
        "decision_refs": [
          {
            "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
            "section": "7 / 13.1"
          }
        ],
        "failure_behavior": "绑定拒绝，无有效模式"
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
      "capability_id": "USE-ARRAY-001"
    },
    {
      "type": "depends_on",
      "capability_id": "CROSS-AVAILABILITY-001"
    }
  ],
  "open_findings": [],
  "related_findings": []
}
-->

## 摘要

类型包装、源代码注解和运行时身份分层表达；模型能显示某个类型形状不代表 DSL 可按其结构筛选。

## 契约

引用 `T?` 等同 T，值类型 `T?` 对应 `Nullable<T>`；元组语法至少两项且只匹配 ValueTuple，长元组归一 TRest。裸 tasklike 为非泛型 Task/ValueTask，`tasklike<T>` 为泛型结果，与 IsAsync 无关。自定义 awaitable 不在该形状契约内。

## 输入条件

候选类别或泛型实参不完整时按所需事实求值。开放定义的形式参数不同于缺少部分实参的损坏构造签名。

## 分层能力

适配器映射 nullable/tuple 的 CLR 身份；核心做结构匹配。普通指针、函数指针、pinned/sentinel 属 UnsupportedTypeSignature；custom modifier 在映射分类/规范显示中按已批准目的使用，没有通用的修饰符 DSL 谓词。

## 阶段失败

合法的不支持类型表示在需要结构身份时 Unknown；`tasklike<void>`、void 的泛型子句为编译诊断。不要把 C# `int*` 写法当成 DSL 指针条件，`*` 还可能是名称通配符。

## 例子与边界

```text
string?                     // 匹配 string，不筛选源代码 ? 注解
int?                        // 匹配 Nullable<int>，不匹配普通 int
(int, string)               // 匹配 ValueTuple<int,string>，不匹配 Tuple<int,string>
method(tasklike<int> M())    // 看返回形状，不要求 async 标志
```

`System.Tuple<int,string>` 用普通有名泛型模式明确匹配。指针显示名如 System.Int32* 可用于已承诺的规范文本匹配，不能推出结构化指针支持。

## 规范依据

语言第 7、11、12、21 节；数组形状独立治理，不用 nullable/tuple 测试替代数组身份验证。

## 实现与证据

[类型包装测试](../../../../tests/AspectN.Tests/Evaluation/NullableTupleTasklikeTests.cs)、[指针测试](../../../../tests/AspectN.Reflection.Tests/FunctionPointerMappingTests.cs)、[跨视图显示](../../../../tests/AspectN.Conformance.Tests/MetadataConsistencyTests.cs)。参数位置的 ref 分类见 USE-REF-001。

## 关联与缺口

此条目没有声明完整 custom modifier 选择语法或静态可空性分析；尚未穷举任意 CLI 修饰符链和不同编译器版本。调用约定对 owner/规范文本的保留见对应函数指针测试及后端边界。
