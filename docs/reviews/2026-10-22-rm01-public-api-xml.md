# RM-01：公开 API 的 XML 文档补全

日期：2026-10-22。对应综合审查 K3-M1、GLM-M12 和后续核对 RM-01。维护者确认按现有实现补全模板式 XML 文档，不改变实现或公共 API。基准为 `HEAD 2f1d56a717983dc4b72d747200b58f39ac272925` 加开始本轮时已有的工作区改动；资源配置任务、RI-01—RI-03 和历史证据校验修复均保留。

## 范围与处理

本轮修改 34 个生产 C# 源文件的英文 XML 文档。逐项说明已存在的输入要求、返回含义、比较规则和契约异常，替换 `Returns the result of the operation.` 等无法帮助调用者判断行为的占位说明；保留已经能说明用途的简洁注释，不将“包含 Specifies”机械当作错误。

| 范围 | 补全内容 |
| --- | --- |
| RoslynSyntaxExtensions | 四个重载说明 mapper 与 Compilation/SemanticModel 必须共享编译实例、声明与模型必须共享语法树，以及空参、上下文不符、缺少声明符号的异常。相同源码文本不能替代实例对应关系。 |
| AssemblyIdentity / TypeIdentity | 明确程序集名称和区域性区分大小写、公钥 token 忽略大小写、null 区域性/token 归一为空字符串，版本和空字段在严格相等中不是通配。类型身份说明每层元数、包含链及 FullMetadataName 的格式。 |
| 三种类型系统实现 | 查询方法使用 `inheritdoc` 引用现有 ITypeSystem 契约，避免重复维护 C06 查询侧限定规则；Cecil 另列释放后异常。接口只补返回状态和取消异常说明，原查询规则保留。Cecil 源文件同时供 Fody 项目编译。 |
| 解析、编译及匹配入口 | 说明返回恢复树或 compilation 不等于编译成功、TryGet 的 out 值及成功条件、输入入口区别、选项用途、取消和直接空参错误。保持资源任务已写出的参数及默认值说明。 |
| 签名与特性目标 | 说明形参位置、声明/构造实参、直接特性快照、属性关联、ref 使用位置、数组 rank/CLI shape 和 AttributeTarget 的有效载荷。未重新制定 C01—C07 或 D01 的语义。 |
| 文本、诊断与语法树 | 说明 UTF-16 单位、半开区间、缺失 token、源顺序、泛型子句及语法节点的参数角色，补充已有的空参、范围及 TextSpan.End 溢出异常。语法节点文案不新增构造时语法验证要求。 |
| 三态结果与逻辑 | 说明 AND/OR/NOT 的返回状态、诊断保留、Boolean 转换和结果快照；Unknown 工厂只替换原占位释义。 |

本轮没有扩大所有权、取消或元数据读取的恢复范围，没有新增比较器、验证逻辑、重载、公共类型或测试输入。RI-01 已补全的模块所有权说明保持；现有 MapTypeUse 后端边界也保持，不将它们重新表述为“完全没有契约”。

**RM-02 仍待确认。** Unknown 工厂的零参调用、异常契约及是否调整诊断参数形状属于另一条讨论，本轮不提前裁决。其他 Minor 的格式检查安排、CS1591 治理、测试命名等也不在范围内。

## 能力与证据适用性

本轮主要归属盘点 `INV-0014`，同步 `CROSS-COMPAT-001/S1、S5` 的公开文档说明。类型身份、解析语法、三态逻辑和签名字段的注释分别关联 `TYPE-NOMINAL-001`、`SYN-GRAMMAR-001`、`CROSS-LOGIC-001` 及相应签名/特性能力，但没有改变任何输入、分支、验证规则或状态；因此不调整这些能力的 supported/excluded、场景验证状态或 N/A，也不以文案更新提高任意后端的验证范围。

非 XML 行与本轮修改前快照一致，已有产品证据的语义适用范围没有因本轮变化而缩小；历史报告不改写，资源配置任务导致的证据重评估仍属于该任务。本次既有测试重跑只证明下面列出的实际输入，不替代历史报告的全部组合或成为新的全面覆盖承诺。

用户受影响说明同步到[选择适配器](../usage/adapters.md)的 Roslyn 语法入口约束和[后端实现指南](../usage/backend-implementation.md)的严格程序集相等规则。其他使用指南无本轮行为影响：注释只解释现有流程与字段，没有增加语法、改变失败通道或要求调用方迁移。

## 验证与限制

修改前候选源文件保存于 `artifacts/rm01/before`，实际修改清单为 `artifacts/rm01/modified-source-files.json`。逐文件归一换行后，只移除以 `///` 开头的文档行，其余源码完全一致；这项检查覆盖本轮修改前的真实工作区，不能用 HEAD 差异中的其他任务变更来冒充本轮实现修改。

- `AspectN`、`AspectN.Cecil`、`AspectN.Cecil.Fody`、`AspectN.Reflection`、`AspectN.Roslyn` 的 Release 构建均覆盖 `netstandard2.0`、`net10.0`，0 错误、0 告警；编译器检查 XML 及 cref 的有效性。
- 既有 RoslynSyntaxEntryPointTests：3 项通过，验证正常上下文和外来语义模型拒绝；没有新增重复实现的文档测试，也没有声称这些用例覆盖全部异常分支。
- 核心既有签名、文本、三态逻辑及查询目标聚焦回归：102 项通过，0 失败；与 Roslyn 的 3 项合计 105 项实际用例，没有扩大为全解决方案或完整兼容矩阵的验证。
- 10 份生成 XML 均解析成功，没有残留的空返回值模板或未解析 cref；三适配器及 Fody 的 5 个接口文档引用均能在相应目标框架的核心 XML 中找到实际成员，四个 Roslyn 语法重载均具备参数、返回和上下文异常说明。检查脚本及输出保存在 `artifacts/rm01/check-xml-docs.ps1`、`artifacts/rm01/xml-check.json`。
- 已执行 `pwsh -NoProfile -File eng/generate-knowledge.ps1`、同命令加 `-Check` 及 `pwsh -NoProfile -File eng/verify-documentation.ps1 -AsJson`，全部通过，文档校验为 0 错误、0 告警。派生资料由正式生成器维护，`git diff --check` 没有空白错误。
- 独立只读审查者 `/root/rm01_xml_review` 已核对最终 XML 与实现、使用指南、能力及清单，并直接核对两份 TRX 和 34 项修改清单，未发现阻断问题。按其意见将选择器关键字和命名段/元组分隔符分别细化；最终源码哈希保存在 `artifacts/rm01/final-source-state.json`。

测试 TRX 保存在 `artifacts/rm01/core`、`artifacts/rm01/roslyn`。本机为 Windows、.NET SDK 10.0.401；没有运行远端 CI、Linux 或整个兼容矩阵。没有提交、推送或发布。
