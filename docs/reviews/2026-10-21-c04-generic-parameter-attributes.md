# C04 泛型形参声明直接特性修复

日期：2026-10-21。基线：`73095f0de1748685a98d085f8715f9ab8977e15e`；目标为其上本次 C04 变更。保留原综合审查报告及既有未跟踪文件；C05–C07 留待后续逐项讨论。

## 范围与根因

`class G<[Marker] T>` 及 `void M<[Marker] U>()` 都是合法 C#，标记属于泛型形参声明。问题不涉及给未来实参 `int` 等类型附加特性，也不涉及非法重载。

Cecil 的 `GenericParameter` 自身提供 `CustomAttributes`，而其 `Resolve()` 固定返回 null。旧 `GetTypeAttributes` 一律尝试得到 `TypeDefinition`，因而将可读取的声明特性误判为不可用。[Cecil 0.11.5 原始实现](https://raw.githubusercontent.com/jbevain/cecil/0.11.5/Mono.Cecil/GenericParameter.cs)提供对应依据；回归测试也直接断言原生记录存在、`Resolve()` 返回 null。

本次只修复 `HasDirectAttribute(AttributeTarget.ForType(...), ...)`。成员表达式的 `attr(type ...)` 继续指成员声明类型，没有新增泛型形参位置。

## 修复与控制

- `GenericParameter.Owner` 为 `TypeDefinition` 或 `MethodDefinition` 时，直接读取该形参自身记录；有目标记录为 Matched，已知空为 NotMatched。
- 引用对象上的泛型槽位可能只有占位空列表，继续返回 Unknown/AN3004；未映射的中立形参仍为 Unknown/AN3001。本次不新增泛型 owner 引用解析能力。
- 独立复核发现稳定缓存键未区分声明与引用：先映射引用会让声明复用引用关联，反向顺序则让引用借用声明的特性。现将声明来源加入内部泛型形参缓存键，中立声明身份仍相同，分别保留原生关联。
- 读取和特性类型映射仍处于 `CecilMetadataReader` 恢复边界内；取消继续向外传播。
- 类型、方法及其泛型形参分别带标记，防止错误借用所属声明的标记；特性构造器抛异常，确认查询不实例化特性。

没有修改核心公共 API、泛型身份、签名结构或 DSL 语法。Fody 共享生产源码与测试源码，但在独立程序集及 Cecil 身份下实际编译执行。

## 回归与验证

旧实现红灯：Cecil、Fody 各 7 项中 4 失败、3 通过；一致性 5 项均在 Cecil 断言失败。失败前执行的原生记录和 Reflection 记录对照通过；一致性循环先执行 Roslyn 源码、PE，再到 Cecil，不能将未执行的后续 Reflection 断言计作红灯证据。仅修读取路径后，新增 4 项缓存顺序测试也全部失败：两个方向分别复现 Unknown 和借用标记。

最终新增 27 项回归全部通过：Cecil/Fody 各 11 项、一致性 5 项。缓存顺序用例在同一类型系统中分别先映射引用或声明，检查有标记/已知空两个槽位；中立身份仍相等，声明与引用保留各自事实。

完整 Release 编译零警告、零错误；完整测试矩阵 1986 项全部通过、零跳过，包含 net472 及 net10.0 测试宿主。格式、知识库生成与一致性检查、文档检查和 `git diff --check` 均通过；文档校验为 17 条能力、99 条场景、零错误、零警告。

```powershell
dotnet build AspectN.sln -c Release --no-restore --disable-build-servers -m:1 -v:minimal
dotnet test AspectN.sln -c Release --no-build --no-restore -m:1 --logger trx --results-directory artifacts/c04-fix/final-v2 -v:minimal
dotnet format AspectN.sln --verify-no-changes --no-restore --verbosity quiet
pwsh -NoProfile -File eng/generate-knowledge.ps1
pwsh -NoProfile -File eng/generate-knowledge.ps1 -Check
pwsh -NoProfile -File eng/verify-documentation.ps1
git diff --check
```

日志在本地忽略目录 `artifacts/c04-fix/`，可由已提交测试复现。

## 独立复核

按 `requesting-code-review` 技能由只读审查者 `/root/c04_attribute_review` 复核实现、测试及文档。其发现的 Important 缓存关联问题已补充红灯测试并修复；审查者重新执行原生 PE 双向映射反例，确认声明为 Matched、引用为 Unknown/AN3004、中立身份仍相同。最终代码和范围复核没有新增 Critical、Important 或 Minor。

审查者最终核对 EV 的 15 个文件哈希及 8 份最终 TRX，全部匹配，计数为 1986/1986 通过；新增 27 项范围准确，复核结论为可以提交 C04。

## 知识库与证据适用性

受影响能力为 `ATTR-TARGETS-001/S2–S4、S15、S16` 及 `CROSS-AVAILABILITY-001/S2、S5`；盘点为 `INV-0011、INV-0004`。同步用户指南、元数据可用性契约、能力条目和派生矩阵。没有新增或更改 N/A。

新证据 [EV-20261021-001](evidence/EV-20261021-001.json)记录本次输入、版本、代码哈希和完整矩阵；正式 Finding 为 [FND-20261021-001](findings/FND-20261021-001.md)。旧证据保留历史状态，本次不将其原有范围自动扩写为泛型形参或 Fody 全部特性组合。

语言第 14 节的 DSL 目标未变，因此不修改其语法；类型/方法形参的原生记录通过直接 API 查询。未修改签名模型与声明身份公式，C01、C02 既有回归仍随完整矩阵执行。本次未证明任意损坏元数据、所有运行时及依赖版本、Fody 的其他真实特性组合或引用 owner 的额外解析路径。
