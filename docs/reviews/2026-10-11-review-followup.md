# REVIEW.md 核查后的修复与裁决

日期：2026-10-11。基准：`9309bcc`。原始 `REVIEW.md` 保留为未跟踪文件；本文件记录核查后的结论，取代原报告中“全部 21 项成立、H1/M5 必须阻塞合并”的建议。

## 维护者裁决

- **H1：维护者决定不修复，不作为合并阻塞项。** Mapper 要求调用方提供结构完整的 Cecil 类型对象；缺少 ReturnType、ElementType 等必要信息的对象是无效输入，保留抛异常。不增加容错，不转换为 Unsupported/Unknown，本轮不改善异常信息。现有合成对象证据不足以支持 High 定级；后续提供真实程序集复现再重新评估。
- **L7：保留构造器的形状匹配能力。** `ctor(tasklike(..))`、`cctor(tasklike<int>)` 使用 Task/ValueTask 形状；普通成员路径中的 `tasklike` 是字面声明类型名。构造器要匹配名为 tasklike 的类型时使用限定名，如 `Example.tasklike`。
- **L8：`!ref` 严格要求邻接。** `! ref`、包含制表符或换行的拼写在成员类型槽中报 AN2005。
- **M1/L2：已独立处理。** 对应提交 `07fea5c`、`9309bcc`，本轮不重复实施。

## 各条目处理结果

| 条目 | 核查结论与本轮处理 |
|---|---|
| H1 | 按维护者裁决保留异常；不是阻塞项。 |
| M1 | `07fea5c` 已修复独立类型模式中的嵌套 ref-kind 诊断。 |
| M2 | Cecil 的数组、指针、函数指针、Pinned/Sentinel 包装器直接类型属性为空；保留命名泛型实例读取定义属性的能力。报告中 Reflection/Roslyn 同路径返回 Unknown 的说法不准确。 |
| M3 | Roslyn 可恢复的符号读取失败统一报 AN3004；缺少原生关联仍报 AN3001。 |
| M4 | 游离泛型形参使用适配器当前模块提供 Object/ValueType 隐含根；不可解析的引用所有者保留 Unknown/AN3004，不解引用空模块。 |
| M5 | 原报告的“显式推送 snupkg 到 v3 index 必然 400”不成立。按建议简化为仅枚举 nupkg，避免重复上传符号包；两类包的签名和验证保持原有流程。 |
| L1 | 无效 `\c` 不再吞掉选择器结束括号和后续 OR；合法控制转义仍被完整扫描。 |
| L2 | `9309bcc` 已补齐泛型替换中的 Pinned/Sentinel 包装器。 |
| L3 | Roslyn 按队列节点处理可恢复失败，继续遍历已排入队列的兄弟分支；已知匹配优先于不完整分支，闭包保留已知祖先。 |
| L4 | Roslyn 闭包查询在入口检查取消，包括缺少原生关联的快速返回路径。 |
| L5 | Reflection 缺少原生关联时优先保留结构比较已经产生的未知诊断。 |
| L6 | TypeIdentity 哈希按包含顺序累计，移除每次调用的 Stack 分配；相等性合同保持一致。 |
| L7 | 保留现有构造器形状语义，补充正反例并澄清语言规范、设计决策和差异文档。 |
| L8 | 空白分隔的 `! ref` 报 AN2005；补充参数、返回、属性和构造器参数回归。 |
| L9 | 删除规范中已废弃的注册 predicate/hasBody 说法；Rougamo 在 AspectN 匹配后筛选可织入成员。 |
| L10 | Reflection 夹具显式包含 Public/NonPublic、Instance/Static 和 DeclaredOnly。 |
| L11 | Roslyn PE 夹具使用 MetadataImportOptions.All；新回归覆盖 private/internal 实例和静态方法。旧夹具会明确失败，原报告“无对应行”的描述不应解释为静默漏测。 |
| L12 | 返回属性、声明类型属性加入 Roslyn PE 对比。新断言实际暴露 readonly 返回 CA 遗漏，同时修复并验证真实 PE 记录有/无两种情况。 |
| L13 | 由 MSBuild GetTargetPath 的输出元数据定位探针，复制其可运行文件与依赖到测试输出；运行时从 AppContext.BaseDirectory 定位，不依赖父目录、配置名或 TFM 名。当前是一个 Theory 方法的 47 个用例。 |
| L14 | 发布 workflow 按 github.ref 设置 concurrency，cancel-in-progress 为 false。 |
| L15 | 保留有价值的短输入生成器边界测试；新增两个解析入口各 1,000 次的 129–1,024 UTF-16 代码单元 Unicode 输入测试，检查终止、保留文本长度和诊断范围。 |

NuGet 客户端支持显式符号包推送，也会在推送 nupkg 后自动上传同名 snupkg。简化发布枚举是减少重复操作，不是修复已证实的 400 故障。[NuGet 客户端实现](https://source.dot.net/NuGet.Protocol/Resources/PackageUpdateResource.cs.html)、[符号包文档](https://learn.microsoft.com/nuget/create-packages/symbol-packages-snupkg)。

Roslyn PE 读取器依据实际 IsReadOnlyAttribute 记录确定 readonly ref 分类，并从公开属性集合中过滤该标记；映射器据此恢复记录。真实 PE 的反例保留 modreq、去掉 CA，验证不会凭 modreq 生成属性。[Roslyn PEParameterSymbol 实现](https://github.com/dotnet/roslyn/blob/main/src/Compilers/CSharp/Portable/Symbols/Metadata/PE/PEParameterSymbol.cs)。

## 回归与验证

先增加回归，再修生产实现。已观察到的目标失败包括：Cecil 包装器属性泄漏与游离参数查询异常 6 项，Reflection 未知诊断丢失 1 项，Roslyn 兄弟分支/诊断/取消 6 项，语法恢复及邻接 5 项，哈希分配与系统性碰撞 2 项。非公开成员夹具先在 PE 查询失败，修复后又在 Reflection 查询失败；两处都由实际新回归确认。

旧哈希在 32 组反转包含链案例中全部碰撞，1,000 次暖机后的调用分配 72,000 字节；修复后的回归要求没有系统性碰撞且重复调用分配为零。哈希值不承诺跨进程或版本稳定。

把测试输出复制到全新的随机临时目录后，用以下方式验证探针运行：

```powershell
dotnet vstest <relocated-output>/AspectN.Conformance.Tests.dll --TestCaseFilter:FullyQualifiedName~MappingDepthTests
```

旧实现 47/47 失败；修复后 47/47 通过。

最终门禁：

```powershell
dotnet build AspectN.sln -c Release --no-restore
dotnet test AspectN.sln -c Release --no-build --no-restore -v minimal
dotnet format AspectN.sln --verify-no-changes --no-restore --verbosity quiet
git diff --check
```

构建：0 警告、0 错误。完整测试：**1,623 passed，0 failed，0 skipped**。

| 测试集 | 通过数 |
|---|---:|
| Core | 919 |
| Conformance | 263 |
| Cecil | 148 |
| Cecil.Fody | 150 |
| Reflection | 102 |
| Roslyn | 28 |
| NetFramework / net10.0 | 7 |
| NetFramework / net472 | 6 |

格式检查和 diff 检查通过。发布脚本仅做 PowerShell AST 解析与包枚举模拟；本轮未运行 GitHub 发布、证书签名或 NuGet push，也未修改相邻仓库。

独立审查：适配器、核心语言/哈希、覆盖与发布流程分范围检查，均为 CLEAR / APPROVE，无剩余阻塞项。审查发现的 readonly PE 属性遗漏已经修复，并由上述最新完整门禁验证。

## 本地提交批次

| 批次 | 提交 | 内容 |
|---|---|---|
| 1 | `40b4859` | 适配器查询、诊断、兄弟分支与取消合同。 |
| 2 | `5e30f23` | regex 边界、严格 !ref 邻接、构造器形状裁决与规范同步。 |
| 3 | `3dc0736` | 包含顺序敏感、无分配的 TypeIdentity 哈希。 |
| 4 | `5666ce8` | readonly PE 属性、非公开成员、PE 对比、可搬移探针与长输入 fuzz。 |
| 5 | 本文件随发布流程修改一并提交 | 按 tag 串行发布、nupkg 枚举简化和完整处理记录。 |

没有重复提交 M1/L2，也没有推送分支或发布包。
