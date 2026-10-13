# 审查修复实施计划

> 在现有检出执行批准修复，先观察失败回归，再修改生产代码。

**目标：** 修复 R1/R2/R4/R5/R6，并落实维护者对 R3 泛型变量声明的澄清。R1 等价状态去重于 2026-10-06 获批，资源预算不在本次范围。

**架构：** 保留中立签名和三态匹配，声明身份与构造后端上下文分开，公开不可变语法快照。变量由类型/方法泛型子句声明；返回/参数只引用已声明变量，泛型变量声明不可否定。

**技术栈：** C#、xUnit、Cecil/Fody、Reflection、Roslyn、net10.0/netstandard2.0/net472。

## 1. 不可变语法（R6）

- 在 tests/AspectN.Tests/Syntax 添加缓存 AST 修改及调用方持有引用记号集合的回归。
- 聚焦运行，确认失败。
- 语法集合快照用只读集合包装，包含 TypeUseSyntax.NegatedRefTokens；保留节点子项一致性和公开签名。
- 重跑语法及核心测试。

## 2. 三态备选（R2/R1）

- 添加部分闭包 OR 回归：后续约束移除已知匹配分支，含成员/特性备选。
- 修改 EvaluationContext、BindingEnvironment、求值器前确认失败。
- 合取和否定保留可能未知备选及其未知状态。
- 按批准合并等价环境，不折叠不同绑定或丢失未知状态。短重复通配表达式验证有界分配，不新增预算策略。
- 运行求值与核心套件。

## 3. 声明身份（R4/R5）

- 对 extern-alias 重载及闭合声明者方法形参加一致性回归，包含上下文特定约束。
- 修改三个声明者编码器和 Cecil 缓存上下文前确认失败。
- 递归有名参数加入程序集身份并保留规范结构。Cecil 方法声明者归一到声明，构造原生查询上下文另存。
- 运行全部适配器、同源码 Fody 和一致性套件。

## 4. 泛型变量契约（R3）

- 拒绝未声明返回/参数变量和否定变量定义，并测试合法方法/类型声明引用。
- 保留声明优先签名绑定，显式区分声明与引用上下文。
- 按维护者澄清更新受影响回归、正式/一致性文档；保留不冲突的元数、否定和分支作用域。
- 运行绑定和完整核心测试。

## 5. 最终验证

- `dotnet restore AspectN.sln`
- `dotnet build AspectN.sln -c Release --no-restore`
- `dotnet test AspectN.sln -c Release --no-build --no-restore`
- `dotnet format AspectN.sln --verify-no-changes --no-restore`
- 新产物目录验证五个本地包及临时使用方。
- 独立只读审查、修复实质缺陷并报告剩余裁决。保留未跟踪文件，不推送或发布。

## 完成证据（2026-10-06）

在 codex/aspectn-redesign-spec 工作树、提交前验证。保留原未跟踪审查文件和 nul，不涉及相邻仓库、worktree、推送或发布。

- 生产修复前观察失败：可变语法快照、未知备选丢失、重复等价 OR 分配、无效变量声明、跨程序集重载身份及闭合声明类型上下文。
- 独立审查还发现闭合 T/int 重载、继承方法引用和 vararg 调用点。添加真实 C#/IL 回归并修复声明解析失败。七个方法声明者身份测试全部通过，含递归数组/泛型参数程序集身份及函数指针显示保留。
- 18 个等价 (* || *) 项、212 字符链，在去重前分配 125,848,488 字节。回归现在以 Matched 通过小于 5 MiB 的分配上限。未复现栈溢出；观察到的是分配增长及 OOM 风险。未新增资源预算、限制或失败策略。
- 撤回 R3 按文本顺序绑定建议，保留声明优先。拒绝否定声明实参及未声明返回/参数变量；已声明变量引用不论文本顺序均合法。规范和一致性案例反映维护者澄清。
- 求值、解析/绑定/语法和适配器的独立只读审查均无剩余可执行发现。

最终验证：

| 命令 | 结果 |
| --- | --- |
| `dotnet restore AspectN.sln` | 通过 |
| `dotnet build AspectN.sln -c Release --no-restore` | 通过，零警告/错误 |
| `dotnet test AspectN.sln -c Release --no-build --no-restore` | 1,252 通过，零失败/跳过 |
| `dotnet format AspectN.sln --verify-no-changes --no-restore` | 通过 |
| `git diff --check` | 通过 |
| `./eng/verify-packages.ps1 -PackageDirectory artifacts/review-remediation-20261006-final2-packages` | 五个包及符号包、两个依赖组、十个使用方构建/运行通过 |

矩阵包含核心 752、Cecil 116、Fody 118、Reflection 82、Roslyn 19、一致性 152、net10.0/netstandard 使用方 7、net472 6。包验证版本 0.0.0-validation-3eb31011785d，未发布。
