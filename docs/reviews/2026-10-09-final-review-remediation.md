# 三份 review 核查后的修复记录

日期：2026-10-09。用户批准问题 1–7 的推荐方向和现有规范下的修复，要求在现有 checkout 分批本地提交；问题 8（发布策略、环境保护及发布门禁）延期。

## 已完成的修复

| 核查项 | 最终行为与验证 |
| --- | --- |
| F01：Cecil 类型程序集身份 | 可解析时使用定义程序集；解析失败保留引用 scope。SDK/ref-pack 引用与实际定义、未解析引用分别覆盖。 |
| F02：直接特性 | 统一为真实 CustomAttribute 记录；Reflection 每项标志只移除一个合成项，保留同类型真实 blob。Roslyn 源符号模拟实际编码，并恢复 in/ref readonly 的编译器真实标记；源、PE、Cecil、Reflection 和旧框架引用覆盖。 |
| F03：独立入口 !ref | 相邻 !ref 在根和嵌套纯类型入口报 AN2005；成员使用位保持合法。 |
| F04：Cecil 惰性读取 | 逐节点保护缓存键、基类和接口展开，恢复失败报 AN3004，保留已读取祖先及可判定分支。损坏零秩 TypeSpec 回归覆盖两个关系 API。 |
| F05：Reflection 裸 by-ref | 保留已知 ByReference，只把精确 RefKind 标为不可用；成员映射保留精确分类。 |
| F06：指针文本 | Reflection 使用规范的嵌套类型分隔符；三后端 regex 一致。无 arity 后缀的 Cecil 泛型指针保留实参。 |
| F07：开放 Nullable | nullable 写法使用有效开放形参，与显式 System.Nullable 模式一致。 |
| F08：TryGet nullable 流 | 两个 out 参数标注 NotNullWhen(true)，兼容 netstandard2.0；包消费者在 nullable + warnings-as-errors 下直接读取成功结果的 Options。 |
| F09：无后缀泛型 arity | Reflection 从实际参数数扣除外层扁平数，补充自身元数；包含类型和无后缀元数据覆盖。 |
| F10：读取失败诊断 | Reflection 读取失败改用 AN3004；没有原生关联继续 AN3001。 |
| F11：数组声明特性 | 数组不继承元素的直接特性，成员及直接类型查询均覆盖。 |
| F12：Unknown 早退 | 未知祖先不掩盖后续已知匹配；合法 I<int*[]> / I<int[]> fixture 覆盖关系 API 与 DSL。 |

另落实以下已批准合同及工程修复：

- 方法/属性参数必须按声明顺序排列，Position 等于列表索引；构造器拒绝乱序、重复、缺号和 null，不自动重排。公共文档解释类型参数扁平 Position 与真实声明 OwnerId。
- 连续 nullable 后缀报 AN2010；分隔开的 nullable 层级保持合法。标识符采用既有 C# 分类集合，补上 Nl，不引入 UAX 扩展或转义名称。
- 三后端类型图、包含身份和规范文本使用显式工作栈；旧 CLR 的 IntPtr 歧义遍历同样迭代。Reflection 在单次映射中保活命中及新建的弱缓存依赖，结束后不永久保留。
- 隔离进程以 256 KiB 线程栈覆盖 1,024 层数组/指针、Cecil/Roslyn 256 层泛型、Reflection 128 层泛型、512 层包含类型、1,024 层 Cecil 定义形参 owner、8,192 层 Cecil 引用形参 owner 和旧 CLR 歧义遍历。后续审查复现了 Cecil 原生 Scope/FullName getter 在深层 owner 上的栈溢出，已替换为迭代计算；七类浅层 owner 保持原生文本，混合模块引用保留自身 Module 和缓存隔离。映射不新增硬深度预算，Match 原有 AN3005/256 限制保留。
- 明确 distinct-binding 环境可能按 2^n 增长，补不同变量键不合并和八组分支的分配/结果回归。运行时绑定数量、时间和分配预算仍延期；测试阈值不是产品预算。
- 修复属性 async guard、空 tasklike 的边界恢复、缺成员分隔符的级联诊断。async.Result、async* 等普通类型模式和 async 成员名保持合法；后续 Boolean 选择器保留。
- Review 特性进入快照，并验证四类真实特性的 target 与 parameter index。Reflection 缺依赖测试分别证明 GetParameters 原生加载失败和 detached 输入 AN3001，删除跨后端 fallback 的虚假覆盖。
- 包消费脚本保留元数据名称中的反引号，并用 char 96 独立比较实际 OwnerId。主 CI 默认依赖版本取自 CPM，兼容矩阵仍显式覆盖；README 说明 Windows 完整矩阵与非 Windows 子集。
- 修正测试主题/结果名称、重复规范章节编号和适配器 XML 文档；fuzz 不再吞掉没有公开取消入口的 OperationCanceledException。既有时间/GC 生命周期回归保留。

## 验证

| 检查 | 结果 |
| --- | --- |
| 默认完整 solution：restore、Release build、test | 1,379 项通过，0 失败、0 跳过；build 0 warning / 0 error。包括 net472 和现代宿主的 netstandard 资产腿。总数由日志自动求和。 |
| dotnet format --verify-no-changes --no-restore | 通过；工具报告 workspace loading warning，没有格式违规。 |
| Cecil 0.11.6 单独兼容腿 | 130 项通过，含最终 owner 文本与模块上下文修复。 |
| Roslyn 5.6.0 单独兼容腿 | 22 项通过。 |
| Cecil 0.11.6 + Roslyn 5.6.0 跨适配器腿 | 179 项通过，含全部十五个深度探针。 |
| 本地五包验证 | 最终 owner 修复后重新生成五包；两个依赖组、DLL/XML/PDB/包元数据通过；十个 net472/net10.0 消费程序构建及运行通过。 |
| 独立只读审查 | 五批及 owner 补充修复均复审通过；已修复审查中发现的无后缀泛型文本、旧 CLR/原生 owner 递归、模块上下文及 async 合法名称误拦截。 |

复现命令见 [执行计划](../plans/2026-10-09-final-review-remediation.md)。红/绿及最终输出保存在仓库 ignored 的 `artifacts/final-review-remediation/`。替代版本验证后先恢复默认 restore/build，再进行包验证，避免共享 bin/obj 版本污染。

## 延期与提交

问题 8 暂不讨论，release 工作流、GitHub 环境保护和发布策略均未修改。Reflection 闭合 owner 保留开放声明约束继续遵循此前已接受边界；本轮不承诺新增上下文能力。运行时绑定资源预算也按批准方案延期。

分批提交：`7001d9f`（核心合同）、`3e41036`（身份/读取/关系）、`2749447`（特性/Reflection）、`314c42e`（迭代映射）、`807324e`（Cecil 深层 owner 元数据），以及本记录所在的覆盖/工程收尾提交。全部为本地提交；未推送或发布包。原三份 review、核查报告与既有 untracked 文件保留。
