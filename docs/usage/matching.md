# 编写匹配表达式

<!-- aspectn-usage:v1
{
  "schema_version": 1,
  "capability_ids": [
    "GEN-IDENTITY-001",
    "USE-REF-001",
    "USE-ARRAY-001",
    "CROSS-AVAILABILITY-001",
    "CROSS-LOGIC-001"
  ],
  "inventory_refs": [
    "INV-0007",
    "INV-0008",
    "INV-0010",
    "INV-0011",
    "INV-0012",
    "INV-0015"
  ]
}
-->

关键字小写且区分大小写。成员表达式使用 CompileMemberExpression，纯类型使用 CompileTypePattern。

| 表达式 | 用途 |
| --- | --- |
| method(public string Example.Service.GetName()) | 普通方法 |
| callable(* Example.Service.*(..)) | 可调用成员，含访问器/运算符等 |
| execution(* Example.Service.*(..)) | 还包括构造器 |
| attr(call Example.MarkerAttribute) | 直接方法目标属性 |
| `System.Collections.Generic.IEnumerable<string>`+ | 声明的类型关系模式 |

选择器/属性/regex 尚未逐能力建档，精确定义依[规范 §6–15](../superpowers/specs/2026-07-31-aspectn-language-spec.md)，不按示例猜测全部适用域。

## 泛型

模式变量要写 $T，例如 method($T `Converter.Convert<$T>`($T))。省略 generic clause 对普通名字要求 arity=0；`Name<?>` 才是不限定 arity，裸 * 有单独规则。模式变量与 CLR 声明形参身份不同。

`Owner<string>`.`M<V>` 与 `Owner<int>`.`M<V>` 中 V 的声明可相同，但有效约束查询上下文不同；同名不同程序集或不同重载不能按显示名称合并。详见[泛型身份](../knowledge/capabilities/generics/GEN-IDENTITY-001.md)。

## ref 和数组

method(* Service.Get(ref int)) 精确要求 Ref。省略注解 int 不约束 ref-kind；!ref int 要求非 by-ref。!ref 必须邻接，! ref 是诊断；返回不能用 out/in。[ref 条目](../knowledge/capabilities/type-uses/USE-REF-001.md)说明裸 Type 为什么不能猜分类。

int[] 同时接受 rank-one Vector 和 General；int[*] 是模型元数据记号，没有增加 DSL 语法。身份和名义关系查询仍区分 Shape；规范 regex 文本也按 rank，不能用于另选 General。[数组条目](../knowledge/capabilities/type-uses/USE-ARRAY-001.md)给方向性反例。

逻辑表达式可用 &&、||、! 和括号。Unknown 的否定仍未知；见[结果说明](results-and-diagnostics.md)。
