# REVIEW-DS 复核修复记录

日期：2026-10-05。验证源码提交：`18483b72db198ace806757dbce957122eab17b53`。本文件是该提交的验证记录，后续文档提交不会改变被验证的源码。

修复遵循核查后的结论，不把原报告中的推测、历史快照或设计偏好直接视为缺陷。用户已经确定：完整补齐元数据可用性、完善数组形状模型、删除自定义匹配扩展、延期性能交付。其余确定问题已完成修复；下表列明后端边界和不采纳的断言。

## 提交与范围

| 提交 | 内容 |
| --- | --- |
| `70e9628` | 单独删除自定义匹配扩展及相关 API、语法、测试、文档。 |
| `b9b0d95` | 元数据可用性、读取失败诊断与三态传播；包含 AN3004。 |
| `33b3181` | 中立数组模型保留 SZ/non-SZ 形状及跨后端关系。 |
| `51858fc` | 记录上述验证，明确性能交付延期。 |
| `c2e6bc4` | 修复本次剩余的解析、比较、关系、映射、公共契约、测试与发布流程问题。 |
| `18483b7` | 补齐 canonical 格式化前的泛型元数预算，防止溢出和提前巨量分配。 |

在现有 checkout 工作。原始未跟踪 review 文件及 `nul` 已保留；没有修改相邻仓库、冻结 oracle 或 Rougamo 子模块。提交均为本地提交。

## 最终验证

两组都使用 FodyHelpers 6.8.1、Release、`Version=PackageVersion=1.0.0-review-validation`。默认组合为 Cecil 0.11.5 / Roslyn 4.12.0；替代组合为 Cecil 0.11.6 / Roslyn 5.6.0。

| 测试程序集 / 资产 | 默认组合 | 替代组合 |
| --- | ---: | ---: |
| AspectN.Tests / net10.0 | 727 | 727 |
| AspectN.Cecil.Tests / net10.0 | 116 | 116 |
| AspectN.Cecil.Fody.Tests / net10.0 | 118 | 118 |
| AspectN.Reflection.Tests / net10.0 | 82 | 82 |
| AspectN.Roslyn.Tests / net10.0 | 19 | 19 |
| AspectN.Conformance.Tests / net10.0 | 143 | 143 |
| AspectN.NetFramework.Tests / net472，引用 netstandard2.0 资产 | 6 | 6 |
| AspectN.NetFramework.Tests / net10.0，引用 netstandard2.0 资产 | 7 | 7 |
| **合计** | **1,218** | **1,218** |

两组均零失败、零跳过；完整 solution 构建均零警告、零错误。格式验证退出码 0；工作区加载仍打印一条一般警告，没有格式差异。

验证命令（PowerShell；替代组合替换前两个属性后重新执行）：

```powershell
$properties = @('-p:CecilVersion=0.11.5', '-p:RoslynVersion=4.12.0',
    '-p:FodyHelpersVersion=6.8.1', '-p:Version=1.0.0-review-validation',
    '-p:PackageVersion=1.0.0-review-validation')
dotnet restore AspectN.sln @properties
dotnet build AspectN.sln -c Release --no-restore @properties
dotnet test AspectN.sln -c Release --no-build --no-restore --logger trx --results-directory artifacts/review-ds-final @properties
dotnet format AspectN.sln --verify-no-changes --no-restore
powershell -NoProfile -File eng/verify-packages.ps1 -PackageVersion 1.0.0-review-validation -CecilVersion 0.11.5 -RoslynVersion 4.12.0 -FodyHelpersVersion 6.8.1
powershell -NoProfile -File eng/verify-function-pointer-fallback.ps1 -RuntimeVersion 7.0.20
powershell -NoProfile -File eng/verify-function-pointer-fallback.ps1 -RuntimeVersion 10.0.12
```

两组实际打包都通过五个 nupkg、五个 snupkg、netstandard2.0/net10.0 两套 DLL/XML/PDB、两套精确依赖组及十个 net472/net10.0 消费者的 restore/build/run。另按 release 的 `--no-build --no-restore` 流程复用已验证二进制生成五包，再用 `-ValidateExisting` 验证实际最终产物及十个消费者。删除复制包的 netstandard 依赖组后，校验器以退出码 1 拒绝该包。

Reflection 的 netstandard2.0 资产已分别在 .NET 7.0.20 和 .NET 10.0.12 验证：旧运行时启用物理声明身份回退，现代运行时使用调用约定事实。LF、UTF-8 无 BOM 的 checksum 文件经本机 GNU `sha256sum -c` 校验通过。三个工作流通过 YAML 结构解析，两个工程脚本通过 PowerShell 语法解析；本次没有运行远端 GitHub Actions、签名或发布。

本地运行日志和 TRX 位于 `artifacts/review-ds-verification/final/`；按 `alternate-test.log` / `default-test.log` 中列出的八份 TRX 核对本轮结果，不混入较早中断运行的文件。该目录被 gitignore；可移植证据为上述命令、入库回归测试、此矩阵及源码 SHA，不能仅凭本地包文件存在声称验证成功。

历史计数保留其历史语义：`084dfb6` 的 952 项与核查基线 `79fa54b` 的 981 项之间新增了 29 项测试。952 也曾用于审计语料数量；两个独立来源不能据此推定误抄。历史文档已经补基线与当前验证入口。

## 严重和重要条目

| 编号 | 结论与处理 |
| --- | --- |
| C1 | 已修复。解析递归入口 128、语法树深度 256，超限 AN1002 并禁止绑定；候选结构深度 256，超限 Unknown/AN3005。语法树检查覆盖左深 Boolean 链、后缀、泛型及属性类型。结构比较使用显式栈和引用对去重；共享图不会重复展开每条路径。canonical 正则格式化追加前检查 65,536 字符预算、取消和泛型元数，超限 Unknown/AN3001。100,000 层比较、40 层共享图和隔离进程输入均已验证。 |
| I1 | 前批已修复。AN3002 仅表示 InvalidRegex，读取解析失败为 AN3004；回归覆盖适配器失败路径。 |
| I2 | 已修复三个后端的泛型约束遍历：隐含 object/ValueType、class/struct/unmanaged、接口和类型参数传递约束，闭包去重。Cecil 从 MethodReference/TypeReference 声明恢复约束并代入封闭上下文；失败保留 Unknown，同时已知 object 等关系仍可确定。Reflection 原生返回开放声明的边界见下文。 |
| I3 | 已修复 tuple 三态。零元 ValueTuple、具体非法 TRest 确定 NotMatched；未知/不完整 TRest 才 Unknown，否定遵循三态规则；零元 TRest 可终止长 tuple。 |
| I4 | 前批已修复。ArrayTypeSignature 明确保留 CLI 形状，三后端结构恒等、直接关系与合成目标统一；Standard 数组语法继续按 rank 匹配。 |
| I5 | 前批已修复预期元数据读取异常与可用性传播。参数错误、取消和致命异常不被笼统吞掉；Method/Property/Attribute 读取路径有回归。 |
| I6 | 按用户决定完整修复。需要不可用事实时返回 Unknown；名字等已知事实仍能给出确定结果，已知否定优先。未读取的属性集合不会被当作已读取的空表。 |
| I7 | 文档证据已修正。保留历史计数与 SHA；本轮矩阵绑定 `18483b7`。不采纳“952 很可能误抄”的推断。 |
| I8 | 已加强实际断言：捕获嵌套泛型目标形状、真实后端正反例、编译失败必须失败、Mapper 非空及实际映射、tasklike 对 async/non-async 同形返回的一致性、TypeAttributes 和 API 边界。扩展注册表相关测试随功能删除。 |
| I9 | 已修复 Roslyn 泛型 owner 的 dynamic 递归 canonical 化为 System.Object，并直接比较 OwnerId/OwnerAssembly/Position。MapType 本身原本已有归一化，不重复实现。 |

## 次要条目

| 编号 | 结论与处理 |
| --- | --- |
| M1 | 修复 selector/独立入口的 tuple 空项、尾逗号恢复差异；测试保留真实右括号及后续 OR。没有为修恢复而大范围重写文法。 |
| M2 | 自定义匹配扩展已整体删除，扩展尾逗号路径随之消除。 |
| M3 | 第一次 `/` 后拒绝 `.`/`..` 作为嵌套类型分隔符，独立和成员入口均有定位回归。 |
| M4 | 纯 TypePattern 入口统一给出不接受 ref kind 的 AN2005；成员返回/参数 TypeUse 保留既定语义。 |
| M5 | 不扩张独立 TypePattern API。其候选是 TypeSignature，没有 ref 使用位置；文档已说明入口差异。 |
| M6 | 实现 u 四位、x 一至四位、U 八位 Unicode 转义及长度/范围诊断与恢复。 |
| M7 | Reset 明确为前瞻 seek，保留累计诊断；未发现所述污染主词法器的可达路径。 |
| M8 | 整数词法使用 ASCII 数字，与解析和规范一致。 |
| M9 | 反斜杠后真实换行停止字符串扫描并诊断，不再跨物理行继续吞输入。 |
| M10 | 规范明确括号仅作 Boolean 分组、tuple 至少两项，`tasklike<System.Void>` 与 void 一致；保留已确认语义。 |
| M11 | 已修复现代和 netstandard 反射桥的 modified parameter/function-pointer conventions 读取。managed/unmanaged、cdecl/stdcall 及额外约定统一 canonical；旧运行时用 MVID/token 后缀区分声明，不伪造 managed。 |
| M12 | IsAsync 统一表示编译器异步方法，包括迭代器；Roslyn source 和 PE 标记、Cecil、Reflection 四视图有回归。tasklike 仍只匹配返回形状。 |
| M13 | 入库 emitted IL 证明 Roslyn PE 没有 RaiseMethod 事实时仍是 OrdinaryMethod，Cecil/Reflection 为 EventRaiser。明确后端边界，禁止按名字猜。 |
| M14 | 原方向断言已驳回；vector 可赋给 non-SZ，逆向不成立，不做反向“修复”。 |
| M15 | 指针显示统一为中立 canonical 名字；普通指针/函数指针仍属于 Standard 不支持的结构匹配形态，返回 Unknown。 |
| M16 | emitted IL 记录无 C# 编译器标记的实例运算符：Roslyn 4.12 为 OrdinaryMethod，5.6 为 Operator；Cecil/Reflection 为 Operator。按版本测试和文档化，不扩大外语支持承诺。 |
| M17 | 随 I4 完善数组模型。 |
| M18 | GenericParameterKey 纳入 OwnerAssembly；同声明文字、不同程序集的 key 不会相等。 |
| M19 | execution 共用 callable 节点并保留 KeywordToken 是有意设计。 |
| M20 | 重写识别出的乱码摘要及关键公共契约 XML 文档，明确语法、不可用事实、能力和异步字段语义。 |
| M21 | 扩展 API 已删除，无剩余 builder 参数名问题。 |
| M22 | 显式公共参数守卫，ArgumentNullException.ParamName 为实际参数名；原 NRE 判断不成立。 |
| M23 | 校验 AttributeSignature.Target/ParameterIndex：参数目标要求非负 index，其他目标禁止 index；保留有用公共字段。 |
| M24 | InheritanceClosure 对 ancestors 做只读快照，failure 只接受 Unknown 或 null；外部列表修改不影响闭包。 |
| M25 | AN4003 公共常量改名 CompoundAccessibilityMigration，并更新 API 基线和测试。 |
| M26 | 扩展域及 enum 强转路径随扩展删除。 |
| M27 | 没有数值范围契约或依赖证据，不重编号 SyntaxKind。 |
| M28 | 公共诊断只对 Code/Severity/Span/Message 完全一致项去重，按 Span.Start、Code、Severity、Span.Length、Message 稳定排序；单次内部 bag 不改为并发容器。 |
| M29 | 保留公共 severity 的合法保留值。 |
| M30 | 与 C1 一并修复极深比较和共享图遍历；不为接口形状偏好增加公开 CancellationToken 参数。 |
| M31 | 缺少声明位置映射时 regex 返回 Unknown/AN3001，不回退源参数名；新增不完整外部签名回归。 |
| M32 | 明确 Regex.IsMatch 子串语义；完整匹配用 ^...$。 |
| M33 | 用户已决定删除自定义匹配扩展，插件异常/并发策略不再需要拍板。 |
| M34 | 扩展注册表缓存隔离机制随功能删除，不改成内容 hash。 |
| M35 | 删除恒 false 的 ignoreCase 死参数；最坏 glob 性能与线性算法证明仍属于延期的性能工作。 |
| M36 | 同程序集命名空间互用不是程序集循环依赖；保留 MatchStatus 位置。 |
| M37 | 删除没有消费者的 Reflection 反向 method/property 缓存及 TryGetMember；保留实际使用的正向和弱 type 缓存。 |
| M38 | Reflection OriginalDefinition 原本已填充，假声明断言驳回。文档明确能力为后端事实能力，不阻止脱离 native 的结构自匹配。 |
| M39 | 打包公共属性按 src 目录在 Directory.Build.props 早期求值，删除五项目重复配置；实际包验证 DLL/XML/PDB。不能晚到 targets 后才设置文档属性。 |
| M40 | 删除仅 net10.0 项目的恒真预处理守卫。 |
| M41 | API 泄漏守卫补字段、事件、接口及泛型约束；原有核心无后端程序集引用守卫保留。 |
| M42 | 一致性快照加入 TypeAttributes；真实类型/方法/返回/参数特性及 type/any/wildcard/组合选择器覆盖四视图。 |
| M43 | corpus kind 明确路由，未知值立即抛带 case identity 的错误，不再默默当作 method。 |
| M44 | 与 I7 合并，历史 SHA 与当前验证入口明确区分。 |
| M45 | release 在签名前验证完整 solution，含 net472 和现代运行时的 netstandard 资产；两资产 TRX 分开命名。 |
| M46 | unsigned/signed checksum 使用 LF、UTF-8 无 BOM，GNU 校验通过。 |
| M47 | release/CI 的依赖属性贯穿 restore/build/test/pack；发布版本在构建前确定；final pack 不重建，实际 release 产物用 ValidateExisting 检查依赖组和消费者。 |
| M48 | 同步本次明确的实现限制、诊断顺序、regex 和入口规则；历史比较基线不伪装成当前验证 SHA。 |
| M49 | AGENTS 未要求每条提交正文，原“违反规则”断言不成立；不修改既有历史。 |
| M50 | 迁移指南补当前 MethodSignature/MethodKind/MapMethod/ForMethod 和 OwnerAssembly 构造示例，以及可用性、数组模型和入口差异。 |
| M51 | 依用户决定延期性能交付；此前已移除暂未使用 BenchmarkDotNet pin，修订计划，不宣称工业性能验收完成。 |
| M52 | net472 真实覆盖 Cecil/Roslyn/Reflection、关系和数组；现代 net10 明确加载 netstandard 资产覆盖反射 bridge；另有 .NET 7 声明身份回退探针。 |
| M53 | Dependabot 只提受控 PR，不自动破坏矩阵；不加入未经需求支持的 ignore 策略。 |
| M54 | AGENTS 记录 src/Shared 为仅供适配器编译包含的内部共享源码，不进入 core 公共 API。 |
| M55 | 精简 .zcodeignore 为生成物/本地敏感配置规则；历史机器路径标明历史语境。按仓库要求保留未跟踪文件。当前证据和复现命令入库，不以 ignored artifacts 作为唯一交付。 |

## 明确的后端边界与延期项

Reflection 在封闭声明类型上取得方法泛型参数时，CLR 仍可能返回开放 DeclaringMethod，无法从该 Type 还原封闭上下文；测试保留可观测的开放约束链，不虚构 string 等封闭参数。

旧 CLR 缺少函数指针形状 API 时，原生 Type 可能表现为 IntPtr；声明身份回退可防止不同方法 owner 坍缩，但不能从该 Type 恢复完整函数指针类型。函数指针本身仍不属于 Standard 可表达形态。Roslyn PE raise 和外语实例运算符分类边界由 emitted IL 与版本回归固定，详见 [后端边界](../language/shared-standard-differences.md)。

性能基准、glob 最坏复杂度优化、工业性能门仍延期。没有新增需要用户决定的产品取舍；以上边界保持已有支持范围。
