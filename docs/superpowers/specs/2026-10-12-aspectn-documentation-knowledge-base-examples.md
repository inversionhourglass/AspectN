# 最终设计配套：字段格式与真实样例

本文件配套 [最终设计](2026-10-12-aspectn-documentation-knowledge-base-design.md)。以下格式是实施基线；本文件中的样例仅用于校准模板，不是实际知识库记录或验证证据。正式实施结果见[试点审查记录](../../reviews/2026-10-12-documentation-pilot.md)。

## 1. 最小存储格式

能力和 Finding 的 Markdown 标题之后各包含一个 JSON 注释块，分别以 `<!-- aspectn-capability:v1`、`<!-- aspectn-finding:v1` 开始，以 `-->` 结束。每个文件恰好一个对应块，schema_version 为 1。正文先摘要，再展开细节。

来源、覆盖、profiles 和证据报告为普通 JSON；每个登记文件含 schema_version。引用路径相对仓库根目录，保存为正斜杠；正文 Markdown 链接相对所在文档。机器字段只维护一份；正文不再维护独立状态枚举。

| 记录 | 最小必填键 |
| --- | --- |
| 来源 | id、path、role、scope、target、approval、delegated_by、supersedes |
| 覆盖 | id、subject、origin_refs、assessment_status、capability_ids、notes |
| profile | id、kind、required_sections、invariants |
| 能力 | id、title、category、kind、profile、norm_refs、scenarios、invariant_checks、relations、open_findings、related_findings |
| 场景 | id、adapter、input_mode、entry_point、conditions、target、expected_behavior、contract_status、verification_status、verification_methods、evidence_ids、unverified_notes |
| 不变量核验 | invariant_id、applicability、condition_ref、reason、review_status、reviewer、reviewed_on |
| Finding | id、kind、status、capability_ids、inventory_refs、input_class、channel、severity、confidence、blocking、expected、actual、impact、reproduction、countercheck、decision_refs、related_evidence |
| 证据 | id、method、scenario_refs、oracle_refs、procedure、result、recorded_on、environment、code_state、dependency_scope、revalidation_conditions |

### 1.1 容器、类型与引用

- sources.json、coverage.json、profiles.json 的顶层分别为 `{schema_version: 1, sources: [...]}`、`{schema_version: 1, items: [...]}`、`{schema_version: 1, profiles: [...]}`；这里只示意结构，实际文件使用标准 JSON 引号。单份证据报告为含 schema_version 和证据字段的对象。
- id、标题、类别、枚举、解释和路径为字符串；scenarios、invariant_checks、relations 及引用集合为数组。所有集合字段必须存在，空集合表示当前没有记录，不表示已经穷举。code_state.dirty 为布尔值。
- 场景编号在能力内唯一，采用 S1、S2 等；scenario_refs 使用 `{capability_id, scenario_id}`。能力、证据、Finding 和盘点引用使用编号字符串。
- 条款引用统一为 `{path, section}`；section 是可辨认的标题、条款号或范围描述。norm_refs、origin_refs、delegated_by、supersedes、condition_ref、oracle_refs、decision_refs 使用此形态，其中 condition_ref 是单个对象，其余为数组。首批 CI 检查路径和字段，条款语义及范围由 review 核验。
- relations 的元素为 `{type, capability_id}`。路径不得逃逸仓库；源码符号和外部资料可在正文补充，不伪装成已存在的本地文件。
- scope、conditions、expected_behavior、unverified_notes、reason、expected、actual、impact、reproduction、countercheck、procedure、revalidation_conditions 为可审查的文字。target 明确工作树或发布版本；时间使用 ISO 8601，reviewed_on 可用 YYYY-MM-DD。
- environment 为键值对象；code_state 为 `{head, dirty, files: [{path, sha256}]}`；dependency_scope 为 `{paths: [...], environment_keys: [...]}`，列出已声明的文件与环境依赖范围。真实代码值在执行取证时获取。

### 1.2 条件必填

- contract_status=excluded 时增加 exclusion 对象，包含 reason_code、reason、decision_refs、failure_behavior；reason_code 使用主设计的五类。pending 时增加非空 pending_reason，说明待裁决事项。支持场景不填写虚构的排除原因。
- verification_status=verified 时 evidence_ids 非空，且存在当前适用的 passed 报告；非 verified 时 unverified_notes 非空。failed 还须关联复现证据或 Finding，说明已确认的产品违约。
- Finding 中尚未分配能力编号的正式记录，inventory_refs 非空；不将历史样例的空关联误作正式合法记录。countercheck 尚未执行时写明缺口，不伪造反证结果。
- fixed 的 Finding 必须关联回归证据；rejected、accepted_risk、documentation_resolved 必须有处置出处，accepted_risk 还包含非空 revalidation_conditions。blocking=yes/pending 时包含非空 blocking_reason；关闭状态仍保留原始复现和有关证据。
- N/A 未核验不产生通过结论；confirmed 必须有 reviewer、reviewed_on 和非空具体理由。profile 要求的核验记录不能以空数组省略。试点阶段核验主体遵循主设计的非作者规则。
- 来源 approval=approved 时增加非空 approval_refs，记录批准或既有批准规范的可追溯出处；不因入目录、被模型引用或校验通过而自动批准。

### 1.3 初始枚举与 profile

来源 role 为 normative、guide、history；approval 为 approved、pending、historical。实际 role 与 approval 的组合需人工确认。supersedes 为有向条款引用，不按文件日期自动生成。

场景 adapter 为 core、cecil、cecil_fody、roslyn、reflection。input_mode 为 neutral、native_member、bare_type、source_symbol、pe_symbol、reference、detached。target 为 working-tree 或带版本的 release 标识；不能将仓库新行为自动推广到已发布包。

N/A 的 applicability 为 applicable 或 not_applicable；review_status 为 pending、confirmed、rejected；未核验时 reviewer 与 reviewed_on 为 null。confirmed 必须有核验者与日期；applicable 场景不用 N/A 豁免表明测试通过。

relations 使用 depends_on、related_to、supersedes、superseded_by，按单边事实保存；反向引用可生成。历史重定向不构成能力依赖循环。

初始 profile 为 feature-v1 和 cross-cutting-v1，必填正文采用“摘要、契约、输入条件、分层能力、阶段失败、例子与边界、规范依据、实现与证据、关联与缺口”。cross-cutting-v1 允许分层能力引用类型适用条件说明，不能省略契约或阶段失败。

初始不变量为 INVLOGIC（三态）、INVPARITY（等信息输入的适配器一致性）、INVMISSING（消费事实缺失）、INVCANCEL（声明支持取消的入口）。这些是 profile 标识，不是覆盖盘点的 INV-NNNN 编号。各 profile 必须给出适用条件，不把全部不变量无条件套到全部能力。

证据的 code_state 至少含 head、dirty、files（path 与 sha256）；报告没有取得实际值时不能登记为有效 passed。这里不填伪造 SHA 或测试成功记录。

## 2. 功能能力样例：USE-REF-001

### 2.1 元数据示例

该示例仅定义核心参数 ref-kind 约束；Reflection 裸 by-ref 的边界在正文说明，并在正式盘点时建立相应场景，不能误读为已验证全部适配器。

```json
{
  "schema_version": 1,
  "id": "USE-REF-001",
  "title": "成员类型使用位置的 ref-kind 约束",
  "category": "type-uses",
  "kind": "feature",
  "profile": "feature-v1",
  "norm_refs": [
    {"path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md", "section": "11"},
    {"path": "docs/language/metadata-availability.md", "section": "By-reference shape and ref classification"}
  ],
  "scenarios": [{
    "id": "S1",
    "adapter": "core",
    "input_mode": "neutral",
    "entry_point": "成员参数的 ref-kind 约束求值",
    "conditions": "Int32 参数，所需类型身份及 ref-kind 均已知；其他成员约束独立成立",
    "target": "working-tree",
    "expected_behavior": "省略 ref-kind 不约束该维度；!ref 约束非 by-ref；显式 ref-kind 要求精确分类相同",
    "contract_status": "supported",
    "verification_status": "unverified",
    "verification_methods": ["automated"],
    "evidence_ids": [],
    "unverified_notes": "已定位测试源码，本设计阶段未运行测试"
  }],
  "invariant_checks": [{
    "invariant_id": "INVMISSING",
    "applicability": "applicable",
    "condition_ref": {"path": "docs/superpowers/specs/2026-10-12-aspectn-documentation-knowledge-base-design.md", "section": "6.1 缺失元数据场景的适用条件"},
    "reason": "精确 ref-kind 求值消费可用性事实，正式条目需要补充未知事实场景",
    "review_status": "pending",
    "reviewer": null,
    "reviewed_on": null
  }],
  "relations": [],
  "open_findings": [],
  "related_findings": []
}
```

### 2.2 契约、边界与阶段

参数模式未写 ref-kind 时不约束该维度；`!ref` 要求非 by-ref；`ref`、`out`、`in`、`ref readonly` 对精确分类提出约束。其他类型、名称和 selector 条件仍独立成立。

| 参数模式 | 已知候选分类 | 参数维度预期 |
| --- | --- | --- |
| int | Ref | 满足 |
| !ref int | Ref | 不满足 |
| ref int | Ref | 满足 |
| ref int | None | 不满足 |
| out int | Out | 满足 |
| ref readonly int | RefReadOnly | 满足 |

这些行来自 [RefKindTests](../../../tests/AspectN.Tests/Evaluation/RefKindTests.cs) 的 IsRefKindMatch_ParameterPattern_ReturnsExpected。它验证参数维度，不能单独当作完整成员匹配及全部后端映射的证明。

DSL 示例 `method(* Service.Get(ref int))` 在成员参数位置约束 ref。解析/绑定错误与匹配不满足分开描述；空白分隔的 `! ref` 是语法诊断问题，关联 SYN 条目，不当作普通不匹配。

[元数据可用性契约](../../language/metadata-availability.md) 说明：Reflection 裸 by-ref Type 保留 ByReference，但精确 RefKind 不可用；成员参数/返回映射可保留精确分类。不能凭裸 Type 猜测 ref/out/in。精确分类不可用时，结果依所消费事实及其他已知约束决定，不一概 NotMatched。

正式条目还需登记所需事实未知的场景、各适配器入口和证据。上例 condition_ref 引用当前设计规定的适用条件；实施时由 profile 登记具体条件。核验记录展示形态，完整条目必须覆盖 profile 要求的所有不变量。相关 INVLOGIC/INVMISSING 等适用性需要完成试点核验，不能据此宣称已通过门禁。

## 3. 横向能力样例：CROSS-LOGIC-001

### 3.1 场景和状态

编号 CROSS-LOGIC-001，分类 cross-cutting，类型 cross_cutting，profile cross-cutting-v1。场景为 core 的 MatchLogic 对 MatchStatus 及 MatchResult 的公共逻辑运算；契约 supported，验证 unverified，验证路线 automated，evidence_ids 为空。

正式元数据采用与上例完全相同的键和状态，不建立第二种字段格式。独立依据为 [正式规范 §5](2026-07-31-aspectn-language-spec.md)，测试入口为 [MatchLogicTests](../../../tests/AspectN.Tests/Evaluation/MatchLogicTests.cs)。

### 3.2 不变量及证据范围

令 T=Matched、F=NotMatched、U=Unknown：

| 左/右 | T | F | U |
| --- | --- | --- | --- |
| T 的 AND | T | F | U |
| F 的 AND | F | F | F |
| U 的 AND | U | F | U |
| T 的 OR | T | T | T |
| F 的 OR | T | F | U |
| U 的 OR | T | U | U |

NOT 分别为 T→F、F→T、U→U。状态已知不意味着有关 Unknown 诊断必须消失；MatchLogicTests.Combine_UnknownDiagnostic_RemainsOnKnownResult 验证这一独立观察行为。

相关方法包括 And_AllTruthTableRows_ReturnExpectedStatus、Or_AllTruthTableRows_ReturnExpectedStatus、Not_AllTruthTableRows_ReturnExpectedStatus。三态例子使用真值表，不需要套用原生程序集输入字段。

这些证据不单独证明 DSL 中所有变量绑定、OR 分支合并或取消行为；它们是其他关联能力。profile 适用性和相关场景仍需在实施中登记。

## 4. 用户指南片段样例

建议放入 results-and-diagnostics.md 的“缺失信息如何影响结果”章节，覆盖元数据可用性与三态能力。

候选是普通的 Service.Run，返回 Int32，只有 Accessibility 不可用，其他必要事实已知：

| 表达式 | 预期状态 | 原因 |
| --- | --- | --- |
| method(public * Service.Run(..)) | Unknown | 查询 public 时缺少可见性事实 |
| method(!public * Service.Run(..)) | Unknown | 否定不能把未知变成确定 |
| method(public string Service.Run(..)) | NotMatched | 已知返回类型不符足以确定不匹配 |

片段直接取自 [MetadataAvailabilityTests](../../../tests/AspectN.Tests/Evaluation/MetadataAvailabilityTests.cs) 的 Match_OneUnavailableFact_DoesNotDisableIndependentFacts 对应行，规范依据为 [metadata availability](../../language/metadata-availability.md)。章节附“不能把占位值或空集合当作事实”的排错说明。

正式实施时应对上述具体行运行测试并保存证据。用户指南可以解释已批准的预期，但不能宣称本轮已经重新验证。

## 5. 历史 Finding 迁入样例：H1

输入依据：[2026-10-11 维护者 H1 裁决](../../reviews/2026-10-11-review-followup.md)。这是已有处置，不能创建新的“确认缺陷”结论。

| 字段 | 迁入值 |
| --- | --- |
| id | FND-20261011-001；正式分配前确认同日编号未占用 |
| kind / status | robustness_risk / accepted_risk |
| capability_ids | 待关联能力未建档时为空，必须关联输入范围盘点记录 |
| 输入类别 | 缺少必要 ReturnType/ElementType 的人工构造 Cecil 对象 |
| channel / severity / confidence / blocking | none / unrated / limited / no |
| reproduction | 原报告或明确的合成对象路径，保留无法证明真实程序集可达的局限 |
| decision_refs | 2026-10-11-review-followup.md 的“维护者裁决/H1” |
| 处置 | 保留异常，不增加容错，不转换 Unsupported/Unknown |
| 重新评估条件 | 新的真实程序集复现或有关输入契约变化 |

accepted_risk 在这里编码既有“不修复”处置，不表示所有描述的进程风险已经成立。不能为了填字段重新伪造 High 严重度、已验证真实调用链或旧红新绿报告。正式导入还需补齐原始复现引用及盘点编号，没有取得的历史证据保持缺口说明。

## 6. 结构与行为验收

配套材料中的示例不参与实际索引和 CI，不占用正式编号；实施时按实际记录导入并核验。

首批校验应能识别重复编号、非法状态、缺失必填字段、失效规范/能力引用和用户章节编号；Finding 引用按告警级处理。元数据有效不表示示例运行通过。

试点由非作者从知识库入口找到正确规范，区分这些样例的契约、源码证据和未运行状态，并完成一次变更影响判断。如此才能验证格式确实支持渐进式披露，而不是仅能通过字段检查。
