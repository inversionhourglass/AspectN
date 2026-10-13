# 两项证据补齐与九分类审查

- 日期：2026-10-13
- 用户授权：先补Fody和特性两项证据，再按知识库分类开展正式code review。
- 基线：858c590aedea76b278951820b222b47afe8df528；实际运行在dirty工作树，产品src未改。
- 计划：[实施计划](../plans/2026-10-13-capability-evidence-and-review.md)。
- 本轮完成了两项指定补证据和有范围的分类审查；四组产品违约未修复，一项契约边界待裁决。不能将“审查完成”解释为全部产品契约通过。

## 两项指定证据

| 范围 | 实际断言 | 新报告 |
| --- | --- | --- |
| Fody方向标志 | 真实[In] ref/[In,Out] ref，原生In/Out/by-ref位；各六种参数模式，RefKind.Ref及结果/空诊断 | [EV-016](evidence/EV-20261013-016.json) |
| 普通Cecil对照 | 同一测试源码和真实PE，用普通Cecil身份独立执行 | [EV-015](evidence/EV-20261013-015.json) |
| 核心特性语法排除 | 七种旧长名写法含AN4005及短名对照；get/set拒绝及getter/setter组合对照，共9个case | [EV-013](evidence/EV-20261013-013.json) |
| 特性继承排除 | 两个位置、带标记基类/仅继承override/直接标记override；运行时inherit:true与false独立控制；四种视图 | [EV-014](evidence/EV-20261013-014.json) |

原ATTR/S5混合编译和后端继承入口，本轮保留S5为核心语法，另建S6–S9表示Cecil、Roslyn源码、Reflection、Roslyn PE的继承查询排除。表达式合法而仅继承时NotMatched，与旧语法编译失败不同；未扩大到Fody继承或任意拓扑。

| 本轮完整测试项目 | Passed | Failed / Skipped |
| --- | --- | --- |
| Core | 928 | 0 / 0 |
| Cecil | 160 | 0 / 0 |
| Cecil.Fody | 162 | 0 / 0 |
| Conformance | 269 | 0 / 0 |

合计1519通过。其余项目没有本轮重跑，原报告继续按记录文件SHA、已执行断言及产品未改事实判断适用；新测试不能借用旧报告声称通过。旧15份报告保持原样。其中2026-10-12的三份报告有17个规范/示例SHA因后续中文化而不同，只保留历史意义，当前引用其后续替代证据；其余19份报告的全部记录快照与当前工作树相符。

## 分类审查与残余范围

阅读路径：Memorix任务简报→知识库入口→九分类摘要→17条能力及必要依赖→正式norm_refs→CodeGraph当前源码→测试与实际证据。没有将解释性泛型编码指南升级为规范，也没有从缺条目推断排除。

| 分类 | 核验重点 | 核验者和结论范围 |
| --- | --- | --- |
| 解析与词法 | 大小写、邻接、恢复、两入口错误屏障、连续否定 | /root/core_capability_review；声明范围静态审查及旧执行证据复核 |
| 泛型 | 元数、声明/闭合上下文、变量声明顺序、AND/OR/否定、缺事实 | 同上；实际泛型反例经主代理执行、非作者复核 |
| 绑定与匹配 | 正则子集、AN2012/AN3002、区域性无关、有限超时、位置编号/预算 | 同上；不穷举任意正则/Unicode |
| 类型与身份 | 程序集、嵌套身份、名义关系 | /root/type_capability_review；不穷举转发拓扑 |
| 类型使用位置 | ref/ByReference、数组rank/Shape、nullable/tuple/tasklike/不支持形状 | 同上；新Fody证据已非作者核验 |
| 横向契约 | 缺事实、取消/上限、原生关联、生命周期、资产/宿主、输入阶段 | 同上；Cecil/Fody取消反例确认，H1保持原裁决 |
| 成员 | 域、访问器及直接位置 | 原审查者提供静态调查后配额中断；/root/core_capability_review接手核对选择域、属性类型与分类路径及旧执行结果 |
| C#与元数据 | 源定义/实现/PE、partial及状态机记录 | 主代理执行探针；/root/core_capability_review于2026-10-13完成原生控制与限定终审 |
| 特性 | 直接记录/继承、具体与嵌入查询、PE记录身份 | 新语法/fixture源码先独立检查；接手审查者复核EV013/014/017–019的源码/日志SHA、实际断言和场景一致性 |

独立审查不是按目录读完就宣布无问题。源码调查提出的疑点由主代理顺序运行探针；没有并行构建争用输出。原元数据审查者的最终汇总因配额中断；后由/root/core_capability_review接手并完成限定终审。保留中断历史，不将后续核验归到原审查者名下。三分区最终结论均限于实际检查范围，不穷举所有生成成员、历史版本或任意PE。

### 不变量核验口径

存量N/A候选共10项：SYN四项、CROSS-LOGIC三项、CROSS-INPUT三项；全部核验，均沿用入口限定理由，没有新增N/A豁免。按N/A记录计最低抽查ceil(10/3)=4，本轮实际10项。

/root/core_capability_review于2026-10-13复核SYN四项；/root/type_capability_review同日复核六项横向N/A及USE-REF/INVCANCEL适用性。审查者曾用“能力+所有不变量”计算20/50分母，最终已更正；那些记录数只描述额外调查范围，不当作N/A抽样分母。历史核验主体/日期不覆写，本轮核验另记此处。

## 实际发现

| Finding | 已执行事实 | 状态 |
| --- | --- | --- |
| [FND-001](findings/FND-20261013-001.md) | Cecil/Fody正常detached闭包入口在预先取消时返回Unknown/AN3001；原生关联及其他后端对照抛OCE | contract、P2、confirmed，未修复 |
| [FND-002](findings/FND-20261013-002.md) | 残缺类型实参regex错误Matched；残缺方法实参重写元数且regex出确定结果 | contract、P2、confirmed，未修复 |
| [FND-003](findings/FND-20261013-003.md) | 两个真实可加载/调用的CLI返回类型重载合并形参OwnerId/AreEqual | contract、P2、confirmed，未修复 |
| [FND-004](findings/FND-20261013-004.md) | Roslyn读者本地同名类型替换实际PE readonly返回CA程序集身份 | contract、P2、confirmed，未修复；原生返回CA身份控制与非作者终审完成 |
| [FND-005](findings/FND-20261013-005.md) | 新Reflection上下文精确类型特性Unknown，但通配/复合仍消费已知嵌入事实 | 实际观测明确，契约优先级pending_decision，未认定产品违约 |

blocking=pending只表示维护者尚未裁决，不自动认定进程危害或合并/发布阻断。本轮未修改产品以修复这些问题，也不把supported改为excluded掩盖违约。每项附规范、输入、实际结果、反证和观察影响；未经运行的变量/约束串用、其他后端或版本不写成事实。

### 两组正常/待裁决边界

partial的Async/AsyncIterator没有显式状态机记录时，源实现IsAsync为true，而源定义和PE为false；显式MarkedAsync各视图true。15行每次执行均与独立原生符号/PE记录一致，tasklike及标记表达式也完成对照。[EV-018](evidence/EV-20261013-018.json)按具体视图登记正常观察，不新增“所有视图字段统一”的承诺。

detached特性精确快速路径与嵌入事实路径差异见[EV-019](evidence/EV-20261013-019.json)，result=inconclusive；不能把真实输出当作批准统一语义。维护者需明确原生关联前置条件与已有TypeAttributes的优先级。

## 可复现证据

[探针项目](reproductions/2026-10-13-capability-boundaries/README.md)保留六组源码及独立Cecil/Fody项目。补入原生返回CA控制后，最新分别执行一次，各45行、无probe_error。实际token分别50cebf1cceb9d05e、1ca091877d12ca03；初次和final输出保留，最新输出为probe-cecil-native.json/probe-fody-native.json；补控制前报告草稿也归档。

[EV-017](evidence/EV-20261013-017.json)登记实际产品预期失败，EV-018登记正常原生对照通过，EV-019保留待裁决。进程exit0只证明探针完成，绝不等于所有契约通过。报告记录SDK、运行时、依赖、源码/规范/探针快照、执行时间和原始JSON/日志SHA。

四项问题发现的独立来源为批准语言、架构及可用性条款；CLI重载先由CLR加载、枚举及JIT实际调用证明有效，不能归为H1。公开中立残缺实参保持非空必要字段，与损坏原生对象不同。

## 同步与验收

本轮17能力、94场景：83 verified、9 failed、2 unverified；契约为78 supported、15 excluded、1 pending。两项指定补证据完成，另登记已知失败和待裁决输入，不把旧限定通过场景整体改成failed。用户指南补充具体使用策略和视图边界；矩阵由唯一元数据生成，不能手改。

已通过文档校验（0错误/0警告）、生成器Check、解决方案及独立探针格式验证（workspace加载警告但退出0）；22份报告已逐一复核，19份当前报告的3023个记录快照及日志一致，3份早期报告的中文化差异单列为历史范围，报告中已声明的测试方法均对应Passed。最新原生readonly和partial控制通过。完整暂存差异与git diff --cached --check已检查通过，共37个任务文件；未暂存产品源码或原始nul。产品src、旧15份报告、原始nul保留；全仓同步闸门未启用，不推送或发布。
