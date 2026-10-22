# RI-02：fork 开发流程的 CI 触发策略

日期：2026-10-22。来源为综合审查 GLM-I7 / 后续核对 RI-02。当前基准为 `HEAD 2f1d56a717983dc4b72d747200b58f39ac272925` 加工作区改动；本轮只调整两个工作流的事件过滤与有关说明。

## 维护者裁决

维护者说明上游仓库为 `aspectn/aspectn`，在自己的 fork 的 `dev` 分支开发，并向上游 `dev` 提交 PR；要求在尚未创建 PR 时，fork 每次 push 也能够独立验证。维护者批准以下方案：

- CI 和 Compatibility 的 push 分支限定为 `dev`、`main`。
- 两个工作流的 PR 目标分支限定为 `dev`、`main`，保留默认的 opened、synchronize、reopened 活动。
- 保留 workflow_dispatch、只读权限、现有 concurrency 分组及 cancel-in-progress。
- 保留全部 job、平台和依赖矩阵；不增加查询 PR 或跨仓库跳过验证的逻辑。

fork 的 push 与上游的 PR 验证各自保留。二者分别验证开发分支和与上游目标分支的模拟合并结果；PR 已存在时继续 push 仍会各运行一轮，这是明确接受的成本，不将本次修改描述为消除了全部重复构建。上游合并后，dev 的 push 验证仍保留。

GitHub 将 fork 向上游提交的 PR 事件发送到上游仓库，fork 不会额外收到该 PR 事件；fork 需要自行启用 Actions，上游也可能要求批准来自 fork 的运行。依据为 [GitHub fork PR 规则](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#pull_request)。分支过滤中，pull_request.branches 筛选目标分支，push.branches 筛选收到推送的分支；只定义 push.branches 时标签不匹配，见 [GitHub 工作流语法](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#onpushbranchestagsbranches-ignoretags-ignore)。

## 最终配置与场景

[ci.yml](../../.github/workflows/ci.yml) 和 [compatibility.yml](../../.github/workflows/compatibility.yml) 均使用：

```yaml
on:
  push:
    branches: [dev, main]
  pull_request:
    branches: [dev, main]
  workflow_dispatch:
```

| 操作 | CI 和 Compatibility 的预期行为 |
| --- | --- |
| fork 的 dev 或 main 收到 push | 在 fork 执行，未创建 PR 时同样执行 |
| 其他分支收到普通 push | 不执行这两个工作流；仍可手动触发 |
| 创建、更新或重新打开面向上游 dev 或 main 的 PR | 在上游执行；源分支名称不限 |
| PR 的目标为其他分支 | 不执行这两个工作流 |
| 跨仓库 PR 存在期间继续 push 到 fork/dev | fork 执行 push 验证，上游执行 PR 验证，各自保留 |
| PR 合并进上游 dev 或 main | 上游的 push 验证执行；没有新增 pull_request.closed 触发 |
| 推送标签 | 不执行这两个工作流；现有 Release 继续监听 v* 标签 |
| 手动运行 | 两个工作流均保留手动入口 |

本地 commit 不产生 GitHub Actions 运行；表中 push 指提交已经推送至对应 GitHub 仓库。仓库的 Actions 启用状态、PR 可合并状态及 GitHub 的运行审批仍影响实际执行，本次不修改远端设置。

## 能力与证据适用性

本轮对应盘点 `INV-0014` 的兼容矩阵执行入口，并关联 `CROSS-COMPAT-001`。没有改变 S1—S5 的库行为、测试输入、断言、目标框架或依赖矩阵，既有执行报告保持原范围；没有因修改 YAML 新增任何产品场景的 verified 状态或远程平台执行证明。README 补充 fork 工作流说明，能力条目和盘点同步记录此次裁决；库使用指南及英文 XML API 无行为变化，不需要新增条款。

本轮不修改 Release、发布门禁或签名流程。标签不再额外触发这两个工作流，Release 仍保留自身的构建、测试和包验证；本次没有新增“发布必须等待 Compatibility”的保证。

## 本地校验与限制

当前工作区同时有资源配置任务及 RI-01 的未提交改动；本轮保留这些改动和既有未跟踪文件。使用修改前快照核对工作流的其他配置不变，并进行 YAML 1.2 解析与触发结构静态校验，不以本地静态检查代替 GitHub 上的真实运行。

执行 `node artifacts/ri02/validate-workflows.cjs`，使用本机已有的 `yaml 2.9.0`，没有安装新依赖。三个工作流均解析成功且无 YAML 错误或告警；两个工作流的触发结构与上述配置一致，除 on 以外的解析对象均与修改前快照深度相等，Release 的文件哈希完全不变。快照、校验脚本和结果保存在忽略目录 `artifacts/ri02` 中。该检查不实现或模拟 GitHub 事件引擎，场景表按官方事件与分支过滤语义逐项对照。

| 文件 | 校验时 SHA256 |
| --- | --- |
| .github/workflows/ci.yml | `2b5cbe883866e93aba4ffa56020d0b0147a1f617093a5a70c6a0cf8f04591b47` |
| .github/workflows/compatibility.yml | `9fdb2fd4f6e7915e2f808366a77b8e0aa489131c397f541afa38853ec718fcbd` |
| .github/workflows/release.yml | `9516228735ce7dbd98ed1e289c647cd849eb16e6ad1a9e352ab7e568b7ca98fb` |

有关文件通过 `git diff --check`。本次没有修改 .NET 产品实现或测试内容，因此没有重复运行产品测试矩阵；也没有推送分支或执行远程工作流。fork Actions 启用、上游审批、分支保护和真实执行结果仍须在 GitHub 上确认。

已执行 `pwsh -NoProfile -File eng/generate-knowledge.ps1`、同命令加 `-Check` 及 `pwsh -NoProfile -File eng/verify-documentation.ps1 -AsJson`。三者均返回失败：校验器仍报告 248 项既有证据路径缺失错误、0 告警，全部指向资源配置任务迁移后的 Reflection/Roslyn 旧 SignatureMapper 分部文件；没有本轮新增的结构或引用错误。日志见 `artifacts/ri02/generate-knowledge.log`、`check-knowledge.log`、`verify-documentation.json`。

盘点与能力源文件已同步，派生目录和矩阵因生成器预检失败尚未更新。本轮没有绕过预检、改写历史证据或手工维护派生状态，不能声称当前全仓文档校验通过。

2026-10-22 由独立只读审查者 `/root/ri02_trigger_review` 复核上述变更及官方事件规则，未发现本次范围内的可操作问题。该复核没有运行产品测试或远端 Actions，不扩大上述证据范围。
