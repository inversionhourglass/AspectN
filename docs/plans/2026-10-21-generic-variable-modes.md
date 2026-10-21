# 泛型变量双模式与捕获预算实施计划

> 按 executing-plans 分步执行；用户于 2026-10-21 明确“开始实施”，已批准完整设计。遵守用户仓库规则，在现有检出目录连续实施，不创建 worktree，不重复请求执行授权。

**目标：** 默认 Declaration 使用确定声明槽位；显式 Capture 保留原绑定语义并受数量、工作量预算保护。

**架构：** 编译配置传入 Binder，声明模式编译不可变槽位来源；求值上下文仅在 Capture 创建 BindingEnvironment。Capture 增量合并备选，预算由单次 Match 共享，在两种 Match 入口转换内部预算耗尽信号为 Unknown/AN3007。

**技术栈：** 现有 C#、netstandard2.0/net10.0、xUnit；Cecil/Fody、Reflection、Roslyn 源码及 PE，兼容 net472。

## 1. 配置与缓存

- 新增 `tests/AspectN.Tests/Evaluation/GenericVariableModeTests.cs`，先核对默认不捕获具体实参、原始开放定义可匹配，以及默认禁止特性/关系首次捕获。先运行，观察旧默认行为导致的真实失败。
- 修改 `src/AspectN/AspectNCompilationOptions.cs`，保留旧 TimeSpan? 构造器，增加设计中的四参数重载与只读属性；新增 `src/AspectN/GenericVariableMode.cs`。
- 在测试中补非法选项、旧构造调用及按模式/预算区分缓存；修改 `src/AspectN/Caching/CompiledPatternCache.cs`。
- 聚焦命令：`dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --no-restore --disable-build-servers -m:1 --filter FullyQualifiedName~GenericVariableModeTests`。

## 2. Declaration 绑定与槽位描述

- 为根类型、成员/属性声明类型和方法泛型槽构造确定来源；数组/可空外包装保存根访问路径，嵌套类型保存声明层级及槽位。
- 新增 `src/AspectN/Binding/DeclarationSlot.cs`，扩展 VariableSymbol 的可选来源；修改 Binder.cs、Binder.MemberPaths.cs、AspectNCompilation.cs 传递选项和声明位置。无需保留额外首次声明标记：声明处和引用处均比较同一个固定身份，声明处实际实参的检查由同一路径执行。
- 首次声明仅直接槽位；特性、关系、嵌套泛型槽首次声明给变量诊断，已有引用保持允许；现有 OR/NOT 作用域不变。
- 新增 `src/AspectN/Evaluation/DeclarationBindings.cs`：根据原始 Match 候选解析槽位及失败；类型使用已有码生成自身形参，方法使用实际声明槽并核对构造实参，不用原始定义替代实际值。

## 3. Declaration 求值路径

- 修改 EvaluationContext、TypePatternEvaluator、MemberPatternEvaluator、AttributePatternEvaluator：默认模式没有动态环境、分支备选收集或变量捕获。确定槽位事实缓存可共享，三态及诊断仍按现有规则合成。
- 扩展核心回归：自身/外部形参、类型与方法同位置区分、闭合构造、嵌套声明与包装、AND 引用、OR/NOT 不泄漏；检查上下文默认不创建 BindingEnvironment。
- 在 `tests/AspectN.Conformance.Tests/GenericVariableModeConformanceTests.cs` 增加真实同源正反例，Fody 在共享 Cecil 测试中单独核验。

## 4. Capture 预算与增量合并

- 新增 `tests/AspectN.Tests/Evaluation/BindingBudgetTests.cs`，先在低限额、不同变量 OR 链和 Unknown 反例上取得失败。
- 新增 BindingBudget、BindingBudgetExceededException、增量收集器；修改 BindingEnvironment，使记录创建、字典复制、绑定比较、去重遍历均先扣额度；去重按现有实例身份/确定性，不按结构值合并。
- 各求值节点及枚举候选计入共享工作量；OR/NOT/属性/祖先分支逐项合并，不先积累无限分支列表；数量上限针对唯一备选，重复仍扣工作量。
- 修改两个 CompiledPattern.Match 入口，预算耗尽整体 Unknown/AN3007，保留既有诊断和触发跨度；取消先检查并向外传播，不捕获其他后端异常。
- 覆盖数量/工作边界、等价去重、嵌套共享、重置、并发隔离及取消优先，不用墙钟断言。

## 5. 旧测试与示例迁移

- 运行完整矩阵；仅将明确依赖旧具体实参捕获/特性与祖先绑定的用例显式配置 Capture，保留无变量及开放声明的默认路径。
- 为默认与 Capture 分别提供端到端证据，不能以统一切换所有测试模式掩盖默认行为。
- 核对 net472/net10 宿主加载 netstandard2.0 的新选项兼容；保持 ITypeSystem 与后端映射形状不变。
- 用有界 8/12/13 组不同绑定和等价 OR 测量工作/分配；核对默认 4096 / 1,000,000，保存实际证据而非宣称整体性能闸门完成。

## 6. 规范、知识库与审查

- 将设计状态记录为已批准实施，保留原待审历史信息；同步语言 §13.2/21、设计决策、用户选项/变量/边界指南及公开 XML。
- 按设计 §6 更新受影响能力/场景、盘点和来源；新增 L01 Finding 与当前带模式配置的证据，保留旧报告，不伪造已验证组合。
- 执行知识库生成、-Check、文档校验；重评 N/A，必要改动交独立审查者确认。
- 完整 Release 构建/矩阵、格式、独立代码复核与最终差异检查后单独提交，不推送。

## 执行记录

六阶段已完成。默认模式先取得 4 失败/1 通过的实现前回归；捕获预算部分是在实现后补测试，没有取得该部分实现前运行时红灯，偏离第 4 阶段先红步骤的事实已在报告披露，不补造历史结果。

完整矩阵 2165/2165、收尾聚焦 68/68、示例 64 项检查通过；构建零警告/零错误、格式、知识库派生资料及文档校验通过。两次独立只读复核均无 Critical/Important 问题；最终复核包含 14 条存量抽查和未改变的 N/A 范围。具体命令、配置、工作/分配量、证据适用性及限制见[实施审查报告](../reviews/2026-10-21-generic-variable-modes.md)，本计划不代替执行证据。
