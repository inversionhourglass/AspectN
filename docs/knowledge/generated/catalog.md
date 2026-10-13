# 分类能力目录

<!-- 由 eng/generate-knowledge.ps1 生成；请修改能力条目或 coverage.json，勿手工编辑。 -->

本页由唯一场景元数据派生。支持承诺与登记验证状态分开；已验证不代替当前证据适用性审查。没有某后端或版本的行不等于排除，也不代表全部输入组合均已验证。

## 类型与身份

| 能力 | 范围入口 | 场景数 |
| --- | --- | --- |
| [TYPE-NOMINAL-001](../capabilities/types/TYPE-NOMINAL-001.md) | 名称、嵌套、程序集身份与名义关系 | 5 |

## 泛型

| 能力 | 范围入口 | 场景数 |
| --- | --- | --- |
| [GEN-BINDING-001](../capabilities/generics/GEN-BINDING-001.md) | 泛型元数、模式变量与作用域 | 3 |
| [GEN-IDENTITY-001](../capabilities/generics/GEN-IDENTITY-001.md) | 泛型声明身份与闭合查询上下文 | 3 |

## 类型使用位置

| 能力 | 范围入口 | 场景数 |
| --- | --- | --- |
| [USE-ARRAY-001](../capabilities/type-uses/USE-ARRAY-001.md) | 数组形状、rank 模式与名义关系 | 4 |
| [USE-REF-001](../capabilities/type-uses/USE-REF-001.md) | ref-kind 与 by-reference 事实 | 8 |
| [USE-SHAPES-001](../capabilities/type-uses/USE-SHAPES-001.md) | 可空、元组、任务类与不支持的类型形状 | 5 |

## 成员

| 能力 | 范围入口 | 场景数 |
| --- | --- | --- |
| [MEM-SELECTORS-001](../capabilities/members/MEM-SELECTORS-001.md) | 方法、构造器、属性和访问器选择域 | 5 |

## C# 与元数据表示

| 能力 | 范围入口 | 场景数 |
| --- | --- | --- |
| [CSH-METADATA-001](../capabilities/csharp-metadata/CSH-METADATA-001.md) | async、迭代器、partial 与源码/PE 边界 | 5 |

## 特性记录

| 能力 | 范围入口 | 场景数 |
| --- | --- | --- |
| [ATTR-TARGETS-001](../capabilities/attributes/ATTR-TARGETS-001.md) | 直接特性目标、真实记录与伪特性 | 5 |

## 解析与词法

| 能力 | 范围入口 | 场景数 |
| --- | --- | --- |
| [SYN-GRAMMAR-001](../capabilities/syntax/SYN-GRAMMAR-001.md) | 词法、邻接、入口与编译诊断 | 3 |

## 绑定与匹配

| 能力 | 范围入口 | 场景数 |
| --- | --- | --- |
| [SEM-REGEX-001](../capabilities/semantics/SEM-REGEX-001.md) | 正则子集、规范签名与超时 | 3 |

## 横向契约

| 能力 | 范围入口 | 场景数 |
| --- | --- | --- |
| [CROSS-AVAILABILITY-001](../capabilities/cross-cutting/CROSS-AVAILABILITY-001.md) | 元数据可用性与独立事实 | 8 |
| [CROSS-COMPAT-001](../capabilities/cross-cutting/CROSS-COMPAT-001.md) | 公开 API、程序集身份与兼容资产 | 4 |
| [CROSS-INPUT-001](../capabilities/cross-cutting/CROSS-INPUT-001.md) | 有效输入、候选加载与人工损坏对象 | 3 |
| [CROSS-LIFETIME-001](../capabilities/cross-cutting/CROSS-LIFETIME-001.md) | 引擎缓存、后端生命周期与并发隔离 | 5 |
| [CROSS-LOGIC-001](../capabilities/cross-cutting/CROSS-LOGIC-001.md) | 三态逻辑、诊断及 Boolean 转换 | 2 |
| [CROSS-RESOURCES-001](../capabilities/cross-cutting/CROSS-RESOURCES-001.md) | 取消、类型深度、元数与文本预算 | 5 |

共 17 条能力；具体承诺、输入条件、失败原因与证据见[逐场景矩阵](support-matrix.md)。
