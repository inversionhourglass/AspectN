# E 阶段扩展与证据审查

- 日期：2026-10-13
- 实施：Codex 主代理 /root
- 代码基线：25eb76fb8459c3c7c4ad793b4b0d931713d03200；本轮测试在未提交工作树执行
- 计划：[E 阶段实施计划](../plans/2026-10-13-documentation-knowledge-base-expansion.md)
- 独立审查：/root/documentation_pilot_review，2026-10-13；修订后通过，无未解决的重要问题

## 实际交付与范围

新增12条能力，加原5条共17条，覆盖九分类；原8个未验证场景明确为实际输入并追加证据，另分行记录Roslyn PE、Fody、detached输入和剩余Fody interop负例。条件细化没有改变语言或匹配承诺，也没有把一个后端通过外推为整个能力通过。

最终76个场景：65个supported、11个excluded；73个verified、3个unverified。未验证场景为 USE-REF-001/S8、ATTR-TARGETS-001/S5、CROSS-INPUT-001/S3；分别保留Fody interop负例、部分特性排除断言、H1本轮未重验的具体缺口。

产品测试新增7个理论测试输入，验证裸Type事实的消费结果，位于 ReflectionTypeUseTests；产品 src 未改。另实现独立PowerShell生成器与黑盒测试，将矩阵/目录从现有元数据生成，避免第二份手写状态表。用户指南新增语言/元数据与生命周期/兼容两章，继续中文、通用匹配定位。

正式来源保持原批准/委托关系。未因进入知识库将 guide/history 自动升级为规范。H1 原裁决不重开、无新修复或真实可达性结论。

## 现有缺口与不变量变化

USE-REF-001/S2 补齐省略、!ref、精确四分类与已知类型失败的消费结果；S3–S6 分别登记有效fixture的Cecil、Roslyn源码、Reflection和独立Fody证据。S7单列Roslyn PE，S8明确Fody interop负例没有独立执行，不能借用非Fody一致性项目。

CROSS-AVAILABILITY-001/S2–S4 明确输入分别为Cecil引用/恢复、Roslyn错误基类及已知接口、Reflection已有Type的读取失败。Cecil取消/限制、Roslyn取消/限制按所列测试核验；S4未声称任意Reflection MapMethod读取异常都恢复。新增独立Fody及三个后端detached特性目标。

原 USE-REF-001/INVCANCEL 曾按仅mapper/ref值函数核验为N/A；E阶段S2/S7纳入完整Match入口后条件变化，改为applicable，不再豁免取消契约。旧N/A理由及非作者记录保存在[旧审查](2026-10-12-documentation-pilot.md)，不改写历史签章。

## 盘点与残余范围

全部17项库存都有条目链接和范围说明。documented仅表示已分析所列范围；INV-0006/H1、INV-0011部分排除断言、INV-0017未执行组合保留needs_evidence。

明确未完成的证据包括：

- Fody interop方向标志负例、Fody netstandard宿主和其他版本/输入组合。
- 特性继承、旧位置名称/get/set排除的全部独立失败断言；正式排除仍可建档，但不能假称已执行。
- H1人工损坏输入的真实程序集可达性；本轮没有重新执行H1或增加容错。
- 任意程序集转发拓扑、custom modifier链、正则输入、GC调度、所有历史NuGet/CLR版本和远程Linux新执行。

全仓强制同步规则没有自动生效；准备闸门的维护者生效范围确认保留后续决定。

## 实际测试与正式证据

| 执行项目 | 通过数 | 报告 |
| --- | --- | --- |
| 核心 net10.0 | 919 | [EV-004](evidence/EV-20261013-004.json) |
| Cecil net10.0 | 148 | [EV-005](evidence/EV-20261013-005.json) |
| Fody Cecil net10.0 | 150 | [EV-006](evidence/EV-20261013-006.json) |
| Roslyn net10.0 | 28 | [EV-007](evidence/EV-20261013-007.json) |
| Reflection net10.0 | 109 | [EV-008](evidence/EV-20261013-008.json) |
| 一致性 net10.0 | 263 | [EV-009](evidence/EV-20261013-009.json) |
| netstandard资产/net10宿主 | 7 | [EV-010](evidence/EV-20261013-010.json) |
| netstandard资产/net472宿主 | 6 | [EV-011](evidence/EV-20261013-011.json) |
| 可运行边界示例 | 56项检查 | [EV-012](evidence/EV-20261013-012.json) |

前八项共1630通过、0失败、0跳过。边界检查包括编译检查，不能解释为56个独立能力。原6份历史报告保持；新9份记录实际命令、时间、TRX/日志SHA、SDK/运行时/包依赖、原生Cecil身份、HEAD/dirty及实现/测试/规范文件快照。小栈隔离探针源码也纳入一致性报告依赖。

普通Cecil与Fody实际加载DLL的公钥token分别为50cebf1cceb9d05e、1ca091877d12ca03；包版本、程序集版本、宿主TFM仍分别记录。net472跑在实际安装的兼容Framework运行时上，不声称逐个测试所有历史运行时。

## 生成器验证

新增黑盒测试先运行失败：有效资料生成预期0，实际64，原因是生成器不存在。初版实现后8项通过。独立审查发现完整格式预检和 Markdown 通配符转义缺口；扩展转义用例实际失败后修复，最终13项通过，覆盖合法状态分离、确定性、陈旧/缺失输出、Check不写入、坏JSON/重复编号/越界引用以及未知profile、缺失不变量、错误排除码、损坏证据报告、重复JSON键拒绝且不覆盖输出。

生成器复用唯一文档校验器。生成预检只豁免两个固定派生目标的 Markdown 链接及陈旧派生正文；不豁免元数据引用、其他链接或语义字段规则。默认严格校验保持完整；新增7项边界回归与原38项一起实际通过。

生成结果带来源说明；支持/排除/待裁决和验证四状态分列。没有某后端/版本行不等于排除；已验证状态不替代报告当前适用性审查。

## 最终复核与验收

### 非作者不变量核验

核验主体：/root/documentation_pilot_review；日期：2026-10-13。新增记录原有8项N/A全部逐项核验，7项通过，1项拒绝并改为适用后核验通过：

| 条目 | 不变量 | 最终判断与依据 |
| --- | --- | --- |
| SYN-GRAMMAR-001 | INVLOGIC | N/A：仅解析/绑定，不消费 MatchStatus |
| SYN-GRAMMAR-001 | INVPARITY | N/A：单一核心语言，不比较后端 |
| SYN-GRAMMAR-001 | INVMISSING | N/A：源码记号不是候选元数据 |
| SYN-GRAMMAR-001 | INVCANCEL | N/A：选定 Parse/Compile 无 Token |
| CROSS-INPUT-001 | INVLOGIC | N/A：选定前置入口不消费三态 |
| CROSS-INPUT-001 | INVPARITY | N/A：不同输入阶段记录，不是同输入后端比较 |
| CROSS-INPUT-001 | INVMISSING | 原 N/A 拒绝；S2 GetParameters 查询可能缺失的依赖元数据，改 applicable。传播加载器异常，不扩大容错或强制转换 Unknown |
| CROSS-INPUT-001 | INVCANCEL | N/A：选定构造器/MapMethod/GetParameters 无 Token |

范围内未变的已确认N/A为 CROSS-LOGIC-001 三项；按 ceil(3/3)=1 抽查 INVPARITY，确认仍只操作状态/结果值，没有后端映射比较。USE-REF-001/INVCANCEL 的旧N/A在新Match范围下改为 applicable，旧核验记录保留。

生命周期 S4/S5 的 verified 预期收窄到实际卸载/回收及失去关联查询断言，单次映射依赖存活和仍持有适配器的上下文存活说明不作为已执行断言。

### 最终检查结果

非作者独立复核EV-004至EV-012的1546个文件SHA和9个日志SHA，全部与工作树/原始记录匹配；8份TRX合计1630通过。报告列出的462项方法记录均对应Passed，边界日志含56项检查。保留原6份历史报告及产品src，无改写。

| 检查 | 实际结果 |
| --- | --- |
| `pwsh -NoProfile -File eng/verify-documentation.ps1` | 17能力、76场景、9用户章节；0错误、0警告 |
| `pwsh -NoProfile -File eng/generate-knowledge.ps1 -Check` | 通过；生成后未手工修改派生页 |
| `pwsh -NoProfile -File eng/tests/generate-knowledge.Tests.ps1` | 13项黑盒通过，含通配符、正则反斜线和删除线字符转义 |
| `pwsh -NoProfile -File eng/tests/verify-documentation.Tests.ps1` | 45项通过，含7项生成预检边界 |
| `dotnet run --project docs/usage/examples/ReflectionQuickStart/ReflectionQuickStart.csproj -c Release --no-restore` | Documentation examples passed |
| `dotnet run --project docs/usage/examples/MatchingBoundaries/MatchingBoundaries.csproj -c Release --no-restore` | 56项检查通过，见EV-012 |
| `dotnet format AspectN.sln --verify-no-changes --no-restore` | 退出0；加载workspace提示warning，未报告格式变更 |
| `git diff --check` | 通过；原始未跟踪nul保留 |
| Markdown/Mermaid | 派生页实际Markdown解析未出现意外强调、删除线或内联代码；三个Mermaid图完成语法/结构人工检查，未执行图形渲染验收 |

Roslyn PE EventRaiser排除和H1处置符合既有批准来源。生命周期及绑定入口范围修订完成；支持承诺没有因测试范围收窄而改成排除。

暂存全部新文件后，缓存差异检查发现派生矩阵末尾多一空行；生成器统一为单个末尾换行，重新生成并由主代理重跑13项黑盒。此修订仅涉及输出格式，不改变场景、产品实现或证据适用范围。

### 路由与成本记录

定位依据沿知识库入口→九分类摘要→受影响条目→norm_refs/场景→证据及具体测试方法，生成矩阵只帮助检索。独立审查按报告范围核对方法及快照，未再次运行全部产品测试。八组顺序测试命令从2026-10-13T04:59:45.2800227Z至05:00:56.634825Z，含构建约71秒；编写、修改与人工核验未独立计时，不报告伪精确成本。

本轮修正生成预检、Markdown转义、N/A适用性、生命周期证据外推和绑定入口描述；没有重复打开已裁决H1，也没有以发现数量或阶段编号证明全部能力已知。三个未验证场景及未执行版本/后端组合继续可见。

E本轮验收完成：九分类盘点、指南同步、真实证据、单一来源生成和独立审查均已交付。完整全仓同步规则仍须维护者明确确认生效范围，本次不自动启用。
