# REVIEW-DS 裁决实施

## 已批准范围

用户于 2026-10-05 批准：

1. 明确保留不可用成员元数据，查询需要它时传播 Unknown。
2. 中立公共模型保留 Vector（SZ）与 General 数组形状。
3. 用独立提交移除自定义选择器/匹配器扩展。
4. 后置可复现性能基准，不宣称性能闸门完成。

取代更早设计及 Shared/Standard 差异文档的扩展和未解析元数据限制。其他独立 REVIEW-DS 发现仍是另一项工作。

## 目标

使核心、Cecil/Fody、Reflection、Roslyn 的批准公共契约准确，提供回归和当前文档。

## 架构

核心保持与后端无关。签名公开不可变可用性，已知值独立于不可用值仍可使用。查询按现有三态组合已知匹配、不匹配和未知事实。成员定义不可读时标记不可用，可恢复解析引用失败用独立诊断码。未知选择器产生解析诊断和恢复节点，不提供扩展注册表、扩展 AST 或自定义求值器。

数组签名保留显式 SZ/General。按维数的 Standard 模式继续接受两种一维形状；结构相等区分形状，名义关系遵守 Vector→General 方向并递归检查元素形状。

## 技术栈

C#、.NET 10/netstandard2.0、Mono.Cecil、Roslyn、Reflection、xUnit、既有一致性/net472 套件、PublicApiAnalyzers。

## 执行与验证

1. 先添加自定义选择器拒绝及扩展 API 缺失失败测试。移除注册表、编译/缓存维度、解析/绑定/求值支持及仅扩展测试，更新公共 API 基线和规范。运行核心/全仓测试及审查，独立提交。
2. 添加未解析成员失败测试（正/负可见性、特性、成员域、返回/参数引用分类、正则）和已知不匹配支配测试。签名增加不可变可用性，由相关求值器消费。统一可恢复 Cecil 解析失败，使用 AN3004。验证核心、Cecil 和同源码 Fody。
3. 添加中立和适配器数组失败测试，含嵌套及两个关系方向。公开形状并映射全部后端，更新结构/关系比较和快照，验证全部适配器及一致性套件。
4. 移除未使用 BenchmarkDotNet 版本锁定，说明性能闸门后置。记录真实构建/测试/格式证据，受影响时验证支持的替代依赖。
5. 审查提交和剩余变化，修复实质发现，运行完整 Release 构建/测试/格式。保留既有未跟踪文件，不推送或发布。

## 已完成改动

| 裁决 | 实现 |
| --- | --- |
| 移除语义扩展 | 独立提交 70e9628；未知选择器 AN2001 并恢复；移除扩展 API、注册表及缓存维度 |
| 补齐可用性修复 | b9b0d95；不可变事实/诊断、按事实三态求值、引用标记/分类分离、脱离关联特性保护、Cecil 可恢复读取 AN3004；含真实损坏延迟属性及已解析引用参数名回归 |
| 改进数组模型 | 33b3181；显式 SZ/General、递归身份、中立方向关系、独立泛型声明者 ID；一维 Standard 模式仍按维数，含特性及泛型关系优化 |
| 后置性能 | 正式设计明确性能闸门未通过，移除未使用 BenchmarkDotNet 版本；不含基准或性能结论 |

扩展移除、元数据修复及数组模型的独立审查通过。最终验收前复现并修复审查发现，含延迟诊断名称读取及维数模式优化回归。

## 最终验证（2026-10-05）

两个库目标 netstandard2.0/net10.0 启用公共 API 分析器构建。默认及替代依赖构建均零警告/错误，兼容性检查后恢复默认版本。

| 测试项目 | 默认通过 | 替代通过 |
| --- | ---: | ---: |
| AspectN.Tests | 673 | 673 |
| AspectN.Cecil.Tests | 112 | 112 |
| AspectN.Cecil.Fody.Tests | 114 | 114 |
| AspectN.Reflection.Tests | 81 | 81 |
| AspectN.Roslyn.Tests | 19 | 19 |
| AspectN.Conformance.Tests | 103 | 103 |
| AspectN.NetFramework.Tests (net472) | 2 | 2 |
| 总计 | **1104** | **1104** |

默认依赖 Cecil 0.11.5/Roslyn 4.12.0，替代 0.11.6/5.6.0，FodyHelpers 保持 6.8.1。最终矩阵没有排除测试。

验证命令：

```powershell
dotnet restore AspectN.sln
dotnet build AspectN.sln -c Release --no-restore
dotnet test AspectN.sln -c Release --no-build --no-restore --logger trx --results-directory artifacts/review-ds-results/final-default-v3
dotnet restore AspectN.sln -p:CecilVersion=0.11.6 -p:RoslynVersion=5.6.0
dotnet build AspectN.sln -c Release --no-restore -p:CecilVersion=0.11.6 -p:RoslynVersion=5.6.0
dotnet test AspectN.sln -c Release --no-build --no-restore -p:CecilVersion=0.11.6 -p:RoslynVersion=5.6.0 --logger trx --results-directory artifacts/review-ds-results/final-alternatives-v2
dotnet format AspectN.sln --verify-no-changes --no-restore --verbosity minimal
powershell -NoProfile -File eng/verify-packages.ps1
```

格式检查通过。既有程序包脚本验证五个包、两个 TFM、符号包及五个临时使用方构建。本会话通过本地产物包装器运行，调用未改动的脚本前核验全部解析后的清理路径。最终原始包日志位于 `artifacts/review-ds-verification/packages-final-v2.log`，TRX 在上述两个目录。

保留原未跟踪审查报告及 nul，不涉及相邻仓库、冻结缓存、包发布或推送。四项批准裁决之外的独立 REVIEW-DS 问题仍是独立工作。
