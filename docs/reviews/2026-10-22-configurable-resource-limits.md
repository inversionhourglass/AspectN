# 分阶段资源限额实施审查

## 范围与批准

落实[已批准计划](../plans/2026-10-21-configurable-resource-limits.md)五组实施，正式契约为[资源规范](../superpowers/specs/2026-10-22-configurable-resource-limits-design.md)。在当前检出目录实施，验收取证时未提交、推送或发布，未修改相邻仓库。工作树同时存在其他审查改动，本报告仅评估资源配置及相关实际回归，不归属其他作者的变更。

公开变化包括三个不可变阶段选项、ResourceLimit/TimeLimit、解析/编译/映射取消参数、AreEqual取消参数、编译选项重组、移除AspectNLimits。默认源码256，Capture每收集器256个不同备选、整次50,000绑定工作，规范文本16,384；全部默认值以规范表为准，本轮未静默调整批准值。

## 五组交付

| 组 | 实现与结果 | 主要断言入口 |
| --- | --- | --- |
| 1 | 值类型、不可变选项、统一入口、全部字段按值缓存和内部查询通道；Core公共API不暴露后端或执行上下文 | ResourceOptionsTests、ResourceContractTests、ResourceApiTests、CompiledPatternCacheTests、PublicApiTests |
| 2 | 两条解析路径和绑定迁移显式工作栈；扫描/前瞻/临时节点共享额度，耗尽后常量大小恢复；树保留配置 | ResourceContractTests、FrontendProbe、MappingDepthTests、WorkStackTests |
| 3 | 候选预检查、通用工作、Capture、字符和规范文本计量；资源耗尽整次中止，结构失败局部，正则超时局部 | ResourceBudgetTests、BindingBudgetTests、InheritanceClosureLimitTests、RegexPatternTests |
| 4 | 每操作原生映射/查询对象；查询内共享一个映射子预算并同时消耗外层；原生辅助路径迭代及分配前检查 | NativeResourceBudgetTests、CecilResourceBudgetTests、Reflection/Roslyn ResourceBudgetTests、MappingDepthTests |
| 5 | 英文公开XML、可运行示例、中文配置/迁移/后端说明、当前规范及知识库同步 | ResourceXmlExampleTests、MatchingBoundaries、文档校验及生成器检查 |

新的工作单位表示处理规模，不是墙钟或精确字节保证。缓存命中按实际少执行的工作收费，仍先检查结构。第三方普通接口与装饰器不隐式接入内部累计状态；新增装饰器回归专门证明这一边界，不将其误报为预算泄漏。

## 能力、证据适用性与范围

本轮逐项重评以下场景；旧报告及快照保留，不改写哈希，不以测试文件存在代替执行。新证据EV-20261022-201记录最终实际矩阵及快照，EV-20261022-202记录独立审查和不变量适用性。旧场景只在原条件内重新执行；新增限额不从旧固定阈值报告推导。

| 能力/场景 | 受影响原因与重验范围 |
| --- | --- |
| CROSS-RESOURCES-001/S1—S8 | 既有结构、取消、C05/D01及Capture路径重构；按原fixture重验，旧8/12/13组压力显式保留旧预算，不能代表新默认值 |
| CROSS-RESOURCES-001/S9 | 解析配置、源码256/257、UTF-16、token/节点、根深度、语法树保留配置及Core千层小栈 |
| CROSS-RESOURCES-001/S10 | Core总预算/文本、分配前检查、第三方资源诊断、Capture默认4/8/9组、全部缓存字段与并发取消 |
| CROSS-RESOURCES-001/S11 | 普通Cecil原生配置、查询/映射子预算、节点/边累计、80槽、千层/2000层隔离及发布重试 |
| CROSS-RESOURCES-001/S12 | Fody独立程序集执行共享Cecil资源回归及既有结构/取消；不外推三后端全部新用例到Fody |
| CROSS-RESOURCES-001/S13 | Reflection独立策略、子预算、累计查询、80槽、千层数组及万层TypeDelegator前检查 |
| CROSS-RESOURCES-001/S14 | Roslyn源码独立策略、子预算、累计查询、80槽、错误符号文本及yield扫描；原生库自身成本边界仍存在 |
| CROSS-RESOURCES-001/S15 | Roslyn PE中80个类型槽位及80个方法槽位，有限80/Unlimited对照；不外推PE全部新增额度组合 |
| CROSS-LIFETIME-001/S1—S5 | 全字段按值LRU、完成结果发布、取消/异常不缓存、并发未命中隔离；适配器作用域和可回收对象既有输入重验 |
| CROSS-COMPAT-001/S1—S5 | 公开API迁移、无公开上下文、Fody身份、Windows net10/net472实际netstandard资产与第三方公开契约；移除旧构造兼容承诺 |
| CROSS-AVAILABILITY-001/S1—S8 | 资源失败不伪装普通缺失；原始字段可用性、缺关联、读取失败及D01通道重验 |
| CROSS-LOGIC-001/S1—S2 | 纯值真值表/转换保持；整次资源中止由资源条目治理，不扩张纯值N/A |
| SYN-GRAMMAR-001/S1—S3 | 解析执行、恢复和公开取消入口变化；标准语法和既有排除不变 |
| GEN-BINDING-001/S1—S11 | 树内解析配置、模式固定、专项/通用预算、固定槽位与动态备选；旧捕获语义显式Capture重验 |
| GEN-IDENTITY-001/S1—S7 | OwnerId、位置命名、身份链及比较改为预算化；保留方法/类型声明和闭合查询上下文 |
| TYPE-NOMINAL-001/S1—S12 | 结构比较、名义闭包、C05合法祖先和C06方向性目标；合成数组关系计量但语义不变 |
| ATTR-TARGETS-001/S1—S26 | 记录扫描/过滤计量、目标结构检查、D01、声明/捕获特性路径；原有精确输入边界重验 |
| SEM-REGEX-001/S1—S4 | 超时可显式不限；格式化文本限额16,384、超限AN3008；缺格式化事实AN3001及超时AN3003保持区别 |

按条目声明加载了GEN-IDENTITY、GEN-BINDING、USE-ARRAY、CROSS-AVAILABILITY、CROSS-LOGIC、CROSS-RESOURCES等依赖，并核对MEM-SELECTORS、CSH-METADATA的调用边界。选择器适用域、数组形状/方向和async/partial记录语义不变；相关现有测试在全矩阵重跑，未据此新增这些条目的场景或外推任意CLI生产者。coverage保留未盘点工具、版本、Fody兼容宿主和性能缺口。

## 不变量抽查与非作者核验

候选池为上述11条能力及各自4项不变量，共55条，最低抽查ceil(55/3)=19。选中全部11条能力记录，加CROSS-RESOURCES四项、CROSS-LIFETIME/INVCANCEL、CROSS-COMPAT/INVPARITY、ATTR-TARGETS/INVMISSING、SYN-GRAMMAR/INVCANCEL，共19条。抽查分别对照全矩阵实际输入、规范和入口，不按单个测试结果扩大承诺；下次轮转优先其余36条。

资源INVLOGIC区分C05局部结构失败、正则超时局部逻辑和AN3007/AN3008全局中止；INVPARITY按实际三后端和Fody独立身份取证；INVMISSING保留AN3001/3004与资源诊断区别；INVCANCEL含预算/缓存/发布边界。生命周期取消用缓存锁等待和并发未命中反例重验；兼容一致性只在同等事实及列出的宿主内判断；特性缺失不从空占位列表推出不存在。

SYN-GRAMMAR的INVCANCEL从not_applicable改为applicable，原因是Parse/Compile现公开CancellationToken。非作者`/root/options_review`于2026-10-22对照profile和签名确认；其余INVLOGIC、INVPARITY、INVMISSING在仅解析/绑定的S1—S3范围继续confirmed not_applicable，由同一非作者重新确认。元数据保存核验主体、日期和理由。CROSS-LOGIC纯值函数的三个旧N/A范围不变，不扩展到DSL求值器。没有新设排除来掩盖实现缺陷。

## 独立审查发现与修复

`/root/options_review`只读审查了核心预算/缓存、原生映射/查询及最终XML/规范。以下问题在本轮修复，并保留对应反例或等价回归：

- 编译缓存锁等待期间取消后仍返回命中：锁内再次检查取消，定向线程回归及并发未命中隔离通过。
- Core声明OwnerId、方法槽位合成及Unsupported诊断在工作检查前分配：先计量节点/字符/复制；宽槽位和长名称回归限制受控分配。独立复跑原反例得到AN3008，取消反例得到OperationCanceledException。
- 原生类型/方法限额混用、闭包祖先和直接特性目标漏结构检查：分别使用类型/方法配置，所有入口共享检查，保留C05分支语义。
- Cecil直接特性目标诊断使用递归FullName、长命名空间提前拼接及调用约定列表整体生成：使用固定诊断主体及迭代文本片段，逐项计量；小栈、提前分配和计数回归通过。
- Cecil发布根缓存后再收费可能留下缺原生关联：先完成必要计量再原子发布，1—600额度重试回归在普通/Fody身份通过。
- Cecil作用域短缓存漏ModuleReference宿主fallback：键同时保存scope和必要fallback引用，保留按文本合并等价身份。完整矩阵先发现两个旧MixedModule用例失败，修复后均通过；独立小对照确认跨宿主不串签名、同宿主复用。
- Roslyn错误符号ToDisplayString绕过生成文本限额，以及ContainsYield扫描方法体零计量：先以129/128文本和100空语句/work80建立失败回归，再改为预算化迭代格式化及逐节点收费/GetSyntax传token。独立复跑两个原反例均返回对应AN3008。
- 最终审查修正规范“失败不缓存”的歧义：正常完成且含错误诊断的编译结果仍可缓存，仅异常/取消不发布。补齐AspectNLimitException资源范围及空诊断异常XML，独立确认最终缺口消除。

独立审查没有替主代理重跑最终全矩阵、格式、生成器或全部压力；不将主代理命令写为审查者执行。审查者也未审定最终机器快照；报告用实际哈希固定主代理取证状态。内存局部分配检查不是整体性能基准。

## 验收命令与实际结果

已执行 `dotnet build AspectN.sln -c Release --no-restore -v:q`，零错误、一个既有MSB3277警告；`dotnet format AspectN.sln --verify-no-changes --no-restore --verbosity quiet`通过。最终 `dotnet test AspectN.sln -c Release --no-build --no-restore --logger trx --results-directory artifacts/resource-limits/verified -v:q`退出0，八个TRX共2,306项通过，无失败或跳过：

| 测试目标 | 通过 |
| --- | ---: |
| AspectN.Tests / net10.0 | 1,268 |
| AspectN.Cecil.Tests / net10.0 | 232 |
| AspectN.Cecil.Fody.Tests / net10.0 | 234 |
| AspectN.Reflection.Tests / net10.0 | 110 |
| AspectN.Roslyn.Tests / net10.0 | 30 |
| AspectN.Conformance.Tests / net10.0 | 415 |
| AspectN.NetFramework.Tests / net10.0 | 9 |
| AspectN.NetFramework.Tests / net472 | 8 |

同源80类型/方法槽位在Cecil、Reflection、Roslyn源/PE四视图的有限80和Unlimited两行通过；并非Fody同源PE证据。64个小栈隔离进程场景通过，含10个Core千层路径、三后端千层数组及Cecil千层泛型/声明者/引用和2000层直接特性目标。默认Capture4组及8组成功（8组256备选），9组AN3007；源长度显式不限以隔离捕获额度。旧8/12/13组报告使用旧额度，仅保留历史对照。

`dotnet run --project docs/usage/examples/MatchingBoundaries/MatchingBoundaries.csproj -c Release --no-restore`通过64项检查。ResourceXmlExampleTests四项及Reflection XML查询作用域示例在矩阵执行。最后2项Parse/Compile取消优先回归纳入最终Core1,268项。计量、配置、三态和并发验证都是确定性fixture；没有将正常样例或小栈通过解释为任意输入/耗时承诺。

执行环境为Windows NT10.0.26200、.NET SDK10.0.401、.NET运行时10.0.12及已安装.NET Framework宿主，PowerShell7.6.7；依赖Mono.Cecil0.11.5、FodyHelpers6.8.1、Roslyn4.12.0。最终运行期间实现/测试源码没有再修改。日志位于`artifacts/resource-limits/`，最终TRX路径、计数、SHA256和实现/测试/规范/配置快照由[自动证据](evidence/EV-20261022-201.json)保存；[独立审查证据](evidence/EV-20261022-202.json)另记非作者范围和局限。最终执行 `pwsh -NoProfile -File eng/verify-documentation.ps1`：17条能力、135场景、11章，零错误零警告；`pwsh -NoProfile -File eng/generate-knowledge.ps1` 与相同命令加 `-Check` 均退出0。11条受影响能力共98场景关联本轮限定证据，新增7个资源场景按输入与后端区分；其余能力未扩张。

构建保留既有net472的MSB3277 System.Memory 4.0.1.2/4.0.2.0版本冲突警告，零编译错误；本任务未更改该依赖策略。Windows/.NET10与net472实际测试不能证明其他操作系统、任意历史CLR/NuGet版本或Fody旧宿主。Unlimited只关闭所选额度，不取消合法性和外部运行时限制；第三方隐藏内部工作仍不属于精确计量承诺。性能基线、Rougamo迁移及包发布仍为独立任务。

## 当前证据与收尾

五组实施及计划验收已完成。旧checkpoint/reviewed失败TRX保留，不覆盖为成功；新验收单独取证。实现/测试/配置快照与当前文件逐项SHA256核验一致，dirty状态下不将HEAD单独当作精确代码证据。验收取证时未提交、推送或发布；2026-10-22用户随后授权本地提交，保留现有检出目录和其他任务的既有改动/未跟踪文件，不执行推送或发布。
