# 实现自定义 ITypeSystem

<!-- aspectn-usage:v1
{
  "schema_version": 1,
  "capability_ids": ["TYPE-NOMINAL-001", "ATTR-TARGETS-001", "CROSS-COMPAT-001"],
  "inventory_refs": ["INV-0015", "INV-0011", "INV-0014"]
}
-->

`ITypeSystem` 由后端提供元数据和关系查询。自定义实现应以[公开接口注释](../../src/AspectN/TypeSystem/ITypeSystem.cs)、[语言规范](../superpowers/specs/2026-07-31-aspectn-language-spec.md)和[架构职责](../superpowers/specs/2026-07-31-aspectn-redesign-design.md#85-类型系统服务)为依据。本章集中说明 C06 补全的查询目标契约，不是新增查询比较器 API。

## 严格相等与查询条件

`AreEqual(left, right)` 判断对称的结构相等，可以使用公开的 `StructuralTypeComparer.AreEqual`。空字段及 `TypeKind.Unknown` 在这里仍是身份值，不是通配条件。

`IsAssignableTo(candidate, target)` 的 `target`、`HasDirectAttribute(target, attributeType)` 的 `attributeType` 则是有方向的查询条件。它们不一定是已经映射的完整原生类型，也不要求有原生关联。

例如 `attr(call RequiredAttribute)` 生成的查询，其程序集名和命名空间为空、种类为 Unknown。它要求类型名为 `RequiredAttribute`，并没有要求实际特性位于空命名空间或没有程序集。`attr(call MyApp.RequiredAttribute)` 只增加命名空间约束，仍不限程序集及种类。具体 `MyApp.Base+` 的目标也采用同样表示。

| 查询侧字段 | 未限定值 | 指定值的含义 |
| --- | --- | --- |
| Assembly.Name | 空字符串：整个程序集身份不限 | 按完整 AssemblyIdentity 比较，包括版本、区域性和公钥 token；其余字段的 null/空值不另作通配 |
| Namespace | 空字符串：命名空间不限 | 区分大小写的精确命名空间 |
| TypeKind | Unknown：种类不限 | 精确种类 |
| MetadataName | 不提供额外通配约定 | 区分大小写的精确元数据名 |
| GenericArity | 不提供额外通配约定 | 每层声明元数必须满足 |

这里的 Unknown 表示查询不约束种类，不意味着结果一定 Unknown。空命名空间也不能用来专查全局命名空间。上述放宽只发生在查询侧，不应将候选缺失的事实解释为通配条件。

## 递归结构与关系遍历

对每层包含类型身份、每个命名泛型实参递归应用查询字段规则。仍需检查包含链结构、声明元数、定义/构造形式和对应实参，不能只比较最末名称，也不能把 `Box<int>` 当作所有 `Box<T>`。

泛型实参中的数组保留 rank、Vector/General 和元素结构，不能丢弃这些信息再做比较。数组自身的名义关系仍按既有规则处理，不能把结构查询的形状比较直接当作完整 CLR 可赋值判断；参见[数组专题](../knowledge/capabilities/type-uses/USE-ARRAY-001.md)。

关系查询应先判断候选自身，再遍历已声明的基类、接口或约束；查询目标不需要自行解析成原生对象。遇到不完整关系时保留诊断并继续已知分支，确定匹配可决定 Matched。完整契约仍以公开接口、可用性和资源限制为准。

直接特性查询对每条实际记录的类型使用同样查询规则，不实例化特性，也不应用运行时继承规则。无法取得所需事实时保留 Unknown 和诊断。不要为了支持这些查询而放宽 `AreEqual`，Capture 重复绑定仍依赖严格相等。默认 Declaration 由核心直接严格比较声明槽位身份，不需要新增 ITypeSystem 接口或公开比较器。

映射器继续保存当前类型/方法的实际构造实参、原始声明及形参 owner kind/ID/assembly/position，不能为了默认模式统一返回 OriginalDefinition。核心据这些事实拒绝将闭合或借用形参的构造当作自身声明。Capture 预算约束核心求值和绑定工作，不抢占后端内部执行；自定义后端仍自行响应取消和管理查询成本。

`HasDirectAttribute` 内部映射原生特性类型遇到深度/元数限制时，应在查询边界返回 Unknown，保留 `AspectNLimitException.Diagnostic` 的 AN3005/AN3006。不要加入通用读取失败恢复而改写为 AN3004，也不要捕获取消、参数或生命周期错误。显式 Mapper 调用仍抛限制异常；第三方实现应分别验证“直接查询返回 Unknown”和“显式映射抛异常”这两个入口，参见 [D01 裁决与回归](../reviews/2026-10-21-d01-direct-attribute-limits.md)。

## 接入验收

以下判断都以实际存在的类型/特性及可用元数据为前提：

| 输入 | 预期 |
| --- | --- |
| 查询空程序集/空命名空间/Unknown kind，实际类型名称与结构相同 | 查询匹配 |
| 查询指定正确命名空间，但不限程序集与种类 | 查询匹配 |
| 查询指定完整正确程序集，其他字段不限 | 查询匹配 |
| 查询指定错误程序集、版本、命名空间、种类或名称大小写 | 查询不匹配 |
| 查询泛型元数、实参或包含链不符 | 查询不匹配 |
| `Box<int[,]>` 对 `Box<int[,,]>`，或 Vector 实参与 General 实参 | 查询不匹配 |
| 实际完整身份与部分限定查询交给 AreEqual | 不相等，两个方向一致 |
| 精确特性/关系模式与对应通配模式 | 在同一正例上均匹配 |

[核心目标记录测试](../../tests/AspectN.Tests/TypeSystem/QueryTargetContractTests.cs)只证明核心传参；其中记录型后端不负责实现匹配。[后端契约回归](../../tests/AspectN.Conformance.Tests/QueryTargetContractTests.cs)在真实同源输入上核验 Cecil、Reflection、Roslyn 源码/PE 的现有实现，包含限定字段、递归结构和严格相等的反例。这些测试可作为第三方实现的用例参考，不是已发布的公共测试套件，也不自动证明任意第三方或 Fody 组合。

本次按用户确认保留现有运行逻辑，暂不新增公开查询比较器。第三方需在自己的后端实现查询规则；已有实现若将点查询直接委托给严格比较器，应据此调整。
