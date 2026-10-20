# C04 泛型形参直接特性查询修复计划

**目标：** 修复 Cecil 直接 API 对泛型形参特性错误返回 Unknown 的问题，单独处理 C04 并提交。

**架构：** 在 `CecilTypeSystem.GetTypeAttributes` 中区分泛型形参声明和普通类型引用。声明从形参自身读取真实记录，保留 `CecilMetadataReader` 的可恢复异常通道；没有原生关联或没有可用声明记录的引用继续 Unknown。不改变 DSL 的 `attr(type ...)` 目标。

**环境：** 当前检出目录；C#、xUnit、Cecil/Fody、Roslyn 源码/PE、Reflection；既有未跟踪报告及 `nul` 保留，不推送。

## 1. 先取得失败证据

- 新增 `tests/AspectN.Cecil.Tests/CecilGenericParameterAttributeTests.cs`，读取测试程序集中的真实类型/方法泛型形参，验证有/无特性、owner 上的特性不替代形参记录、构造器不实例化、取消及缺关联/缺定义控制。
- 该文件由现有 Fody 测试项目共享编译，分别执行两套聚焦测试。
- 新增 `tests/AspectN.Conformance.Tests/GenericParameterAttributeTests.cs`，同一真实编译程序集在三端及 Roslyn 两视图验证，并保留 DSL 目标控制。
- 记录旧实现的断言失败，再修改产品代码。

## 2. 最小修复

- 修改 `src/AspectN.Cecil/CecilTypeSystem.cs` 的特性来源选择，避免对泛型形参声明调用普通类型 Resolve。
- 按独立复核补充缓存边界：内部泛型形参缓存键区分声明与引用来源；在同一类型系统内双向映射两个对象，既不丢失声明记录，也不借用声明事实。
- 不改签名模型、特性继承、公开 API 形状、未知事实语义及其他 Finding。
- 聚焦回归通过后执行全部受影响后端测试。

## 3. 同步资料

- 记录 `ATTR-TARGETS-001` 的泛型形参场景及 `CROSS-AVAILABILITY-001` 的可用性边界，精确列出后端/输入/证据范围。
- 更新用户特性指南、正式 Finding、修复报告及证据 JSON；原始综合报告保持原文及未跟踪状态。
- 生成知识库并执行 `eng/generate-knowledge.ps1 -Check`、`eng/verify-documentation.ps1`。

## 4. 验证与提交

- 完整 Release 构建、完整矩阵、格式检查、`git diff --check`。
- 按 requesting-code-review 请求只读独立复核并处理本次发现。
- 单独提交 C04，保留 C05–C07 待后续讨论，不推送。
