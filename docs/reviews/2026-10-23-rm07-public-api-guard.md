# RM-07：集中维护公共 API 后端泄漏守卫

日期：2026-10-23。对应 K3-M12 与后续 RM-07。基准为 `056d40a3e08cd7c7633f35402376842c2209e756`。维护者批准统一三处扫描规则，保留程序集引用检查和原有覆盖，再删除重复扫描，不改生产代码或公共 API。

## 范围与合并方式

统一入口为 [PublicApiTests.CoreAssembly_PublicTypesDoNotExposeAdapterNamespaces](../../tests/AspectN.Tests/PublicApiTests.cs)。`Mono.Cecil`、`Microsoft.CodeAnalysis`、`System.Reflection` 的禁用清单和递归类型识别在这三处原守卫中合并为一份；基类、接口、字段、事件、构造参数、方法返回值/参数和泛型约束的既有扫描保持。

属性扫描保留原范围：所有核心公开类型自身声明的公开实例/静态属性，以及 `AspectN.Syntax` 类型的公开继承属性。后一部分原由 SyntaxTreeTests 扫描，现在通过统一入口的 `GetPublicProperties` 选择。其余类型继续使用 `DeclaredOnly`，没有将框架基类继承成员纳入新的禁止范围。

统一的 `UsesNamespace` 继续递归识别泛型参数、数组元素、引用和指针类型。语法模型继承属性也使用这套递归规则，不再仅检查外层类型全名开头。独立的 `CoreAssembly_DoesNotReferenceAdapterAssemblies` 原样保留。

删除 [SignatureTests](../../tests/AspectN.Tests/Metadata/SignatureTests.cs) 的 `CorePublicApi_DoesNotExposeNativeBackendTypes` 及其唯一使用的 `Flatten`，删除 [SyntaxTreeTests](../../tests/AspectN.Tests/Syntax/SyntaxTreeTests.cs) 的 `SyntaxModel_PublicSurface_DoesNotReferenceBackendAssemblies`。两类中的其他模型测试保持原样；不新增永久测试、后端依赖或公共辅助 API。

## 知识库与历史证据

关联 `CROSS-COMPAT-001/S1` 的核心公开 API 反射与程序集引用检查。该场景已经定位 PublicApiTests，迁移没有更改产品 API、语言、生命周期或元数据读取契约，也不修改能力范围、验证状态或 N/A，故无需改能力元数据、其依赖条目或用户章节。生成矩阵的状态继续保持。

EV-20261013-004、EV-20261014-001 的历史测试清单包含被删除的方法，保留名称、计数和源码快照哈希。当前重跑统一入口；历史清单不回填为新清单。本报告只验证当前扫描的范围迁移与指定类型形状，不证明所有可能的 CLR API 位置、任意类型形状或兼容运行时组合均已检查。

## 当前验证

修改前三类共 18 项通过，修改后 16 项通过，均为 0 失败、0 跳过。两个 TRX 的实例集合只减少上述两个已合并守卫，程序集引用检查、统一泄漏检查和其余模型用例均保留并通过。命令如下；修改前使用相同过滤器，结果文件与目录分别为 `rm07-before.trx`、`artifacts/rm07/before`：

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --filter 'FullyQualifiedName~AspectN.Tests.PublicApiTests|FullyQualifiedName~AspectN.Tests.Metadata.SignatureTests|FullyQualifiedName~AspectN.Tests.Syntax.SyntaxTreeTests' --logger 'trx;LogFileName=rm07-after.trx' --results-directory artifacts/rm07/after -v:minimal
```

临时控制台探针 `artifacts/rm07/probe` 直接调用统一守卫的私有辅助方法，不纳入提交。它逐一比较核心当前 102 个公开类型、415 个属性检查位置：统一选择结果与原有属性扫描范围的并集一致。合成对照确认语法命名空间包含继承属性、其他命名空间仍只取自身声明属性；六组类型识别对照覆盖直接 MethodInfo、泛型包装、泛型包装数组、ref、pointer 和普通字符串集合。均通过，见 `artifacts/rm07/probe.log`。这是有限范围探针，不新增生产契约。

`artifacts/rm07/verification.json` 记录源文件与修改前快照的精确差异、测试实例差集、TRX 哈希和历史证据不变检查。两个模型文件仅删除上述扫描与无用辅助方法；PublicApiTests 仅替换属性选择表达式并增加选择辅助方法，原其余扫描和引用检查保持。

三个测试源文件的 `dotnet format AspectN.sln --verify-no-changes --no-restore --include ... --verbosity minimal` 检查退出码为 0；工具有工作区加载告警，不将结果表述为加载零告警。`pwsh -NoProfile -File eng/verify-documentation.ps1 -AsJson` 返回 0 错误、0 告警；`pwsh -NoProfile -File eng/generate-knowledge.ps1 -Check` 和 `git diff --check` 通过。

独立只读复核通过，无需修复项。复核者确认主守卫仅迁入语法模型继承属性选择，其他扫描和独立引用检查保持，两个模型文件仅删除重复扫描及无用辅助方法；独立读取 TRX 确认 18→16 且仅减少两个已合并守卫，历史两份证据保持，并核对探针与报告的验证边界。

既有 README 修改、原始 review、综合报告及 `nul` 保留；既有未跟踪的后续核对清单仅更新进度，继续未跟踪。不推送、不发布、不修改相邻仓库。
