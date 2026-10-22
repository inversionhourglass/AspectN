# 能力驱动的 Code Review

1. 固定代码状态、变更范围、有关能力/场景、环境与排除范围。未建档先登记[盘点](../coverage.json)，继续依现有规范审查。
2. 从分类摘要进入条目，读 norm_refs 和必要依赖；分别对照承诺、阶段表现、事实可用性、证据和缺口。
3. 根据 profile 和历史种子选择组合，不要求全组合穷举。不变量不适用项附具体理由和条件引用；试点新增/改动 N/A 默认 pending，由非作者核验后记主体/日期。存量范围抽查 ceil(N/3)，N 是能力+不变量记录数；列候选、抽中和结果并轮转。
4. 发现先保留复现、预期、实际、反证及五个独立字段。承诺范围实质违约走 contract；真实入口可达、可复现且严重危害的证据走 robustness；最终阻塞由维护者裁决。已批准异常和未捕获异常不能直接证明不可恢复危害。
5. 已修复附原复现或等价映射回归；rejected 附反证；accepted_risk 附维护者决定与重验条件；documentation_resolved 附修订证据。未证实不能假装 fixed。
6. 同步条目、用户受影响章节和证据；记录本轮未覆盖范围。修复产品 bug 不把 supported 改 excluded；维护者不修复风险仍对受影响用户可见。

源码重命名后保留历史证据的原路径和哈希，不将当前路径回填成原始快照。快照与依赖路径的结构校验见[格式说明](../format.md)；通过校验不自动恢复证据的当前适用性。当前规范、依据和正文断链仍需修复，不能借历史快照声明豁免。

[能力模板](../templates/capability.md)、[Finding 模板](../templates/finding.md)、[证据格式](../templates/evidence.json)。正式 Finding 放 docs/reviews/findings/，正式报告放 docs/reviews/evidence/。

首批 Finding 引用仅告警。校验通过不能代替产品测试或维护者裁决。完整全仓规则的准备闸门尚未自动生效；本轮只治理已建档场景及盘点例外路径。

[试点审查记录](../../reviews/2026-10-12-documentation-pilot.md)提供首批阅读路由、六项初始 N/A 核验及变更影响分析；[E 阶段审查记录](../../reviews/2026-10-13-documentation-expansion.md)补充九分类扩展、证据范围修订、N/A 复核和生成器验证，供后续审查参考。

[本轮能力分类审查](../../reviews/2026-10-13-capability-evidence-and-review.md)记录两项补证据、九分类实际范围、已确认但未修复的Finding及待裁决边界；包含审查中断和未执行输入的限制。
