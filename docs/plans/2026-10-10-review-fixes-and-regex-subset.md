# 审查修复与正则子集

**目标：** 修复五项确认发现，实施 2026-10-10 批准的正则子集。

**架构：** 中立公开签名不变。Reflection 保留弱原生关联，约束查询上下文与声明身份分开。正则主体保留分隔语法，使用小特性白名单及编译诊断；.NET 核验语法，并在既有有限超时下匹配。

**技术栈：** C#、.NET Standard 2.0/.NET 10、xUnit、Mono.Cecil、Roslyn、Reflection。

在现有检出执行，保留未跟踪文件和相邻仓库。不包括 worktree、推送、包发布或 Rougamo 集成。

## 1. 适配器引用分类

- 添加 [In] ref、[In, Out] ref、普通 ref、out、in、ref readonly 一致性用例。
- 修改 CecilRefKindDetector/ReflectionSignatureMapper 前观察匹配与映射分类失败。
- 区分互操作方向与 readonly 标记，保留普通仅出方向的分类。
- 运行聚焦一致性及 Cecil/Reflection/Fody 套件。

## 2. Reflection 约束上下文与继承分支

- MethodGenericOwnerIdentityTests 验证 `Owner<string>`/`Owner<int>` 映射方法的 Reflection 关系，含缓存复用及传递约束。
- 方法形参使用位置保留上下文特定原生关联，不改变声明身份、不强保留可回收程序集。
- 用 MetadataLoadContext 添加缺失接口依赖 PE 继承闭包场景；证明已知基类可匹配，没有已知匹配时失败仍可见。
- 关系遍历在单个元数据失败后继续已知分支。
- 运行聚焦一致性及可回收生命周期测试。

## 3. 开放构造签名的位置命名

- 真实 Reflection 输入添加 `Container<List<T>>`、重复形参、嵌套/数组实参正则测试。
- 修改 GenericParameterNamer 前观察 AN3001。
- 收集嵌套泛型形参使用，按确定性为不同声明命名，保留已有声明槽命名。
- 运行正则、身份和适配器匹配测试。

## 4. 受限正则语言

- 编译拒绝注释、全部内联选项、命名/平衡/条件/原子分组、环视、反向引用、嵌套/相减字符类及类内未转义字面括号。
- 接受字面/转义、基础字符类/范围、普通/非捕获分组、分支、普通量词（含懒惰量词）、锚点，以及看似禁止结构的字面文本。
- 主体扫描简化为转义、普通字符类和分组深度状态。匹配前报告不支持特性，完整语法由 .NET 核验。
- 注释/字符类相减的旧正向测试改为拒绝覆盖，保留超时、取消、恢复和原文保留测试。
- 更新诊断 API 基线、语言规范、设计决策、迁移及当前比较文档。

## 5. 验证与独立审查

各改动先聚焦红/绿测试，再运行：

```powershell
dotnet restore AspectN.sln
dotnet build AspectN.sln -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test AspectN.sln -c Release --no-build --no-restore -m:1 -nr:false
dotnet format AspectN.sln --verify-no-changes --no-restore
powershell -NoProfile -File eng/verify-packages.ps1
```

实现后使用独立代码审查子代理。核验五个原始探针及负向对照，解决确认发现，汇总最终差异和证据。

## 执行结果（2026-10-10）

五项全部修复，生产改动前观察到回归失败。独立适配器、核心和发布/文档审查发现的嵌套元数预算、外部泛型声明者边界已纠正，最终审查通过。

最终验证：

- 还原成功，Release 解决方案构建零警告/错误，含 netstandard2.0、net10.0 和 net472 测试宿主。
- 完整测试 1,442 通过，零失败/跳过：核心 860、Cecil 130、Reflection 97、Roslyn 22、一致性 188、兼容 net472 6/net10.0 7、Cecil.Fody 132。
- dotnet format AspectN.sln --verify-no-changes --no-restore 和 git diff --check 通过。
- eng/verify-packages.ps1 验证五个本地包、两个依赖组及十个使用方构建/运行，版本 0.0.0-validation-e7d058e64459。
- 可回收生命周期测试在持有映射器和中立签名时验证卸载，以及后续 Unknown/AN3001 关系。

接受子集在规范第 15 节 中为正式依据。不支持特性编译期 AN2012，无效 .NET 语法编译期 AN3002，超时仍 Unknown/AN3003。生产包依赖不变，MetadataLoadContext 为私有测试依赖。
