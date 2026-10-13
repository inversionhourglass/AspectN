# AspectN 文档与知识库 E 阶段实施计划

> **执行者：** 使用 writing-plans 细化已批准设计，并按 executing-plans 分批实施；当前由 Codex 在现有检出中执行，按 requesting-code-review 请求独立审查。

**目标：** 补现有证据缺口，按 .NET/C# 功能扩展全部九分类，并从场景元数据生成支持矩阵和导航。
**架构：** 继续使用现有 Markdown 正文、唯一 JSON 元数据块、来源/盘点/profile 和正式证据；派生展示只读取这些资料，不成为第二份人工维护的契约。
**技术栈：** C#、xUnit、既有六个 .NET 10 测试项目及兼容测试，PowerShell 7 标准库，现有文档校验和 CI。

## 基线与边界

用户于 2026-10-13 授权开始 E 阶段。基线为 25eb76fb8459c3c7c4ad793b4b0d931713d03200；原有五能力、19 场景，其中 8 场景未验证，coverage 有 12 项未完整盘点。原始未跟踪 `nul` 保留。

依据：[最终设计](../superpowers/specs/2026-10-12-aspectn-documentation-knowledge-base-design.md)第 8 节及[字段格式](../superpowers/specs/2026-10-12-aspectn-documentation-knowledge-base-examples.md)。已有支持承诺、H1 接受风险、公开 API 和语法均保持；不以文档修订修复产品缺陷。实际矛盾记录 Finding/冲突及复现，只有受影响的新承诺待裁决，无关工作继续。

范围以当前工作树和实际测试环境为目标。版本/后端/输入模式按真实证据分行；不由当前源码推断发布包或其他版本。全仓强制同步规则维持原状态；其生效范围后续由维护者在准备闸门满足后确认。

## 批次 1：现有证据缺口

**文件：**

- 修改：`tests/AspectN.Reflection.Tests/ReflectionTypeUseTests.cs`。
- 修改：`docs/knowledge/capabilities/type-uses/USE-REF-001.md`、`docs/knowledge/capabilities/cross-cutting/CROSS-AVAILABILITY-001.md`。
- 新增：`docs/reviews/evidence/EV-20261013-004.json` 起的实际报告、`docs/reviews/2026-10-13-documentation-expansion.md`。

1. 将 8 个未验证场景对照明确测试：InteropRefKindTests、CecilCallableMappingTests、RoslynMappingTests、ReflectionMappingTests、ReflectionTypeUseTests、CecilUnavailableMetadataTests、UnavailableAncestorTests。Fody 项目实际编译 Cecil 测试源码，须单独执行其程序集并记录 Fody Cecil 身份。
2. 补裸 Type 消费缺失 RefKind 的真实匹配断言：省略注解匹配、!ref 已知不匹配、精确 ref/out/in/ref readonly 未知、已知类型失败支配。仅验证已存在行为，不改产品实现。
3. 场景按实际输入、入口和断言范围写清条件。宽描述未完全覆盖时保留未验证残余场景，或拆分具体场景并解释拆分，不直接把一组后端测试标成整个能力通过。
4. 执行核心、Cecil、Fody、Roslyn、Reflection 和 conformance .NET 10 测试；顺序构建以避免共享输出竞争。运行 compat 项目的 net10.0 与 Windows net472；失败按实际证据记录，不能虚报支持。
5. 保存执行时间、精确命令、TRX/日志、计数、SDK/运行时/依赖、HEAD/dirty、实际相关文件 SHA；旧报告不变。改变适用条件时重新评估不变量，原 N/A 判断保存在审查记录。

## 批次 2：按分类扩展

**文件：**

- 新增：`docs/knowledge/capabilities/{types,generics,type-uses,members,csharp-metadata,attributes,syntax,semantics,cross-cutting}/*.md`。
- 修改：各 `docs/knowledge/categories/*/index.md`、`docs/knowledge/coverage.json`、`docs/knowledge/index.md`。
- 新增：`docs/usage/language-and-metadata.md`、`docs/usage/lifetimes-and-compatibility.md`；修改有关使用指南的元数据、示例链接和定位。

1. 盘点词法/邻接/诊断；泛型元数、模式变量及作用域；名称、嵌套、程序集、转发与关系。
2. 盘点可空类型、元组、tasklike、指针/函数指针及自定义修饰符；方法、构造器、属性、访问器、事件访问器和生成成员；直接特性目标。
3. 盘点正则子集、规范签名文本、超时；async/迭代器/partial 源码与 PE；取消、资源上限、缓存与生命周期；公开 API、程序集身份及兼容资产。
4. 把 H1 和有效/人工损坏输入的界限建为可追溯范围，保留既有不修复处置及真实可达性缺口。
5. 每条遵循九正文节，至少提供输入、表达式/模型代码、预期结果、失败阶段与原因；排除包含正式依据和五类 reason_code；复杂流程使用 Mermaid。依赖只链接有关条目，不要求读整个库。
6. 根据实际已执行断言标 verified；没有运行、只有部分覆盖或没有独立预期时保留明确缺口。新/改 N/A 默认待核，独立审查后登记主体、日期和具体条件。
7. coverage 记录已评估范围及剩余子范围，不能将一个宽主题已建条目解释为全组合验证完毕。

## 批次 3：派生矩阵与导航

**文件：**

- 先新增测试：`eng/tests/generate-knowledge.Tests.ps1`。
- 新增实现：`eng/generate-knowledge.ps1`。
- 生成：`docs/knowledge/generated/support-matrix.md`、`docs/knowledge/generated/catalog.md`。
- 修改：`.github/workflows/ci.yml`、`docs/knowledge/format.md`、`README.md` 及入口链接。

1. 先定义黑盒行为：临时资料树的 supported/excluded/pending 与 verified/unverified/needs_reverification/failed 独立展示，保留输入模式/目标/条件/证据和未盘点项，中文标题及 Markdown 转义正确。
2. 先运行测试，确认因生成器缺失失败；实现最小 PowerShell 标准库生成器，提供默认写入与 `-Check` 只核验模式，支持 `-RepositoryRoot` 用于独立测试。
3. 检验确定性、数据修改后 Check 非零、丢失文件非零、坏元数据/引用失败且不覆盖既有输出、表格管道/反引号/换行转义。生成输出带来源说明，禁止人工编辑；不把 verified 解释为当前报告自动适用。
4. 输出逐场景完整矩阵和九分类目录；没有某后端行不等于排除，跨适配器一致性不由汇总行猜测。
5. 在现有 CI job 接入生成一致性检查及测试；维持原 Finding 告警行为，不启用新的产品语义阻断。

## 批次 4：审查、验收与提交

1. 运行所有真实例子、聚焦新增断言、生成器黑盒测试、既有文档校验器回归、文档校验和派生展示 `-Check`。
2. 请求非作者按九分类执行定位任务、检查证据 scope/哈希及排除理由，核验全部新增/改 N/A，并抽查范围内未变的已核验 N/A 至少 ceil(N/3)。
3. 验证 Markdown 标题、围栏、链接、泛型显示、Mermaid 和字段一致；修复重要发现，不伪造核验签章。
4. `dotnet format AspectN.sln --verify-no-changes --no-restore`、`git diff --check`，核对 src 无修改及原始文件保留。
5. 按任务批次提交，不推送/发布；更新此计划与实际审查记录，明确本轮覆盖和残余证据缺口。
6. E 本轮完成标准：九分类有可追溯盘点与条目；旧缺口完成验证或有具体剩余条件；用户说明同步；矩阵由唯一场景数据确定生成；检查和独立审查通过。不是全版本、全输入组合或强制同步闸门已生效。

## 执行记录

四批均已完成。详细命令、证据、N/A逐项核验和未覆盖范围见[本轮审查记录](../reviews/2026-10-13-documentation-expansion.md)。

- 批次1：补裸Type七个消费输入，原八个未验证场景按实际范围追加证据；八组测试共1630通过，新增九份正式报告，旧报告保留。
- 批次2：新增12条能力，合计九分类17条、76场景；覆盖盘点及两章中文指南同步，73场景已验证、3场景明确未验证。
- 批次3：目录和矩阵由唯一元数据生成，完整格式预检复用现有校验器；CI接入派生检查及黑盒，13项黑盒通过。
- 批次4：非作者复核通过，新八项原N/A中七项确认、一项改适用后确认，存量抽查一项；文档0错误/0警告，45项校验器回归、真实示例、格式及差异检查通过。提交按当前用户授权进行，不推送或发布。

全仓强制同步生效闸门保持原状态，等待维护者明确确认范围。H1既有处置保持，不新增修复或真实程序集可达性结论。
