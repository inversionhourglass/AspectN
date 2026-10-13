# 贡献指南设计

## 目标

为计划中的 AspectN 实现添加简明、贴合仓库的贡献说明，不将根指南扩展为完整风格手册。

## 交付物

- AGENTS.md 是入口。原设计使用“Repository Guidelines”标题，约 200–400 个英文单词，概述目标布局、架构边界、开发命令、测试、提交/PR 规则及详细规则链接。这里保留当时要求；现行中文规则以根 AGENTS.md 为准。
- .editorconfig 包含可机器校验的 C# 格式和命名：四空格、UTF-8、文件范围命名空间、私有实例字段 _camelCase、私有静态字段 _PascalCase、常量 UPPER_SNAKE_CASE。
- .agents/rules/csharp-style.md 提供详细 C# 说明，解释提前退出、减少嵌套及单行不加括号的 return/continue/break，并给紧凑示例。
- .gitignore 保留维护者当前 AI 工具排除项，同时允许跟踪 .agents/rules/**。

## 仓库模型

指南描述工业化重构计划批准的目标结构，而非仅描述当时只有文档的骨架：

- src/AspectN 包含与后端无关的解析器、语义模型、签名和匹配器。
- src/AspectN.Cecil/Roslyn/Reflection 包含后端映射和类型系统逻辑。
- tests/ 下测试项目对应生产项目，一致性、.NET Framework 及测试程序集独立。Legacy 对照由[单一语言计划](../../plans/2026-10-03-single-language-aspectn.md)退休。
- benchmarks/、eng/、docs/ 分别保存性能套件、工程脚本和规范。

## 验证与提交

核对 Markdown 字数及链接，检查 .editorconfig 命名规则键，确认 Git 跟踪 .agents/rules/csharp-style.md。原文档任务时生产项目尚不存在，不要求运行构建或测试。AGENTS.md、.editorconfig、.agents/rules/csharp-style.md 与维护者的 .gitignore 改动一起作为单个聚焦提交。
