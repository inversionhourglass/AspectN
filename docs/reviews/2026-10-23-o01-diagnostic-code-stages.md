# O-01：诊断码与检测阶段说明

日期：2026-10-23；对应K3-M3；基准 `c24a84dc81ea16b30696c6150ef1869bba6ade09`。维护者批准保留现有编号和实现，仅澄清诊断码前缀不代表实际检测阶段。

## 修改与依据

在[语言规范第17节](../superpowers/specs/2026-07-31-aspectn-language-spec.md)和[结果与诊断指南](../usage/results-and-diagnostics.md)补充分类规则：调用方按入口及返回结果判断处理阶段，按完整诊断码识别原因。正则子集扫描产生AN2012、未知选择器解析产生AN2001、无效正则语法绑定产生AN3002，三者都可导致编译失败。

现有规范第15节已明确AN2012与AN3002均为编译期错误，第16节明确解析时报告AN2001。本次不更改编号、诊断消息、报告时机或错误处理；原审查没有被重新认定为运行缺陷。

当前实现依据为[Lexer.LexRegexBody及ReportUnsupportedRegexFeature](../../src/AspectN/Parsing/Lexer.cs)、[Binder.BindRegexPattern](../../src/AspectN/Binding/Binder.cs)和[诊断定义](../../src/AspectN/Diagnostics/DiagnosticCodes.cs)。同步校正正则知识库“Binder检查受限子集”的文字归属，实际子集检查在Lexer；没有将这次文案修订写成实现迁移。

## 能力与验证范围

受影响条目为SYN-GRAMMAR-001/S1、S2，以及SEM-REGEX-001/S1、S3；同步其阶段说明，并为正则条目增加第17节依据引用。指南章级引用增加SEM-REGEX-001。场景元数据、验证状态、evidence_ids及N/A均保持；正则泛型编号、资源计量、匹配和后端行为不变，相关依赖无需扩大重验范围。

本项只修改Markdown文档，未修改产品、测试、工程脚本、CI或公开API；不新增重复测试，也不将静态核对记作产品回归通过。旧报告及历史证据保留，适用性在原输入与实现范围内不变。

## 当前核验

对照Git基准，src、tests、eng、CI及正式Finding/证据文件均无差异。结构化比对确认两条能力的场景、证据、N/A及其余元数据保持，只有正则norm_refs增加第17节，指南章级引用增加正则能力。

正式知识库生成及`-Check`、`eng/verify-documentation.ps1 -AsJson`和`git diff --check`通过，文档0错误、0告警；两份派生文档均无内容变化。未重跑产品测试、示例程序、打包或消费者。未跟踪后续清单仅同步O-01为已澄清、O-02继续待确认，不纳入本项提交；其他既有WIP保留。

独立只读审查未发现可操作问题；确认说明与Lexer、SelectorParser、Binder及既有第15/16节契约一致，“当前实现”例子没有新增诊断时机承诺，范围与元数据保持声明准确。
