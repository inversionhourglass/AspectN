# 分类审查五项修复与验证

基线：`62daff6e39734be6e903e8a13ea81f56430edfdf`。本记录对应 FND-20261013-001–005 的实现、契约、使用指南与知识库同步；原[分类审查](2026-10-13-capability-evidence-and-review.md)和既有22份证据保留原值。

## 维护者裁决与适用范围

维护者于2026-10-14明确选择 FND-20261013-005 的规则：“优先使用已知完整的特性事实（推荐）：精确、通配、复合查询一致；所需事实缺失时再查询原生上下文，仍不可用则 Unknown。”该裁决已落实到[已批准可用性条款](../language/metadata-availability.md)及英文公共 API XML 契约说明。

“事实缺失后查询”受现有 ITypeSystem 接口实际能力约束：精确可具体化的类型模式可调用 HasDirectAttribute；接口没有原生特性列表枚举能力，通配、变量、复合模式在缺列表时仍 Unknown。不将复合查询拆为多个存在性查询，避免破坏同一条记录的组合语义。完整已知列表的三种查询一致；已知空也不需要原生关联。这次没有扩张公共 API，也没有改变直接 HasDirectAttribute 的关联条件。

H1（FND-20261011-001）继续接受风险，不增加损坏 Cecil 对象的容错承诺。全仓强制文档同步闸门仍未启用，需另经准备闸门及维护者确认。本轮没有发布包、推送分支或修改相邻仓库。

## 根因、修复与影响编号

| Finding | 根因与修复 | 受影响能力/场景 |
| --- | --- | --- |
| 001 | Cecil 缺关联提前返回位于取消检查之前；现在入口先检查 Token，未取消的缺关联仍 Unknown | CROSS-RESOURCES-001/S6、S7；CROSS-AVAILABILITY-001 |
| 002 | 非空实参数量替换声明元数，Formatter 把不完整构造当作完整文本；现在按原始已知正元数/声明槽位求值，实际需要的槽位缺失为 Unknown/AN3001 | GEN-BINDING-001/S4、SEM-REGEX-001/S4 |
| 003 | 方法声明者编码缺少返回形状，真实 CLI 仅返回类型不同的重载碰撞；三个适配器加入原始返回形状，保留闭合查询上下文 | GEN-IDENTITY-001/S4–S7；既有 S1–S3、类型形状控制 |
| 004 | Roslyn 导入 PE 隐藏返回 CA 时使用读取编译中的同名类型；现在读取原 MethodDef/返回 Param 的 CA 构造器及 TypeDef/TypeRef 作用域，按对应程序集符号恢复 | ATTR-TARGETS-001/S10、S14；CSH-METADATA-001 |
| 005 | 精确类型特性快速路径无条件查询原生关联；现在完整 TypeAttributes 快照优先，所需事实缺失才尝试精确原生查询 | ATTR-TARGETS-001/S11–S13；CROSS-AVAILABILITY-001、CROSS-LIFETIME-001 |

声明身份编码仍是实现细节，[说明指南](../language/generic-parameter-identity.md)保持 guide/pending 地位，不承诺旧 OwnerId 文本稳定。函数指针返回形状沿用已有调用约定与程序集编码器，不递归映射声明形参来生成身份。包、依赖和 PublicAPI 清单均无变更；完整构建包含两个库 TFM 及现有 API 分析器。

使用指南同步了[限制与常见坑](../usage/limits-and-pitfalls.md)、[语言与元数据](../usage/language-and-metadata.md)、[生命周期及兼容](../usage/lifetimes-and-compatibility.md)。入门安装、遍历查找、公开结果类型及程序集选择方式没有改变，故其余章节无行为更新；完整示例仍重跑核验。

## RED、GREEN 与有效原生控制

| 修复 | 修复前失败证据 | 修复后与反证 |
| --- | --- | --- |
| 001 | Cecil/Fody 各3例，缺关联预先取消各1例失败 | 各3例全部通过；关联取消仍传播，未取消缺关联仍 Unknown |
| 002 | 初始15例中7例失败；非作者指出的原始声明/当前槽位缺失再补4例形成19例，其中3例失败 | 19例完整通过；声明元数已知仍可匹配元数子句，完整实参无声明槽位、空原始形参列表不抹掉当前声明、开放定义、嵌套总元数都有控制 |
| 003 | 普通 Cecil/Fody 的真实 CLI 重载各1例失败；跨适配器返回身份缺陷有独立失败 | 真实 PE 先由 CLR 加载、枚举和调用，返回0/null；Cecil/Fody、Reflection、Roslyn PE 的 OwnerId 区分且 AreEqual 为 NotMatched |
| 004 | 普通/嵌入及同名读取者身份的3例失败 | 当前4例通过，含多同名依赖与实际标记依赖缺失；原生 Reflection CA、Cecil 构造器 Scope 独立控制 |
| 005 | 9例中4例失败：已知列表换上下文、不可用列表的精确原生回退 | 9例通过；带标记/已知空、原/新上下文、缺列表、原生有/无关联与 AN3004 原诊断保存均覆盖 |

原始局部日志/TRX 保存在 `artifacts/classified-fixes-20261014/`。最初 owner 跨后端运行中的闭合查询控制失败来自 fixture 没有注册动态根程序集；修正 resolver 后两例在产品修复前即通过，不能计作产品 RED。readonly 初次运行的测试/实现编译错误，以及首次完整回归中旧身份文本、旧精确类型特性测试前置条件引起的失败，也分别保留日志，不作为未修复产品缺陷。旧身份断言只增补返回形状，保留原参数、程序集和函数指针检查；原生查询测试现在显式声明 TypeAttributes 不可用。

新增返回形状控制覆盖 `Owner<string/int>.M<V>` 的声明身份、有效返回 T / List<T[]> 和 `where V : T` 查询上下文，另验证 ref V、ref readonly V、cdecl 函数指针返回；不借关闭变量或约束串用的未执行路径扩大结论。

## 当前完整执行证据

`dotnet build AspectN.sln -c Release --no-restore` 通过，0警告、0错误；最终收据及日志为 `artifacts/classified-fixes-20261014/build-clean-receipt.json`、`build-clean.log`。

| 当前报告 | 项目/宿主 | 通过/总数 |
| --- | --- | --- |
| [EV-20261014-001](evidence/EV-20261014-001.json) | 核心 net10.0 | 947/947 |
| [EV-20261014-002](evidence/EV-20261014-002.json) | Cecil net10.0 | 164/164 |
| [EV-20261014-003](evidence/EV-20261014-003.json) | Cecil.Fody net10.0 | 166/166 |
| [EV-20261014-004](evidence/EV-20261014-004.json) | Roslyn net10.0 | 28/28 |
| [EV-20261014-005](evidence/EV-20261014-005.json) | Reflection net10.0 | 109/109 |
| [EV-20261014-006](evidence/EV-20261014-006.json) | 一致性 net10.0 | 290/290 |
| [EV-20261014-007](evidence/EV-20261014-007.json) | netstandard资产在 net10.0 宿主 | 7/7 |
| [EV-20261014-008](evidence/EV-20261014-008.json) | netstandard资产在 net472 宿主 | 6/6 |

合计1717个测试通过，无跳过。各报告保存实际命令、时间、TRX/控制台哈希、环境和实现/测试/规范/配置文件快照；covered_assertions 的每个测试方法均核对当前 TRX 定义且所有执行结果为 Passed。完整执行不表示任意 CLI 输入或全部版本组合都验证过。

[EV-20261014-009](evidence/EV-20261014-009.json)保存两个用户示例的新执行，MatchingBoundaries 为56项检查，ReflectionQuickStart 自身断言通过。[EV-20261014-010](evidence/EV-20261014-010.json)保存未改源码的两个原探针，各45行、无 probe_error，逐项核对120项语义/原生控制检查；实际 Cecil 强名称标记分别 `50cebf1cceb9d05e` 和 `1ca091877d12ca03`，不把进程退出0当作语义通过。

## 知识库与证据适用性

核心、适配器和批准条款变化后，重新执行八组现有测试与两个示例，并以当前快照新建10份报告。17个能力条目中的旧证据引用重新评估：当前场景引用相应新报告，旧报告及旧审查的日期、计数、失败事实都保留。新增 ATTR-TARGETS-001/S12–S14 分别治理精确回退、缺列表非具体模式以及 PE 实际标记依赖缺失；S11 按维护者批准从 pending 改为 supported。其他原场景条件没有扩成全输入承诺。当前共97个场景：82个supported、15个excluded，96个verified、1个unverified（H1），无failed/pending；场景状态不能等同全仓功能已覆盖。

五个 Finding 的机器状态在回归证明后更新为 fixed，原始 expected/actual 与复现仍保存为2026-10-13历史事实，并追加当前处置。有关 open_findings 转入 related_findings；H1 不变。本轮没有新增或修改 N/A 适用性核验记录，原非作者确认值保留。盘点中 H1 真实程序集可达性及 Fody 兼容资产/其他版本仍有证据缺口。

## 非作者复核与验证记录

2026-10-14，`/root/core_capability_review` 完成001–003的代码、新旧断言、十份证据及全部97场景的限定终审，通过，无剩余阻塞。独立核对297个唯一快照文件、原始/控制台日志哈希，八组TRX的1717个通过结果及622个测试方法引用；构建收据、原探针实际JSON及CLR控制一致。22份旧报告、H1及17条不变量/N/A记录与基线保持一致。

2026-10-14，`/root/type_capability_review` 完成004/005、S10–S14、使用指南和未执行边界的限定终审，通过，无阻塞。实际核对4＋9＋8例新增一致性用例及旧有/无记录控制，PE标记作用域、缺实际依赖、完整事实优先和缺列表非具体Unknown与断言对应。两位复核者均只读核验已有产物，没有修改文件或另运行构建/测试。

复核意见已处理：SEM-REGEX/S4只登记类型实参少/多及方法实参少的实际执行范围，未登记方法多实参；五个Finding的旧章节明确标注2026-10-13历史事实和原处置，保留原文。

暂存检查发现7个新增C#文件和2个新增Markdown末尾多一个空行；仅清理EOF空白，语义无改动。为使最终源码快照与执行字节一致，另存clean轮构建、八组完整测试、两个原探针和两个用户示例，均通过；clean原探针JSON与此前120项核对通过的fixed输出字节一致。新报告引用clean轮收据和快照，旧轮日志保留。

`/root/core_capability_review` 对clean轮另完成快速证据复核：十份报告、297个快照文件、全部原始/控制台哈希、1717个Passed及622个测试方法引用均对应；build-clean、两个示例和两组实际探针一致。仅核验证据，确认EOF收尾不影响先前限定通过结论，无新增阻塞。

最终 `pwsh -NoProfile -File eng/verify-documentation.ps1` 为17能力、97场景、9用户章节，0错误、0警告；生成器与 `-Check` 通过，`dotnet format AspectN.sln --verify-no-changes --no-restore` 通过，证据快照/原始输出哈希及暂存差异检查通过。格式工具有工作区加载提示但退出0；Release完整构建的警告/错误计数均为0。原未跟踪 `nul` 保留，不纳入提交。

## 残余范围

旧 CLR 对“未知返回函数指针调用约定”的物理声明隔离分支只有静态复核，本轮兼容测试覆盖既有函数指针参数/现代桥接控制，不能作为该新增返回分支的执行证明。WindowsRuntime AssemblyIdentity.ContentType 按原始标志保留，尚未执行 WindowsRuntime PE fixture；真实转发和任意多模块拓扑也未穷举。

Roslyn PE 恢复的异常过滤不捕获取消或 AspectNLimitException；本轮是静态控制流核验，不为了测试该分支构造 H1 式损坏元数据。支持/失败的阶段依据沿用现有可用性契约。上述限制不阻止五项限定反例的修复，不自动成为新排除。
