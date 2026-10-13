# 数组形状

本契约于 2026-10-05 获批。`ArrayTypeSignature` 保留数组维数、元素类型和 CLI `ArrayShape`：`Vector` 是 SZ 一维数组；`General` 包含非 SZ 一维数组和多维数组。不建模单个数组实例的长度或下界。现有 `(elementType, rank)` 构造器在维数为一时选择 Vector，其他维数选择 General。显式形状构造器拒绝维数不为一的 Vector 和未定义形状值。

Cecil 映射 `ArrayType.IsVector`，Reflection 映射运行时的 SZ 事实，Roslyn 映射 `IArrayTypeSymbol.IsSZArray`。结构相等递归比较形状，包含数组元素和泛型实参。泛型形参的声明者标识保留 General 一维参数数组的 `[*]`，避免只因数组形状不同的重载被合并。

数组之间的名义关系查询同时适用于脱离原生关联的签名和已映射签名：

| 候选 | 目标 | 结果 |
| --- | --- | --- |
| int[] | int[] | Matched |
| int[*] | int[*] | Matched |
| int[] | int[*] | Matched |
| int[*] | int[] | NotMatched |
| string[,] | object[,] | NotMatched |

维数和元素身份必须一致。元素身份包含嵌套形状；数组元素协变和泛型变体仍在名义关系契约范围之外。已知的形状或维数不匹配，无需解析元素即可给出结果。需要元素身份而其未知时，传播对应诊断。与有名基类或接口的关系仍使用适配器上下文：只有 Vector 提供数组的泛型集合接口。

Standard 类型模式仍按维数匹配：`int[]` 匹配两种一维形状。DSL 不新增 `int[*]` 语法。规范正则签名文本保留现有按维数呈现的方式，因此正则表达式不是新的形状选择器。元数据声明者身份和一致性测试快照独立于显示文本保留形状。

该维数规则也适用于有名泛型模式内部，包括特性模式及 `+` 关系目标。包含一维 `[]` 的模式不能优化为单一具体 Vector 身份；这些路径针对嵌入特性或已声明的继承闭包求值。公共 `AreEqual` 和 `IsAssignableTo` 仍使用显式元数据身份与形状。
