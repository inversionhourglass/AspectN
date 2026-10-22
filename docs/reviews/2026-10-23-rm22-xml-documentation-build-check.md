# RM-22：启用公开 XML 文档的构建检查

日期：2026-10-23。对应 GLM-M13 与后续 RM-22。基准为 `f98f3c7affc2d0595988f4dbf2053d2fcabb3e2a`。维护者批准移除生产库的 CS1591 抑制、构建盘点缺口并补齐英文 XML，保持运行逻辑。

## 处理范围

[Directory.Build.props](../../Directory.Build.props) 保持生产库生成 XML 和 TreatWarningsAsErrors，移除 NoWarn 中的 CS1591，并用中文注释说明构建约束。范围仍是 src 直属生产项目，不扩大到全部测试或示例工程。

初次构建识别 150 个独立缺文档成员：145 个枚举值与 5 个受保护构造器，分布在 11 个核心源文件。两目标框架各报告 150 个 CS1591，编译失败共 300 个错误；日志中的末尾汇总又重复输出相同诊断，不将其算作更多缺口。

补充枚举值的具体含义、语法 token 拼写、元数据事实和后端能力，以及受保护构造器的参数、快照、源跨度与已有空参异常。不修改枚举值、API 声明、可见性、方法体或验证逻辑；内部类型的 public 辅助成员继续按有效可见性处理。公开 XML 使用英文，内部说明使用中文。

## 构建与检查

执行命令：

```powershell
dotnet build AspectN.sln -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal
```

- 移除抑制、补文档之前，构建按预期因 CS1591 失败；补齐后全解决方案构建成功，0 个错误，没有 CS1591。net472 测试项目另有 1 条 MSB3277 依赖引用冲突告警，本轮未改变依赖，也不宣称整个解决方案零告警。
- 五个生产项目的有效 MSBuild 属性均为 GenerateDocumentationFile=true、TreatWarningsAsErrors=true、NoWarn=1701;1702，不含 CS1591。该 C# 编译检查不会把所有 MSBuild 引用解析告警自动转为错误。
- 11 个 C# 文件去掉 XML 注释行并归一换行后，与开始前快照完全一致。10 份生产 XML（五个项目各 netstandard2.0/net10.0）解析成功。
- 指定 11 个源文件的 `dotnet format --verify-no-changes --no-restore` 退出 0；工具另提示工作区加载告警，不将该结果扩大为全解决方案无告警的证明。
- 最终生成 XML 逐项核对 145 个枚举值及 5 个构造器条目；`eng/verify-documentation.ps1 -AsJson` 返回 0 错误、0 告警。知识库由 `eng/generate-knowledge.ps1` 生成，随后 `-Check` 通过，派生矩阵只同步盘点说明。
- 不新增或重跑行为测试。本轮验证的是编译器文档检查、源码一致性和生成文档，不把编译成功当作运行时行为或全部兼容矩阵的重新验收。
- 独立只读审查者 `/root/rm22_xml_review` 核对最终 11 个源码及 10 份 XML 哈希、构建日志、文档含义与治理记录，未发现阻断问题；按其意见将 NamePatternSegment 的描述修正为同时适用于类型和成员名称的通用名称模式段。

日志和取证保存在 `artifacts/rm22/build-before-docs.log`、`build-after-docs.log`、`build-final.log`、`missing-xml.json`、`evaluated-policy.json`、`verification.json`；源文件修改前快照位于 `artifacts/rm22/before`。

## 能力与证据适用性

关联工程盘点 `INV-0014` 与 `CROSS-COMPAT-001` 的公开文档说明。同步知识库条目、盘点及审查流程；现有 S1—S5 的场景条件、验证状态和 N/A 保持，没有新增产品能力或扩大后端承诺。

用户可见变化是公开 XML 内容补全。用户指南中的表达式、调用方式、取消、资源或元数据行为均不变，因此无需改写运行时章节；源码 XML 即本轮补充的 API 使用说明。历史行为报告的语义适用范围未因本轮变化而缩小，历史证据不改写；构建闸门证据单独保存在本记录，不追加为某个完整产品场景的通过报告。

既有 README 修改、两份原始 review、综合报告和 `nul` 原样保留。既有未跟踪后续清单只更新 RM-22 进度，继续未跟踪；本轮不推送、发布或修改相邻仓库。
