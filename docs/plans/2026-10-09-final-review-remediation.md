# 最终审查修复实施计划

> **面向 Claude 的原执行说明：** 必须使用 superpowers:executing-plans，逐项执行本计划。

**目标：** 修复已核实发现，实施维护者批准的契约，保留回归证据并分批本地提交。

**架构：** 保留 Standard 及中立公共模型。归一已解析类型身份，只公开真实自定义特性，强制参数声明顺序，迭代映射原生类型图，不引入新深度或绑定预算。

**技术栈：** C#、.NET 10/netstandard2.0/net472、xUnit/FsCheck、Mono.Cecil、Roslyn、Reflection、PowerShell。

## 已批准设计与范围

维护者于 2026-10-09 批准建议方向、实施及任务级本地提交。在现有检出工作。保留全部未跟踪文件，不暂存四份审查报告或 nul。不推送、发布、修改相邻仓库，也不改变发布/环境批准策略。

- attr 消费真实 CustomAttribute 项。互操作标志及运行时伪特性一致排除，Roslyn 源码符号也如此；数组不继承元素特性。
- 有名类型可解析时使用定义程序集身份，否则保留引用程序集身份。
- 方法/属性参数列表按声明顺序，Position == index；拒绝损坏列表，不默默重排。
- 连续可空后缀无效，合法嵌套可空使用仍支持。
- 标识符采用 C# Unicode 类别范围，含 Nl；不扩充 UAX #31 Other_ID_Start 或转义名语法。
- 绑定备选预算仍后置，保留有意义有界回归，明确说明不同绑定的增长。
- 适配器映射使用显式工作栈，保留缓存/原生关联及生命周期契约，不添加新原生形状上限。
- Reflection 此前批准的开放约束上下文边界不变。

## 批次 1：核心语言及公共契约

文件：`src/AspectN/Parsing/TypePatternParser.cs`、Lexer.cs、SelectorParser.cs、`src/AspectN/Binding/Binder.cs`、`src/AspectN/Evaluation/TypePatternEvaluator.cs`、`src/AspectN/Metadata/MethodSignature.cs`、PropertySignature.cs、GenericParameterSignature.cs、`src/AspectN/AspectNCompilation.cs`，相关核心测试/公共 API 基线及语言规范。

1. 添加独立 !ref、重复可空后缀、Nl 标识符、开放 Nullable 相等、无效参数位置、可空 TryGet 消费回归。聚焦运行并记录预期失败。
2. 补解析保护，以 AN2010 拒绝直接嵌套可空后缀节点；识别 C# Unicode 类别，使用有效 Nullable 实参，核验复制参数列表，标注 TryGet 成功结果，说明展开的泛型槽契约。
3. 核心测试、解决方案构建、格式及独立审查，解决可执行发现。
4. 提交 fix(core): enforce language and signature contracts。

## 批次 2：Cecil 身份、元数据失败与关系查询

文件：`src/AspectN.Cecil/Internal/CecilTypeMapper.cs`、CecilResolver.cs、`src/AspectN.Cecil/CecilSignatureMapper.cs`、`src/AspectN.Reflection/ReflectionTypeSystem.cs`，Cecil/Reflection/一致性测试及身份/可用性文档。

1. 添加普通 SDK/ref-pack 引用身份、损坏 InterfaceImpl TypeSpec、数组元素特性隔离及合法 `I<int*[]>`/`I<int[]>` 关系回归，先确认失败。
2. 归一可解析程序集身份，节点展开保护附可恢复失败诊断，隔离数组声明特性；全部已知关系分支处理后才决定 Unknown。
3. Cecil、链接 Fody、Reflection、一致性测试，格式及独立审查。
4. 提交 fix(adapters): preserve neutral identities and relation evidence。

## 批次 3：真实特性与 Reflection 元数据

文件：Reflection/Roslyn 映射器、必要的适配器共享特性策略、Reflection 类型系统、相关测试/测试程序集及特性/可用性文档。

1. 添加源码/PE/Cecil/Reflection 的 out/in/optional、显式伪特性、普通标记、嵌套指针、无后缀元数名、脱离关联引用可用性一致性测试，确认红。
2. 一致过滤元数据标志伪特性但保留真实记录；保留引用形状不猜分类；规范化指针、恢复自身元数，读取失败用 AN3004。
3. 适配器/一致性测试、netstandard 构建、格式及独立审查。
4. 提交 fix(adapters): align custom attributes and reflection metadata。

## 批次 4：迭代原生类型映射

文件：三个签名映射器、必要原生缓存/键、必要 src/Shared 工作栈支持、深度及生命周期测试。

1. 对深数组/泛型/包含链添加回归，可能栈溢出杀宿主时用隔离进程。保留普通嵌套身份、共享图缓存复用、可回收/模块生命周期测试。
2. 用显式依赖栈替代递归形状/包含身份映射，保留后端归一、稳定引用、Reflection 弱缓存和锁；确认缓存键不会重引入递归。
3. 运行映射/生命周期/Fody/一致性及独立审查。确认深映射成功，已有 Match 保护仍返回 AN3005。
4. 提交 refactor(adapters): map native type graphs iteratively。

## 批次 5：覆盖及工程跟进

文件：一致性缺失祖先测试/辅助方法、特性快照过滤、绑定备选测试、eng/verify-packages.ps1、.github/workflows/ci.yml、README、语言/资源文档，以及小型已核实测试/规范命名修正。

1. 纠正误导性 Reflection 回退覆盖和空 Review 特性快照，测试区分原生输入与脱离关联输入，并验证真实字段。
2. 修正程序包使用方字面反引号，断言预期声明者 ID，执行 TryGet 保护消费。主 CI 默认版本来自 CPM，保留显式替代矩阵，不改发布批准/闸门。
3. 说明 Windows 完整矩阵、不同绑定资源增长、映射保护范围及批准契约；修正规范重复编号和明显错误测试名称。
4. 完整还原/构建/测试/格式、两个支持依赖分支、本地包/使用方及独立最终审查。
5. 提交 test: close review coverage and consumer validation gaps；更清楚时将文档/CI 单独提交。

## 验证与执行证据

红/绿及审查证据保存到忽略目录 artifacts/final-review-remediation/，更新本计划完成节。每次提交精确指定路径。

最终命令：

```text
dotnet restore AspectN.sln
dotnet build AspectN.sln -c Release --no-restore
dotnet test AspectN.sln -c Release --no-build --no-restore
dotnet format AspectN.sln --verify-no-changes --no-restore
dotnet test tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj -c Release -p:CecilVersion=0.11.6
dotnet test tests/AspectN.Roslyn.Tests/AspectN.Roslyn.Tests.csproj -c Release -p:RoslynVersion=5.6.0
dotnet test tests/AspectN.Conformance.Tests/AspectN.Conformance.Tests.csproj -c Release -p:CecilVersion=0.11.6 -p:RoslynVersion=5.6.0
eng/verify-packages.ps1 -PackageDirectory artifacts/final-review-remediation/packages-after-owner
```

替代版本分支后，包验证前还原/构建默认解决方案，避免共享 bin/obj 产生误导版本冲突。

## 完成

- [x] 批次 1：核心契约。确认 21 个初始失败及 3 个嵌套 !ref 失败；完整 1,301 通过，Release 零警告/错误，格式通过（仅工作区加载提示）。独立只读审查通过，含 netstandard 可空使用方编译。
- [x] 批次 2：身份/元数据/关系。复现五项预期回归，补未解析作用域回退。完整 1,312 通过，Release 零警告/错误，独立审查通过。
- [x] 批次 3：特性/Reflection。复现初始六项失败；八项 CA/指针案例覆盖标志旁真实记录及旧框架内嵌标记。完整 1,322 通过，Release/格式通过；审查两项 P2 修复并复核通过。
- [x] 批次 4：迭代映射。小栈隔离进程复现五个深形状和两个深指针失败。十三个初始深映射/旧歧义案例和无后缀泛型指针回归通过，完整 1,337 通过。审查两项 P2 及弱缓存保活建议修复并复核。后续在 8,192 引用声明者层级复现 Scope/FullName 递归（1,024/2,048 通过），迭代作用域/原生声明者格式移除路径，保留引用局部模块回退。十五个深度探针及逐字节声明者文本/上下文检查覆盖补充。
- [x] 批次 5：覆盖/工程/最终闸门。修复前复现解析器/特性快照失败；缺失依赖 Reflection 覆盖明确区分原生创建失败与脱离关联能力缺失。默认完整 1,379 通过，零失败/跳过（八分支自动求和），Release 零警告/错误，格式仅工作区加载提示。Cecil 0.11.6/Roslyn 5.6.0 分别 130/22 通过，联合一致性 179（含十五个小栈探针）。最终源码包验证五包、两依赖组及十个 net472/net10.0 使用方构建/运行。独立审查及声明者模块补充复核通过。问题 8 仍后置，发布流程不变。
