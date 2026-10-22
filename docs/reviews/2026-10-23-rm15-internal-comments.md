# RM-15：内部注释语言与 Cecil 静态性说明

日期：2026-10-23。对应 K3-M21、GLM-M16 的注释建议。基准为 `4e26931abce5ec13e9131bb4ba9bf2763a126a42`；已有 README 修改、原始 review、综合报告和 `nul` 保留。既有未跟踪后续清单仅更新本项进度，继续保留未跟踪状态。

## 处理范围

按维护者批准，仅将 [Directory.Build.props](../../Directory.Build.props) 的默认配置时机注释及 [release.yml](../../.github/workflows/release.yml) 的符号包上传注释译为中文，并在 [CecilMappingOperation.cs](../../src/AspectN.Cecil/CecilMappingOperation.cs) 的静态性参数旁补充中文原因说明。

`definition?.IsStatic ?? !methodReference.HasThis` 保持原样。方法引用签名直接记录是否携带隐式 this；定义无法解析时，仍可据此判断静态性，不应将 IsStatic 标记为不可用。没有改变定义解析、元数据可用性或失败通道。该共享源码同时供普通 Cecil 与 Fody 编译，公开英文 XML 不变。

## 能力与证据适用性

说明涉及 `CROSS-AVAILABILITY-001` 的已知签名事实及工程盘点 `INV-0014`。去除相应注释行并归一换行后，三个文件其余内容与修改前快照完全一致；构建属性、发布命令、API 和运行逻辑均未改变。因此知识库场景、用户指南及现有行为证据没有语义影响，不新增场景、不改变验证状态或 N/A，也不改写历史报告。

## 验证范围

三个文件的修改前快照位于 `artifacts/rm15/before`；非注释内容一致性结果及最终哈希位于 `artifacts/rm15/verification.json`。Directory.Build.props 的 XML 解析成功，`git diff --check` 通过。

`eng/verify-documentation.ps1 -AsJson` 返回 0 错误、0 告警；`eng/generate-knowledge.ps1 -Check` 通过，既有派生资料无需修改。

独立只读审查者 `/root/rm15_comment_review` 核对注释语义、非注释内容及快照哈希，未发现阻断问题。

本轮不新增或重跑行为测试，不触发发布或远端 CI；注释一致性检查不作为新的适配器行为证据。
