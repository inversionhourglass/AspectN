# 发布版本规则

五个包共用根目录 [Directory.Build.props](../Directory.Build.props) 中唯一、无条件的 `VersionPrefix`，当前为 `1.0.0`。基础版本使用不含前导零的三段数字；更改它后再推送对应发布引用。

| 入口 | 示例 | 生成的包版本 |
| --- | --- | --- |
| 推送正式 tag | `v1.0.0` | `1.0.0`，tag 必须与基础版本完全一致 |
| 推送 `prerelease` 分支 | 基础版本为 `1.0.0` | `1.0.0-preview-1792779709513`，数字仅为示例 |

预发布后缀使用 UTC Unix 毫秒时间戳。每次工作流在还原前调用一次 [get-release-version.ps1](get-release-version.ps1)，产生基础版本、后缀及完整版本。还原、构建、测试、五个包的打包和后续校验共用这份输出；内部的 `AspectN` 包依赖也使用同一个完整版本。重新执行版本计算步骤会生成新的预发布时间戳；仅重试后续签名发布任务时复用原制品版本。时间戳不承诺跨机器或任意并发操作的全局唯一性。

正式 tag 不接受预发布后缀或 `+build` 元数据，例如 `v1.0.0-rc.1`、`v1.0.0+build.42` 均会失败。需要预发布时使用 `prerelease` 分支；版本不符的正式 tag 在还原前失败。工作流没有手动输入完整版本的覆盖入口。

## 验证与发布流程

[Release](../.github/workflows/release.yml) 的两种入口共用完整构建和测试、包内容及消费者校验、未签名摘要、下载后的摘要核对、签名验证及发布步骤。发布任务继续使用 `nuget-release` 受保护环境及其中的 NuGet 和签名凭据。仓库若配置环境允许部署的分支/tag，须允许预期的 `prerelease` 分支和正式 tag；自动触发仍受环境保护规则约束。

[CI](../.github/workflows/ci.yml) 与 [Compatibility](../.github/workflows/compatibility.yml) 保持 `dev`、`main` 的 push、目标为这两个分支的 PR 及手动验证。推送 `dev` 会验证开发内容，推送 `prerelease` 会进入预发布流程。

可以在本地只计算版本而不生成或发布程序包：

```powershell
./eng/get-release-version.ps1 -Mode Release -Tag v1.0.0
./eng/get-release-version.ps1 -Mode Preview
```

输出为 JSON，字段为 `VersionPrefix`、`VersionSuffix`、`Version`。解析不会修改基础配置。工程回归入口：

```powershell
pwsh -NoProfile -File eng/tests/get-release-version.Tests.ps1
pwsh -NoProfile -File eng/tests/get-release-version.Tests.ps1 -PowerShellHost powershell
```

第二条需 Windows PowerShell。脚本使用 UTF-8 BOM，防止该宿主误读中文注释。本地验证范围、已知消费者阻塞及未执行的远端步骤见 [O-02 处置记录](../docs/reviews/2026-10-24-o02-release-version-policy.md)。
