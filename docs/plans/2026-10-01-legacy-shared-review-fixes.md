# Legacy/Shared 兼容性审查修复

> **历史计划，已于 2026-10-03 被取代：** [单一语言计划](2026-10-03-single-language-aspectn.md)移除 Legacy 及其对照基础设施。本文件记录之前已完成的兼容性修复，不授权恢复兼容规则或基线。

用户于 2026-10-01 授权修复；当时 Standard/Legacy 差异后置，冻结 Shared 对照保持不变。

1. 生产修改前添加回归：损坏签名编译返回诊断；未绑定 T 是字面名；声明泛型名仍绑定；!int 返回模式；通配声明路径；混合省略实参；元组元数和泛型邻接。
2. 修复 src/AspectN 解析器和绑定器，无效语法不进入绑定。保持合法语法树和有意义诊断码稳定，保留显式变量、引用返回及 Unicode 名称。
3. 扩展 tests/AspectN.LegacyOracle.Tests 与 Shared 对照接受/匹配。记录明确批准修正：重复泛型名、布尔作用域、无效前导路径、单元素/空元组和严格记号消费。恢复 Shared 泛型邻接规则，按用户澄清保留返回简写。
4. 在 docs/language 记录示例、历史行为和兼容策略。只为真实语料中用户明确批准的案例更新 approved-differences.json。
5. 运行聚焦回归、完整 Release 矩阵、格式及独立审查。修复可执行发现，最终证据记录到 artifacts/reviews。

验证命令：

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --no-restore
dotnet test tests/AspectN.LegacyOracle.Tests/AspectN.LegacyOracle.Tests.csproj -c Release --no-restore
dotnet test AspectN.sln -c Release --no-restore
dotnet format AspectN.sln --verify-no-changes --no-restore
```

范围不包含程序包发布、分支推送、worktree 或冻结对照修改。

五步全部完成。最终 Release 矩阵 969 测试通过，格式检查退出 0，独立审查者 213 个边界探针通过且无可复现发现。证据：`artifacts/reviews/aspectn-legacy-shared-fixes-2026-10-01.md`。
