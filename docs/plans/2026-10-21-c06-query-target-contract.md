# C06 非限定查询目标契约修复计划

**目标：** 补全 `ITypeSystem` 查询目标的公开说明、第三方实现指南和契约测试，单独提交 C06。

**范围：** `IsAssignableTo.target`、`HasDirectAttribute.attributeType` 接受有方向的查询条件；空程序集名、空命名空间、Unknown 类型种类表示相应字段不限。名称、元数、构造实参及嵌套/数组结构仍有约束；`AreEqual` 保持严格相等。用户明确暂不新增公开比较器。本次只修改生产源码的 XML 注释，不修改运行逻辑。

## 1. 固定现有行为

- 新增核心记录型后端测试，检查具体关系和精确特性模式传出的空程序集、命名空间、Unknown kind，以及泛型/嵌套/数组内部结构。
- 新增同源跨后端测试，核对 Cecil、Reflection、Roslyn 源码/PE 的直接关系及特性查询，包含明确限定字段的正反例、严格相等、泛型、嵌套和数组形状。
- 在文档修改前运行测试，记录现有实现的实际状态。此修复补充契约，运行时已有正确行为，不以虚构产品红灯作为证据。

## 2. 补全公开契约

- 在 `src/AspectN/TypeSystem/ITypeSystem.cs` 添加英文 XML 参数及 remarks，明确目标方向、递归规则和不限定字段。
- 在 `docs/usage/backend-implementation.md` 编写中文实现指南与验收用例；从适配器指南链接。
- 明确实际候选不自动取得通配语义，指定程序集按完整 AssemblyIdentity 判断，Unknown 查询种类不等于结果 Unknown。

## 3. 证据与提交

- 同步 `TYPE-NOMINAL-001`、`ATTR-TARGETS-001`、`CROSS-COMPAT-001` 的具体场景及盘点，新增正式 Finding/证据/报告；不修改历史报告和 N/A。
- 完整 Release 编译、完整矩阵、格式、文档、知识库派生检查和 `git diff --check`。
- 按 requesting-code-review 做独立只读复核，并确认生产 diff 仅为 XML 注释。
- 在现有检出目录单独提交；保留原未跟踪文件，不推送；C07 后续讨论。
