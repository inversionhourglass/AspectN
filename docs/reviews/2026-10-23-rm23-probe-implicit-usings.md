# RM-23：临时探针显式启用 ImplicitUsings

日期：2026-10-23；对应GLM-M14；基准 `4bdb791df4ad0198b8f69c1fa49b83261fbee490`。维护者批准在临时探针工程显式启用隐式命名空间导入。

## 问题与处理

[探针脚本](../../eng/verify-function-pointer-fallback.ps1)生成的Program.cs使用System中的类型和System.Linq扩展方法，原临时工程没有声明ImplicitUsings。当前生成目录位于仓库内，合法继承根Directory.Build.props的enable设置，正常探针原本可以通过；本项是维护性改进，不把原报告已反证的当前编译失败重新认定为实际缺陷。

只在生成工程的PropertyGroup中增加一行 `<ImplicitUsings>enable</ImplicitUsings>`。目标框架、允许unsafe、编译文件集合、程序集引用、探针源码、运行时参数、断言和退出检查保持；根props、CI和产品源码均未修改。该属性只明确探针自己的命名空间导入需求，不表示临时工程完全独立于仓库其他构建设置。

## 当前验证

先构建当前核心和Reflection的netstandard2.0资产，再在修改前后分别执行真实脚本：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-function-pointer-fallback.ps1 -RuntimeVersion 7.0.20
```

两次均编译通过并输出 `Runtime 7.0.20: calling convention identity checks passed (fallback=True).`，实际覆盖原脚本的调用约定重载区分、声明回退及跨映射器身份检查。仅本地授权探针子进程使用ExecutionPolicy Bypass，未修改机器执行策略；直接以本机原策略启动曾被AuthorizationManager拒绝，该启动限制不记为产品或探针断言失败。

为验证导入配置的来源，修改前后对真实生成工程设置 `-p:ImportDirectoryBuildProps=false`，使用分别隔离的BaseIntermediateOutputPath和OutputPath构建，避免复用正常构建产生的global usings。修改前求值的ImplicitUsings为空、编译退出1，出现Where扩展方法、Console和System类型缺失的C#诊断；修改后求值为enable、编译退出0，隔离生成的Probe.dll在.NET 7.0.20上运行也通过且fallback=True。两次隔离求值的LangVersion均为11.0，未用语言版本参数掩盖差异。

修改前后生成的Program.cs及实际使用的两个产品DLL哈希相同；从修改后脚本移除新增一行，与Git基准脚本逐字节相同。命令、退出码、完整输出、源码和资产哈希见[数据记录](data/2026-10-23-rm23-probe-implicit-usings.json)，本地原始日志保留于artifacts/rm23。

资源构建首次退出1，仅显示0告警、0错误，未能据此确定原因；随后以单节点和诊断日志构建成功，0告警、0错误。本项没有为此修改构建配置，也不将首次异常解释为已定位并修复的问题。

## 能力与证据边界

新增工程盘点INV-0022，状态保持unassessed，不据一次探针运行新增产品能力验证承诺。关联范围为USE-SHAPES-001/S2的Reflection函数指针表示，以及CROSS-COMPAT-001的兼容资产主题；本次未改变产品实现、API、语言、能力场景状态、evidence_ids或N/A。现有证据继续保留其原范围；脚本配置对日常匹配调用无影响，使用指南无需行为修订。

实际执行环境仅Windows、已安装的.NET 7.0.20和本机SDK；没有运行Linux、远端Actions、完整解决方案矩阵、打包或消费者流程。现有Compatibility流程仍调用同一脚本，未新增CI步骤。RM-11保持现状，RM-13已记录的Roslyn/net472 System.Memory消费者阻塞未在本轮重验或处理。

## 收尾核验

独立只读审查未发现可操作问题，核对了Git基准与一行修改、原始结果与七条命令日志及哈希、独立obj/bin目录和实际生成的System/System.Linq global usings，以及能力与平台范围说明；未替代真实Linux或远端CI验证。

正式知识库生成、`eng/generate-knowledge.ps1 -Check`、`eng/verify-documentation.ps1 -AsJson`及`git diff --check`通过，文档0错误、0告警；catalog不变，support-matrix仅新增INV-0022盘点行。未跟踪后续清单同步为22项已处理、1项保留现状、1项Minor待确认；清单及其他既有WIP不纳入本项提交。
