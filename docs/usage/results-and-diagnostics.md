# 结果与诊断

<!-- aspectn-usage:v1
{
  "schema_version": 1,
  "capability_ids": [
    "CROSS-AVAILABILITY-001",
    "CROSS-LOGIC-001",
    "USE-REF-001"
  ],
  "inventory_refs": [
    "INV-0012",
    "INV-0013"
  ]
}
-->

| 状态 | 含义 | 调用方处理 |
| --- | --- | --- |
| Matched | 当前表达式被满足 | 使用匹配结果，并按需要检查诊断 |
| NotMatched | 已知事实足以证明不匹配 | 不把它解释成全部元数据都已读到 |
| Unknown | 所需事实不可用或约定的限制/超时结果 | 检查诊断，选择显式策略 |

MatchResult.ToBoolean(UnknownMatchBehavior.False) 将 Unknown 转 false；Throw 策略会抛 InvalidOperationException。不要把任意异常都吞成 Unknown。

手工创建 Unknown 的 MatchResult 必须提供诊断；空诊断列表会被构造器拒绝。通常直接使用匹配返回的结果及诊断，再选择转换策略。

## 缺失信息如何影响结果

Service.Run 返回 Int32，只有 Accessibility 不可用，其他事实已知：

| 表达式 | 状态 | 原因 |
| --- | --- | --- |
| method(public * Service.Run(..)) | Unknown | 缺 public 所需事实 |
| method(!public * Service.Run(..)) | Unknown | 否定不补足事实 |
| method(public string Service.Run(..)) | NotMatched | 已知返回类型不同决定失败 |

默认手工签名 Complete 表示传入值/空集合已知，Accessibility.Unknown 仍未知。unavailable 标记的空属性列表不证明没有属性。不同字段可能独立可用；占位值不能作事实。裸 by-ref Type 的 ByReference 已知不意味着 RefKind 已知。

## 逻辑与阶段

AND 的 NotMatched 支配，OR 的 Matched 支配，NOT Unknown=Unknown。结果已知时 Unknown 相关诊断仍可能保留。

Capture 的资源中止是整次求值边界：任一绑定预算耗尽立即返回 Unknown/AN3007，即使先前局部分支已经 Matched；它不继续套用未求完表达式的真值表。诊断保留触发跨度、预算名/限额及已收集的其他诊断。默认 Declaration 不建立动态备选集，也不消耗捕获预算。取消继续抛 OperationCanceledException。

编译诊断先于匹配；原生映射超限抛 AspectNLimitException，中立超限签名匹配按契约返回 Unknown。取消传播。读取失败 AN3004 与无原生关联 AN3001 不混用。regex 非法/超时规则另依规范，当前不从三态值测试推断所有 regex 场景。

依据：[可用性](../knowledge/capabilities/cross-cutting/CROSS-AVAILABILITY-001.md)、[三态](../knowledge/capabilities/cross-cutting/CROSS-LOGIC-001.md)、[诊断规范](../superpowers/specs/2026-07-31-aspectn-language-spec.md)。
