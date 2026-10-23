# 结果与诊断

<!-- aspectn-usage:v1
{
  "schema_version": 1,
  "capability_ids": [
    "SYN-GRAMMAR-001",
    "SEM-REGEX-001",
    "CROSS-AVAILABILITY-001",
    "CROSS-LOGIC-001",
    "USE-REF-001",
    "CROSS-RESOURCES-001"
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

手工创建 Unknown 的 `MatchResult` 或 `TypeRelationResult` 必须提供至少一条诊断，说明无法确定结果的原因。两个 `Unknown(params AspectNDiagnostic[] diagnostics)` 工厂虽允许零参调用通过编译，但零参、空数组和 `null` 诊断集合都会在运行时抛出 `ArgumentException`，参数名为 `diagnostics`。直接用构造器创建 Unknown 时也遵循同一约束；创建 Matched 或 NotMatched 时允许省略诊断。通常直接使用匹配返回的结果及诊断，再选择转换策略。

自定义后端可以将无法取得必要事实的原因传给工厂：

```csharp
var diagnostic = new AspectN.Diagnostics.AspectNDiagnostic(
    "AN3001",
    AspectN.Diagnostics.DiagnosticSeverity.Warning,
    "Required relationship metadata is unavailable.");
var result = AspectN.TypeSystem.TypeRelationResult.Unknown(diagnostic);
```

## 诊断码与处理阶段

诊断码前缀不代表实际检测阶段，不能据此将`AN1xxx`、`AN2xxx`、`AN3xxx`分别当作解析、绑定、匹配阶段的标记。应结合调用入口和返回结果判断阶段，使用完整诊断码识别具体问题。

以当前实现为例，`regex((?=Run))`的环视超出支持子集，扫描正则正文时就会报告`AN2012`；未知选择器在解析时报告`AN2001`；无效正则语法在绑定时报告`AN3002`。这些错误都会阻止取得有效模式。编译调用先检查`Diagnostics`和`TryGet*`，取得模式后再处理匹配结果的三态及诊断，详见[诊断规范第17节](../superpowers/specs/2026-07-31-aspectn-language-spec.md)和[正则专题](../knowledge/capabilities/semantics/SEM-REGEX-001.md)。

## 解析错误的呈现

成员表达式之后多余的连续输入按一个错误区域报告。例如 `method(* Run(..)) a b c d` 产生一条 AN1001，跨度覆盖 `a b c d`，不包含首尾空白；编译仍然失败。中间的空白、换行按原始 UTF-16 跨度包含在这个区域中。

这只合并根表达式之后的尾部恢复提示。`method(` 缺少类型模式、名称模式和括号的不同消息继续保留；尾部的非法字符、未闭合字符串等词法错误也独立保留。尾部扫描仍消费 token 和节点额度，资源耗尽会停止扫描并保留已扫描部分的尾部错误和 AN1003；取消继续抛 OperationCanceledException。该规则不按诊断位置统一去重，详见[语法与恢复场景](../knowledge/capabilities/syntax/SYN-GRAMMAR-001.md)。

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

资源中止是整次求值边界：Capture 专项额度耗尽返回 Unknown/AN3007；总工作、关系、特性或文本额度耗尽返回 Unknown/AN3008，即使先前局部分支已经 Matched；它不继续套用未求完表达式的真值表。诊断保留触发跨度、预算名/限额及已收集的其他诊断。默认 Declaration 不建立动态备选集，也不消耗捕获预算。取消继续抛 OperationCanceledException。

AN3005/AN3006 的结构超限仍为局部分支失败，可继续其他合法祖先；正则超时 AN3003 仍遵循局部三态逻辑。源码、token、语法节点额度超限产生 AN1003 编译错误；深度保护保留 AN1002、泛型条款保留 AN2011。[资源配置](resource-limits.md)列出完整默认值和作用域。

编译诊断先于匹配；原生映射超限抛 AspectNLimitException，中立超限签名匹配按契约返回 Unknown。取消传播。读取失败 AN3004 与无原生关联 AN3001 不混用。regex 非法/超时规则另依规范，当前不从三态值测试推断所有 regex 场景。

依据：[可用性](../knowledge/capabilities/cross-cutting/CROSS-AVAILABILITY-001.md)、[三态](../knowledge/capabilities/cross-cutting/CROSS-LOGIC-001.md)、[诊断规范](../superpowers/specs/2026-07-31-aspectn-language-spec.md)。
