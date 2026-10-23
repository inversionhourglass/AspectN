# 签名前的未签名包校验

`verify-unsigned-packages.ps1` 用于发布流程两个 job 之间的包交接：build job 生成 `SHA256SUMS.unsigned`，签名 job 下载产物后核对文件和摘要，通过后才读取证书并签名。脚本只读取输入，不删除清单、不修复包，也不执行签名或发布。

在仓库根目录运行：

```powershell
pwsh -NoProfile -File eng/verify-unsigned-packages.ps1 -PackageDirectory artifacts/release
```

`PackageDirectory` 是平面目录，相对路径以当前工作目录为基准。目录必须包含清单和清单列出的全部包，不能包含其他文件或子目录。清单和包必须是普通文件，不能是重解析点。

清单每行格式为 `64位十六进制SHA256 *包文件名`。文件名只接受字母、数字、下划线开头的平面 ASCII 名称，后续允许字母、数字、下划线、点、加号和连字符，以 `.nupkg` 或 `.snupkg` 结束；不接受目录、绝对路径或清单自身。哈希忽略大小写；文件名精确匹配，并拒绝仅大小写不同的重复记录，避免跨平台歧义。允许 LF 或 CRLF 和文件末尾的一个换行，不允许空清单、空行或其他格式。

成功时输出核对的包数量并退出 0。清单缺失、格式错误、重复记录、文件缺失、多余条目或内容 SHA256 不符都会以非零退出码终止。脚本本身不规定包数量；发布工作流原有的五个 nupkg、五个 snupkg 检查继续保留。

脚本使用 Windows PowerShell 5.1 可用的 API，并通过单文件的 UTF-8 BOM 编码规则保证中文注释可正确解码。发布工作流实际使用 `pwsh`。黑盒测试宿主需要 PowerShell 7：

```powershell
pwsh -NoProfile -File eng/tests/verify-unsigned-packages.Tests.ps1
# Windows 上可补测旧宿主。
pwsh -NoProfile -File eng/tests/verify-unsigned-packages.Tests.ps1 -VerifierHost powershell
```

测试在 `artifacts/unsigned-package-tests/<独立标识>/` 创建合成包，验证真实子进程的退出码、错误信息及运行前后输入快照。结果和输入保留在该目录，便于排查；合成文件无需是有效 NuGet 压缩包，因为这组测试只验证交接摘要。NuGet 内容、消费者和签名验证仍由既有发布步骤负责。

摘要校验用于发现交接文件集合或内容不一致，不提供独立来源认证；如果清单和包一起被替换，它不能据此鉴别。当前本地验证范围与未执行的远端发布环节见 [RM-10 处置记录](../docs/reviews/2026-10-23-rm10-unsigned-checksums.md)。
