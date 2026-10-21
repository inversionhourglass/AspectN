# 表达式语言与 C# 元数据边界

<!-- aspectn-usage:v1
{
  "schema_version": 1,
  "capability_ids": [
    "SYN-GRAMMAR-001",
    "TYPE-NOMINAL-001",
    "GEN-BINDING-001",
    "USE-SHAPES-001",
    "MEM-SELECTORS-001",
    "ATTR-TARGETS-001",
    "SEM-REGEX-001",
    "CSH-METADATA-001"
  ],
  "inventory_refs": []
}
-->

先选择要查询的事实：类型身份、方法签名、直接特性，或源代码声明。AspectN 表达式匹配前几类已承诺的事实；需要未提供的源码条件时，由调用方结合 Roslyn 等原生 API 分析。

## 名称、泛型和关系

```text
Company..*Service
Outer/Inner
System.Collections.Generic.List<int>
System.IDisposable+
```

名称区分大小写；嵌套类型用 `/`。省略具体名称的泛型子句要求零元数；`List<?>` 才是任意元数。`+` 按名义继承/实现闭包匹配，不是完整 CLR 可赋值判断；局部需要关系时可以使用 `Base+[]` 或 `IEnumerable<object+>`，与整体 `IEnumerable<object>+` 不同。

模式变量用 `$T`，由泛型子句中的正向出现声明：

```text
method($T Service.Convert<$T>($T))
```

普通 `T` 是字面类型名。默认 Declaration 将直接声明槽位中的变量作为候选自身形参的别名，按声明身份比较，拒绝具体实参及借用形参。返回/参数只能引用已有变量；特性、关系或嵌套实参中的首次捕获仅在显式 Capture 合法。AND 分享可见别名，OR 分支内新别名不逃逸，否定不能首次声明。Declaration 组合三态；Capture 才复制并保留动态备选，受每次 Match 的有限预算约束。结果不返回变量捕获。[配置示例](matching.md#泛型)、[名称与关系](../knowledge/capabilities/types/TYPE-NOMINAL-001.md)及[泛型作用域](../knowledge/capabilities/generics/GEN-BINDING-001.md)提供完整范围。

## 方法、构造器与属性

| 任务 | 表达式示例 | 范围 |
| --- | --- | --- |
| 普通方法 | `method(public string GetName())` | 只普通方法 |
| 非构造器方法类 | `callable(* *(..))` | 含属性/事件访问器、运算符及转换 |
| 全部方法类 | `execution(* *(..))` | 另含实例/静态构造器 |
| 属性两种访问器 | `property(string Service.Name)` | 按关联属性类型/名称 |
| 只 setter | `setter(string Service.Name)` | 属性类型是 string，不是 setter 的 void |
| 实例构造器 | `ctor(Service(int))` | 不含静态构造器 |
| 静态构造器 | `cctor(Service)` | 不含实例构造器 |

能匹配不代表成员有方法体。字段/事件声明没有独立选择器；事件 add/remove 等方法类仍可参与 callable 匹配。[成员专题](../knowledge/capabilities/members/MEM-SELECTORS-001.md)区分声明、访问器、操作符和后端边界。

## 可空类型、元组和任务类

```text
string?                      // 身份仍为 string，不能查找源码中的 ? 注解
int?                         // Nullable<int>
(int, string)                // 仅 ValueTuple，不匹配 System.Tuple
method(tasklike<int> M())     // Task<int> 或 ValueTask<int> 返回形状
```

普通返回 Task 的方法与 async 方法都能满足相同 tasklike 模式。IsAsync、迭代器及状态机记录是另一类事实。方法体内局部函数的 yield 不说明外层是迭代器。

对 partial 方法，先明确取得的是源定义、源实现还是发射后的成员：

```csharp
public partial class Api
{
    public partial Task<int> Load();
    public async partial Task<int> Load()
    {
        await Task.Yield();
        return 1;
    }
}
```

当前已验证的编译环境中，该例的 Roslyn 源实现 IsAsync 为 true，源定义和 PE/Reflection/Cecil 视图为 false；编译器没有为该 partial 方法发射状态机特性。它们均可按 Task<int> 返回形状匹配 tasklike。需要分析源码中的 async 时，读取实现符号；需要查询状态机特性时，检查实际记录。不要以某一个视图的 IsAsync 推断其他视图的字段值或源码写法；[场景依据](../knowledge/capabilities/csharp-metadata/CSH-METADATA-001.md)限定了实际输入及版本。

指针/函数指针可以映射为带规范显示名的不支持类型表示；这不提供结构化指针 DSL。params/scoped 方法可以按实际数组/ref 参数匹配，不能把这些 C# 关键词写成独立筛选修饰符。[类型形状](../knowledge/capabilities/type-uses/USE-SHAPES-001.md)、[源码与 PE](../knowledge/capabilities/csharp-metadata/CSH-METADATA-001.md)给出原因及当前后端条件。

## 直接特性

```text
attr(type MarkerAttribute)
attr(call MarkerAttribute)
attr(prop MarkerAttribute)
attr(para[0] MarkerAttribute)
attr(ret MarkerAttribute)
getter(string Name) && attr(call MarkerAttribute)
```

call 在访问器上表示访问器方法自身，prop 表示关联属性；exec 在访问器上包含方法和关联属性。查询不实例化对象、不应用继承特性规则。源码特性写法若被编码为元数据标志/其他表，不自动成为可匹配的真实 CA 记录；真实同类型 PE 记录仍可保留。[特性专题](../knowledge/capabilities/attributes/ATTR-TARGETS-001.md)详细区分这些情况。

泛型形参声明也可以携带特性，下面是合法的 C#：

```csharp
[AttributeUsage(AttributeTargets.GenericParameter)]
public sealed class MarkerAttribute : Attribute { }

public class G<[Marker] T>
{
    public void M<[Marker] U>() { }
}
```

这里的特性分别属于 `T`、`U` 的声明。将原生泛型形参映射为 `TypeSignature` 后，可以用 `HasDirectAttribute(AttributeTarget.ForType(parameter), marker)` 查询其自身记录；已知有标记为 Matched，已知无标记为 NotMatched。声明记录或原生关联不可用时仍为 Unknown。`attr(type MarkerAttribute)` 查询成员的声明类型 `G<T>`，不会转而查询 `T` 或 `U`，也不会因为形参有标记就命中。

旧位置名 callable/property/parameter/return 会产生 AN4005，应分别改为 call/prop/para/ret。get/set 也不是特性位置；要筛选访问器上的直接特性，使用上例的 getter 或 setter 与 attr(call ...) 组合。

类型特性查询优先使用方法签名中已知完整的 TypeAttributes，已知空列表也能证明没有直接记录。对同一带标记候选，下列三种查询在原 ReflectionTypeSystem 和新实例中均为 Matched；对已知空候选均为 NotMatched：

```text
attr(type MarkerAttribute)
attr(type Marker*)
attr(type (MarkerAttribute || MissingAttribute))
```

完整事实不依赖迁移原生关联。若 TypeAttributes 不可用，精确可具体化模式才可尝试原生直接查询；仍不可用则 Unknown。现有接口不能枚举列表，缺列表的通配/复合/变量查询也为 Unknown，不能据占位空列表认定没有记录。[特性场景](../knowledge/capabilities/attributes/ATTR-TARGETS-001.md)记录已批准规则及缺事实对照。

Roslyn 导入 PE 的只读返回标记按原始 CustomAttribute 构造器作用域恢复类型身份；读取编译中的同名类型不会替换真实记录。若实际标记依赖无法解析，ReturnAttributes 标为不可用，需要该事实的查询 Unknown/AN3001；已知 RefReadOnly 分类和不消费特性的普通签名查询仍可使用。[回归证据](../../tests/AspectN.Conformance.Tests/ReadonlyReturnAttributeIdentityTests.cs)包含普通/嵌入标记、同名读取者、多依赖和缺失依赖控制。

## 正则及错误处理

```csharp
var compilation = new AspectNEngine().CompileMemberExpression("regex((?=Run))");
// 环视不在支持子集中：有 AN2012，不能取得成员模式。
```

regex 做规范签名子串搜索，完整匹配需锚点；规范文本不能恢复未编码的源注解和数组 Shape。特殊分组、内联选项、反向引用等被排除；超时为 Unknown/AN3003，无效语法为编译期 AN3002。[正则专题](../knowledge/capabilities/semantics/SEM-REGEX-001.md)提供清单和规范编号规则。

编译先检查 TryGet* 和 Diagnostics；匹配再分别处理 Matched、NotMatched、Unknown。完整可执行的[边界示例](examples/MatchingBoundaries/Program.cs)覆盖查找、包装类型、选择器排除、引用参数与资源上限；它不能单独证明本章所有宽场景。
