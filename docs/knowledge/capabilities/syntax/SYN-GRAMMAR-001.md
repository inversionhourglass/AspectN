# SYN-GRAMMAR-001 词法、邻接、入口与编译诊断

<!-- aspectn-capability:v1
{
  "schema_version": 1,
  "id": "SYN-GRAMMAR-001",
  "title": "词法、邻接、入口与编译诊断",
  "category": "syntax",
  "kind": "feature",
  "profile": "feature-v1",
  "norm_refs": [
    {
      "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
      "section": "3 / 4 / 16 / 17 / 21"
    },
    {
      "path": "docs/superpowers/specs/2026-10-22-configurable-resource-limits-design.md",
      "section": "2—6；仅资源策略与公开入口"
    }
  ],
  "scenarios": [
    {
      "id": "S1",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "AspectNParser / AspectNEngine.Compile*",
      "conditions": "Lexer/LexerLiteral、SelectorParser、TypePatternParser、ParserRecovery及ModifierBoundaryTests的已执行输入；包含修饰符/连续类型名称、否定、复合权限与备选后的类型槽；仅核心解析与绑定；ResourceContractTests两种Parse/Compile预先取消优先于源码超限",
      "target": "working-tree",
      "expected_behavior": "关键字和名称区分大小写；邻接规则保留；恢复给诊断但不能把有错误编译当作有效模式",
      "contract_status": "supported",
      "verification_status": "verified",
      "verification_methods": [
        "automated",
        "manual"
      ],
      "evidence_ids": [
        "EV-20261014-001",
        "EV-20261020-003",
        "EV-20261022-201",
        "EV-20261022-202"
      ],
      "unverified_notes": ""
    },
    {
      "id": "S2",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "CompileMemberExpression",
      "conditions": "ClosedLanguageTests 的未限定与限定自定义选择器",
      "target": "working-tree",
      "expected_behavior": "AN2001；无有效成员模式",
      "contract_status": "excluded",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-001",
        "EV-20261022-201"
      ],
      "unverified_notes": "",
      "exclusion": {
        "reason_code": "explicit_exclusion",
        "reason": "语言契约封闭，没有注册自定义选择器或谓词的扩展接口",
        "decision_refs": [
          {
            "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
            "section": "16"
          }
        ],
        "failure_behavior": "AN2001；无有效成员模式"
      }
    },
    {
      "id": "S3",
      "adapter": "core",
      "input_mode": "neutral",
      "entry_point": "CompileMemberExpression",
      "conditions": "边界项目中的 field/event/params/scoped 表达式",
      "target": "working-tree",
      "expected_behavior": "编译失败且有诊断；不固定全部具体码",
      "contract_status": "excluded",
      "verification_status": "verified",
      "verification_methods": [
        "automated"
      ],
      "evidence_ids": [
        "EV-20261014-009",
        "EV-20261022-201"
      ],
      "unverified_notes": "",
      "exclusion": {
        "reason_code": "dsl_not_expressible",
        "reason": "Standard 选择器集合及参数注解不包含这些独立条件；元数据或 C# 接受不自动形成 DSL 语法",
        "decision_refs": [
          {
            "path": "docs/superpowers/specs/2026-07-31-aspectn-language-spec.md",
            "section": "6 / 11"
          }
        ],
        "failure_behavior": "编译失败且有诊断；不固定全部具体码"
      }
    }
  ],
  "invariant_checks": [
    {
      "invariant_id": "INVLOGIC",
      "applicability": "not_applicable",
      "condition_ref": {
        "path": "docs/knowledge/profiles.json",
        "section": "invariants/INVLOGIC"
      },
      "reason": "选定入口只解析和绑定源码，返回编译诊断，不消费 MatchStatus/MatchResult",
      "review_status": "confirmed",
      "reviewer": "/root/options_review",
      "reviewed_on": "2026-10-22"
    },
    {
      "invariant_id": "INVPARITY",
      "applicability": "not_applicable",
      "condition_ref": {
        "path": "docs/knowledge/profiles.json",
        "section": "invariants/INVPARITY"
      },
      "reason": "选定入口为单一核心语法，不比较多个后端映射或关系",
      "review_status": "confirmed",
      "reviewer": "/root/options_review",
      "reviewed_on": "2026-10-22"
    },
    {
      "invariant_id": "INVMISSING",
      "applicability": "not_applicable",
      "condition_ref": {
        "path": "docs/knowledge/profiles.json",
        "section": "invariants/INVMISSING"
      },
      "reason": "词法/解析/绑定不读取候选元数据；缺失输入记号不是不可用元数据",
      "review_status": "confirmed",
      "reviewer": "/root/options_review",
      "reviewed_on": "2026-10-22"
    },
    {
      "invariant_id": "INVCANCEL",
      "applicability": "applicable",
      "condition_ref": {
        "path": "docs/knowledge/profiles.json",
        "section": "invariants/INVCANCEL"
      },
      "reason": "Parse/Compile公开入口现声明CancellationToken，取消穿过解析、绑定和缓存；符合profile条件，旧无令牌N/A理由不再成立。",
      "review_status": "confirmed",
      "reviewer": "/root/options_review",
      "reviewed_on": "2026-10-22"
    }
  ],
  "relations": [
    {
      "type": "depends_on",
      "capability_id": "GEN-BINDING-001"
    }
  ],
  "open_findings": [],
  "related_findings": [
    "FND-20261020-003"
  ]
}
-->

## 摘要

一个 Standard 语言，类型模式和成员表达式是不同入口；语法错误、绑定错误与匹配失败分阶段处理。

## 契约

关键字小写、序数比较。布尔优先级为 !、&&、||，分组可以改变顺序。`.`、`..`、`/`、泛型 `<`、数组 `[`、可空 `?` 与名称的邻接有明确规则。没有扩展注册表或第二语言模式。

修饰符识别先保留连续类型模式：`static*` 不等于 `static *`，`!static*` 不等于 `!static *`。正向/否定、复合权限的第二个词、访问性备选后的词均不能截断类型名称。方法及属性共用解析规则；`public|internal`、`private protected *` 等原有修饰符形式保持有效。

## 输入条件

输入为字符串源码。这里的 neutral 表示核心源码编译过程，不表示存在候选签名。解析可返回恢复树；编译是否能产生模式由诊断和 TryGet* 决定。

## 分层能力

Lexer 保留跨度、记号和附属文本，Parser 构建不可变树，Binder 验证选择器适用域、变量和后缀。后端不参与决定语法。

## 阶段失败

未知选择器 AN2001；空白分开的 `! ref` 为 AN2005；非法泛型位置 AN2011；深度保护 AN1002。诊断可多个并存，不要求第一条覆盖全部原因。

## 例子与边界

```csharp
var engine = new AspectNEngine();
var valid = engine.CompileMemberExpression("method(public string *(..))");
var invalid = engine.CompileMemberExpression("vendor.select(*)");
// valid 可取得成员模式；invalid 有 AN2001，不能取得模式。
```

`method(* A..Run(..))` 不能省略声明类型名称段；`method(* *..*.Run(..))` 与 `method(* Run(..))` 的范围不同。源码支持 `params`/`scoped` 的方法仍可按实际参数类型匹配，这两个关键字没有独立 DSL 筛选能力。

## 规范依据

正式语言规范第 3、4、16、17、21 节；成员选择器与 ref 注解见第 6、11 节。

## 实现与证据

[解析测试](../../../../tests/AspectN.Tests/Parsing/SelectorParserTests.cs)、[封闭语言测试](../../../../tests/AspectN.Tests/Binding/ClosedLanguageTests.cs)及[边界示例](../../../usage/examples/MatchingBoundaries/Program.cs)。场景证据只证明已执行输入，不证明任意恢复路径都完成审查。

S1 的修饰符边界回归见 [ModifierBoundaryTests](../../../../tests/AspectN.Tests/Parsing/ModifierBoundaryTests.cs)。[C03 修复记录](../../../reviews/2026-10-20-c03-modifier-boundaries.md)另列真实后端匹配对照，其通过不扩大本条目只覆盖解析/绑定的入口范围或当时四项 N/A；[Finding](../../../reviews/findings/FND-20261020-003.md)记录原始误解析及修复。

[RI-03](../../../reviews/2026-10-22-ri03-fuzz-host-timeout.md)调整解析 fuzz 的测试防线：不再按单个样本的一秒墙钟时间判定失败，由核心测试项目的 VSTest 配置提供 60 秒宿主级挂起保护，保留文本长度和诊断跨度检查。该工程验证登记于盘点 `INV-0019`；该项工程调整本身不改变本条目 S1—S3 的语言契约、验证状态或当时 N/A，也不把有限随机样本解释为全部输入的终止证明。RI-03自身未改变用户解析配置或生产行为；本轮资源配置另按下文重评。

## 关联与缺口

此条目不验证后端或匹配三态。编译入口的三项 N/A 已由非作者按入口核验；不借此豁免完整匹配流程。变量语义依 GEN-BINDING-001。

2026-10-22资源配置变更按[实施审查](../../../reviews/2026-10-22-configurable-resource-limits.md)重评当前证据。历史报告不改写；新增配置的范围由CROSS-RESOURCES-001/S9—S15区分。INVCANCEL改为applicable，其余三项纯语法N/A由非作者/root/options_review于2026-10-22重新确认；理由见元数据。没有从缺少某后端场景推断排除。

本轮入口增加CancellationToken后，ParseAndCompile_PreCanceled_PrioritizeCancellationOverSourceLimit覆盖两种解析/编译入口及既有树编译的取消优先；旧C03/RI-03记录保留各自当时的变更范围，不代表本轮入口仍无取消。
