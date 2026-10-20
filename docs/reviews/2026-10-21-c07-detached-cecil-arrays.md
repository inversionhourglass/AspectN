# C07 游离 Cecil 数组关系修复

日期：2026-10-21。基线：`ef3caf93b7114a1aee0d289f46342379571563b1`；目标为其上本次 C07 变更。保留原综合报告、既有未跟踪文件及其他历史 Finding。

## 范围与根因

公开 Cecil 构造器可建立 `new ArrayType(new TypeReference("Example", "Unknown", null, null))`。该数组元素非空，`MapType` 成功并保留原生关联，但原生引用自身没有 Module。旧 `EnumerateArrayRelations` 将空 `array.Module` 传给 `SyntheticReference`，访问 `module.TypeSystem.CoreLibrary` 时 NRE，导致 IsAssignableTo 和 GetInheritanceClosure 异常外抛。

这是已映射原生引用的上下文回退问题，不是中立签名脱离原生关联，也不是 H1 中缺少必需 ElementType 的人工损坏对象。未证明真实 PE 的数组会自然丢失 Module，不宣称普通程序集扫描会因此崩溃。

## 最小修复

数组关系合成改为 `array.Module ?? _mapper.Module`，与已有泛型形参回退一致。数组自带模块优先；缺失时使用当前映射上下文的核心库身份。不扩大 catch，不修改形状、元素身份、泛型接口列表、限制或取消处理。

数组的 System.Array/Object 及六个非泛型接口不依赖元素定义解析。只有 Vector 合成五个泛型集合接口，实参与数组元素精确一致；非 SZ 一维和二维数组不合成这些接口。本次不改变数组之间既有名义关系，也不把任意未知类型推断为可解析定义。

## 回归与验证

新增 [CecilDetachedArrayTests](../../tests/AspectN.Cecil.Tests/CecilDetachedArrayTests.cs)，由普通 Cecil 和 Fody 项目分别编译执行，各 30 项，共 60 项。旧实现各 9 项抛 NRE、21 项控制通过；修复后各 30/30 通过。

- 三种元素输入：Module/Scope 为空的游离引用、带 Module 的缺依赖引用、模块 TypeSystem.Int32 引用；每种均覆盖 Vector、非 SZ 一维和二维形状。
- 每种组合分别核对固定关系、完整闭包，以及五个泛型接口的正确/错误元素目标；Vector 闭包 13 项，General 闭包 8 项。
- 测试 resolver 拒绝依赖解析，且明确断言未解析元素 Resolve 抛 AssemblyResolutionException，固定关系仍可确定。
- 独立模块使用不同核心库引用，验证原 Module 优先；两入口均验证预先取消传播。

```powershell
dotnet build AspectN.sln -c Release --no-restore --disable-build-servers -m:1 -v:minimal
dotnet test AspectN.sln -c Release --no-build --no-restore -m:1 --logger trx --results-directory artifacts/c07-fix/final -v:minimal
dotnet format AspectN.sln --verify-no-changes --no-restore --verbosity quiet
pwsh -NoProfile -File eng/generate-knowledge.ps1
pwsh -NoProfile -File eng/generate-knowledge.ps1 -Check
pwsh -NoProfile -File eng/verify-documentation.ps1
git diff --check
```

完整 Release 编译零警告、零错误；完整矩阵 2100 项全部通过、零跳过，包含 net472 及 net10.0 宿主。格式、知识库生成与派生一致性、文档检查和 `git diff --check` 均通过；文档校验为 17 条能力、112 条场景、10 个使用章节，零错误、零警告。日志在本地忽略目录 `artifacts/c07-fix/`，回归源码可独立重跑。

## 独立复核

按 `requesting-code-review` 技能由只读审查者 `/root/c07_array_review` 于 2026-10-21 复核。其确认最小回退及原模块身份优先正确，文档没有扩大真实 PE、H1 或 D01 结论；15 项证据文件哈希全部一致，两份红灯各 9 项 NRE/21 通过，绿灯各 30/30，8 份完整矩阵合计 2100/2100。无 Critical、Important 或 Minor 发现，结论为可以提交。

## 知识库与证据适用性

受影响能力为 `USE-ARRAY-001/S5、S6`、`TYPE-NOMINAL-001/S2、S11`、`CROSS-AVAILABILITY-001/S2、S5`；盘点为 `INV-0003、INV-0004、INV-0015`。原 USE-ARRAY/S1 的同 IL fixture 范围不扩写为本次公共引用输入，另建 S5/S6 区分普通 Cecil 与 Fody。

同步数组/可用性说明、用户限制章节、能力条目及派生目录；没有新增或更改 N/A。[FND-20261021-004](findings/FND-20261021-004.md) 对应 C07，[EV-20261021-004](evidence/EV-20261021-004.json) 保存当前代码哈希、环境、红绿灯与完整矩阵。

旧证据保留原有范围，完整矩阵回归不自动扩大后端/版本承诺。本次不处理 D01 直接特性原生超限通道裁决、不修改 H1、不证明任意损坏元数据恢复能力或真实 PE 自然无 Module 的来源。
