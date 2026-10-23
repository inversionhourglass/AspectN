# RM-05：Cecil 损坏元数据测试夹具的布局守卫

日期：2026-10-23。对应 K3-M9 与后续 RM-05。基准为 `c54c3240401d610e6b0621cb31cc86b6e9c34341`。维护者批准保留现有测试，补充布局说明、明确断言和实际 Cecil 版本，不新增生产兼容行为。

## 问题与变更范围

两个测试通过反射读取 Cecil 的 `Image`、`TableHeap`、`BlobHeap`、`Tables`、`data` 等内部字段，修改延迟读取的数据。原辅助方法在 `GetField` 后直接使用空值抑制；临时探针调用旧方法并请求缺失字段时，实际得到 `NullReferenceException`，没有字段路径或 Cecil 版本信息。这是已复现的夹具报错问题，不代表已经验证某次依赖升级失败。

新增测试内部共享类 [CecilDamagedMetadataFixture](../../tests/AspectN.Cecil.Tests/CecilDamagedMetadataFixture.cs)，两个用例委托给该类。Fody 测试项目通过既有源码链接规则编译同一文件，不改项目或生产 API：

- 字段存在且值非空；可见类型分别验证为 `Array`、`byte[]`、`uint`。
- 表数组为零起一维数组，目标索引存在；当前两个小型夹具各有一条目标记录，行大小分别为 6 和 2。
- 在转换偏移和读写字节前检查缓冲区范围。
- 先验证 getter 语义字节和 `I<int>` 的原始 blob，再写入与原用例完全相同的损坏字节。
- 断言失败包含完整字段路径、实际所属类型或布局值，以及实际加载程序集的完整身份；要求重新核验夹具布局。

注释记录基准为 Mono.Cecil 0.11.5 的内部布局。当前普通 Cecil 和 Fody 载体均报告版本 `0.11.5.0`，公钥标记分别为 `50cebf1cceb9d05e`、`1ca091877d12ca03`。这只是本次验证的两个载体，不是版本准入白名单；没有新增版本相等检查，没有限制 `CecilVersion` 覆盖，也不宣称未来版本必然失败或均可使用。

## 契约、知识库与历史证据

涉及 `CROSS-AVAILABILITY-001/S2、S5` 关联的普通 Cecil/Fody 局部读取恢复测试，以及 `CROSS-INPUT-001` 的输入边界说明。新增守卫只判定夹具是否仍能正确注入指定故障；原有测试名称、输入构建、损坏字节和针对 AspectN 结果的断言保持。未新增、删除测试用例，未改变正常程序集处理或 U01 对必要方法签名损坏的既有裁决。

知识库现有条件继续定位原测试类；本次没有改变支持范围、场景状态或 N/A，故不修改能力元数据与生成矩阵。用户指南的调用方式和失败契约不变，无需同步用户章节。历史报告、快照与证据保留原文和哈希；本报告及下述重跑只证明指定夹具在当前两个载体下成立，不替代完整适配器或版本兼容矩阵。

## 验证与边界

修改前普通 Cecil 的两类测试共 42 项通过。初次沙箱运行因 VSTest 无法连接 testhost，在 90 秒后中止；宿主权限下使用独立结果文件重跑通过，结果为 `artifacts/rm05/before/rm05-before-host.trx`。不将沙箱中止计为测试通过。

修改后使用以下聚焦过滤器，普通 Cecil 和 Fody 各 42 项通过，共 84 项，0 失败、0 跳过：

```powershell
dotnet test tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~CecilUnavailableMetadataTests|FullyQualifiedName~CecilResolvedMetadataTests' --logger 'trx;LogFileName=rm05-cecil.trx' --results-directory artifacts/rm05/cecil -v:minimal
dotnet test tests/AspectN.Cecil.Fody.Tests/AspectN.Cecil.Fody.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --filter 'FullyQualifiedName~CecilUnavailableMetadataTests|FullyQualifiedName~CecilResolvedMetadataTests' --logger 'trx;LogFileName=rm05-fody.trx' --results-directory artifacts/rm05/fody -v:minimal
```

普通 Cecil 已先由临时探针工程重新构建，随后运行 `--no-build`。TRX 分别保存在命令声明的目录中；原测试文件快照保存在 `artifacts/rm05/before`。

`artifacts/rm05/probe` 是仅用于本轮验证、未纳入提交的临时控制台工程。旧辅助方法的缺字段探针未达到明确断言要求，见 `probe-before.log`；新辅助类的缺字段、空字段、错误类型、短缓冲区、字节不符、过大偏移、表索引越界和表行结构不符共八类探针均得到带定位信息与实际 Cecil 程序集身份的 xUnit 断言，见 `probe-after.log`。这些失败注入探针运行于普通 Cecil 宿主，不声称覆盖任意内部布局变化。

`artifacts/rm05/verification.json` 记录两个原测试文件中针对 AspectN 的断言文本保持、修改前后源码哈希、三个 TRX 的计数和哈希、两个 Cecil 程序集身份，以及八类探针的通过计数。未用本次源码哈希替换历史证据中的快照哈希。

三个修改测试源文件的 `dotnet format AspectN.sln --verify-no-changes --no-restore --include ... --verbosity minimal` 检查退出码为 0；工具有工作区加载告警，不将该结果表述为加载零告警。`pwsh -NoProfile -File eng/verify-documentation.ps1 -AsJson` 返回 0 错误、0 告警；`pwsh -NoProfile -File eng/generate-knowledge.ps1 -Check` 和 `git diff --check` 通过。

独立只读复核通过，无需修复项；复核者确认两处损坏字节与基准一致，输入构建、测试名称及产品断言保持，全部索引/转换/字节访问具有前置守卫，Fody 源码链接覆盖新类，并独立核对三个 TRX、八类探针日志与实际程序集身份。本次修复没有版本白名单或生产兼容行为扩张。

既有 README 修改、原始 review、综合报告及 `nul` 保留；既有未跟踪的后续核对清单仅更新进度，继续未跟踪。不推送、不发布、不修改相邻仓库。
