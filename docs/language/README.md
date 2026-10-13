# AspectN 语言指南

AspectN 表达式筛选 .NET 类型和可调用成员，语言及编译后的模式 API 不暴露具体元数据后端。

与 Shared 的已确认差异、示例及原因见[设计差异说明](shared-standard-differences.md)。

## 选择器

- `method(...)` 选择普通方法。
- `callable(...)` 还覆盖属性及事件访问器、运算符和类型转换。
- `execution(...)` 覆盖 callable 的全部成员，以及实例和静态构造器。
- `property(...)`、`getter(...)`、`setter(...)` 选择属性访问器。
- `ctor(...)`、`cctor(...)` 分别选择实例和静态构造器。
- `attr(target Type)` 检查指定位置的直接特性：类型、可调用成员（`call`）、属性（`prop`）、返回值（`ret`）、参数（`para[n]`）、执行族（`exec`）或任意位置（`any`、`*`）。参数位置同时接受方括号写法（`para[0]`、`para[*]`）和空格写法（`para 0`、`para *`）。

选择器可用 `&&`、`||`、`!` 和括号组合。声明类型与成员名也可在路径内部组合，例如 `method(* (A || B).(Run || Stop)(..))`。路径前置 `!` 同时否定声明类型、名称和方法泛型约束。连续的 `!` 被拒绝。属性名和命名空间段不能有泛型子句。

```text
method(tasklike<string> Service.Load(..))
method($T Converter.Convert<$T>($T))
getter(string Example.Model.Name) && attr(prop Example.MarkerAttribute)
ctor(Example.Service(string, ref int))
```

## 类型模式

类型模式支持限定及非限定名称、嵌套类型、泛型定义与构造类型、数组、可空值/引用类型语法、元组、通配符，以及用 `+` 表示的类型关系。

```text
System.Collections.Generic.IEnumerable<string>+
Example.Outer<string>/Inner<int,double>
(string, int)
int[,][]
```

## 结果与缺失元数据

`regex(...)` 在规范成员签名中搜索，使用受限正则子集：字符转义/字符类、普通及非捕获分组、分支、锚点和贪婪/懒惰量词。注释、内联选项、环视、反向引用、其他特殊分组，以及嵌套/相减字符类被拒绝并报告 AN2012。在字符类内使用字面方括号时必须转义。

无效的正则语法产生编译期 AN3002。正则匹配设有限超时；超时产生 `Unknown` 和 AN3003。完整规则见规范第 15 节。

匹配采用三态：

- `Matched`：候选满足表达式。
- `NotMatched`：可用元数据证明候选不满足表达式。
- `Unknown`：缺失后端能力或引用定义。

除非应用程序明确采用该策略，否则不要默默将 `Unknown` 当作 `NotMatched`。结果诊断解释所缺事实。

## 单一语言契约

现有 Standard 语法是 AspectN 唯一语言。关键字小写且区分大小写；模式变量必须带 `$`；未写泛型子句的有名类型或方法要求元数为零。任意元数使用 `<?>`。[设计决策](standard-design-decisions.md)解释这些规则和通配符族。

Legacy 和语言选择 API 已移除。[迁移指南](../migration/shared-cecil-to-aspectn.md)说明从 Shared.Cecil.AspectN 迁移的破坏性变化。Rougamo 集成和对应大版本发布属于独立任务。

完整语法、优先级、修饰符、泛型变量语义、规范正则格式和诊断契约以[正式语言规范](../superpowers/specs/2026-07-31-aspectn-language-spec.md)为准。
