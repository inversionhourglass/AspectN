# RM-21：知识库标识符使用 Ordinal 排序

日期：2026-10-23；对应 GLM-M7；基准 `5d56cb06aed7d01d328ffcfaed3dbd97fda48f0d`。维护者批准按[计划](../plans/2026-10-23-rm21-ordinal-knowledge-order.md)统一能力ID和盘点ID的字符串排序。

## 问题与修改

能力文件路径原本已按Ordinal排序，最终能力ID和盘点ID却使用文化相关的Sort-Object。合法的CH/H/I名称集合在捷克语环境会产生不同顺序，可能导致派生行序和跨文化-Check结果不同。当前真实17个能力ID在en-US、zh-CN、da-DK、cs-CZ下未发生漂移；本项修复未来允许名称集合的确定性隐患，不将原报告的当前漂移指控改写为已证实。

新增私有 Sort-RecordsByOrdinalId，使用带 StringComparer.Ordinal 的 SortedDictionary 排序记录，两个字符串ID入口复用。ID唯一性仍由生成前预检验证，分类顺序、原路径策略、文件路径排序及场景数字排序保持。场景S1/S2/S10按数字部分排序；盘点ID按字符串序号排序，不改为数字排序或添加新格式约束。

## 跨文化回归与真实资料

现有生成器测试追加由正式校验器接受的USE-CH-001、USE-H-001、USE-I-001，与原USE-TEST-001同属type-uses分类。各新增能力以S10/S2/S1乱序输入，盘点以INV-9000/INV-0002/INV-10000乱序输入。测试文件按cap.id写入以支持多条合成能力，原14项夹具及语义保持。

子进程显式设置CurrentCulture和CurrentUICulture并输出实际文化名称，在en-US、zh-CN、da-DK、cs-CZ下分别执行完整生成和同一份en-US生成输出的只读-Check，共8项。逐项检查两份文件内容一致、Ordinal能力及盘点顺序、场景数字顺序，并记录生成SHA256。

基准新增检查6项通过、2项失败，均为cs-CZ：生成行序不同，检查en-US输出时退出1。修复后8项全部通过，四种文化生成的catalog与support-matrix分别具有相同SHA256。原14项生成器黑盒和24项路径检查同时通过，空分类及单记录输入继续有效。

```powershell
pwsh -NoProfile -File eng/tests/generate-knowledge.Tests.ps1
```

真实仓库在更新本项盘点文字之前，修改前后分别以四种文化执行-Check，八次均退出0；两份派生文档哈希前后一致。此控制独立于合成CH/H/I反例，证明当前17个ID没有因修改改变行序。之后矩阵仅增加INV-0018处置说明，catalog不变。原始记录、源码和派生哈希见[数据](data/2026-10-23-rm21-ordinal-knowledge-order.json)。

## 能力与验证边界

本项属于INV-0018工程工具盘点，保持unassessed；不修改产品实现、语言、公开API、场景状态、evidence_ids或N/A。用户匹配调用无影响，使用章节无行为修订需求；工程规则与命令由本报告覆盖。历史审查保留当前漂移未复现的原结论。

CI没有修改，新增回归通过原生成器测试入口随既有Windows/Linux矩阵执行，RM-11双平台格式检查保持。本机实际运行Windows，区域设置只在测试子进程内改变；未执行真实Linux或远端Actions。本轮无.NET代码变化，不重跑产品矩阵、打包或消费者，RM-13已记录的Roslyn/net472 System.Memory阻塞仍保留。

## 收尾核验

独立只读审查未发现可操作问题；复核了排序函数的空、单条、多条输入及selector作用域，生成前唯一性预检、真实子进程文化设置、红绿记录、源码与真实资料哈希以及报告范围。审查没有代替Linux、远端Actions或产品矩阵验证。

运行正式生成器后，catalog保持不变，support-matrix仅更新INV-0018处置说明。`eng/generate-knowledge.ps1 -Check`、`eng/verify-documentation.ps1 -AsJson`及`git diff --check`通过；全仓文档校验0错误、0告警。未跟踪后续清单同步为21项已处理、1项保留现状、2项Minor待确认，清单及其他既有WIP不纳入本项提交。
