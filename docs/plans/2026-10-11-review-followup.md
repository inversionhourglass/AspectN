# 审查跟进实施计划

> **执行流程：** 使用 executing-plans 和 test-driven-development，每批完成前独立审查。

**目标：** 修复已接受的 REVIEW.md 发现，按任务本地提交并记录维护者裁决。

**架构：** 保留中立核心及适配器边界，修正各后端查询/诊断，保留构造器形状匹配，要求 !ref 邻接，移除测试产物布局假设。

**技术栈：** C#、xUnit、FsCheck、Mono.Cecil、Roslyn、Reflection、MSBuild、GitHub Actions。

## 范围与裁决

- M1 已由 07fea5c 修复，L2 已由 9309bcc 修复，不重复处理。
- H1：维护者决定不修复。映射输入必须含 ReturnType/ElementType。对不完整图保留异常，不引入 Unsupported/Unknown 回退，本任务不改异常文案。有真实程序集复现才重评估，不阻塞合并。
- L7：保留实例/静态构造器形状匹配，说明与字面声明类型成员路径的区别，并加匹配回归。
- L8：!ref 要求邻接。
- M5 是误报：NuGet 选择符号上传端点；简化重复枚举，不当作发布阻塞。
- L6 是哈希质量/分配改进；L15 是生成器不变量测试，与更强长输入模糊测试并存。
- 在现有检出工作，保留未跟踪 REVIEW.md/nul，不推送、发布或修改相邻仓库。

## 批次 1：适配器查询正确性（M2、M3、M4、L3、L4、L5）

文件：CecilTypeSystem.cs、CecilSignatureMapper.cs、Internal/CecilResolver.cs、RoslynTypeSystem.cs、ReflectionTypeSystem.cs 及聚焦适配器回归。

1. 添加包装特性、脱离关联泛型约束、不可读 Roslyn 兄弟分支/解析诊断、取消闭包快速路径、Reflection 不确定性保留回归。
2. 生产修改前聚焦运行并记录预期失败。
3. 数组/指针/函数指针/pinned/sentinel 不返回直接类型特性，保留有名泛型行为。
4. 形参无模块时，隐式泛型根使用活动 Cecil 模块；不解析脱离关联引用声明者；不可用声明保留 Unknown/AN3004。
5. Roslyn 每个队列节点捕获可恢复失败，保留已知兄弟分支，使用 AN3004，在闭包快速路径前检查取消。
6. Reflection 保留 `unresolved ?? MissingNativeReference(candidate)`。
7. 运行全部受影响套件，含同源码 Cecil.Fody，再提交。

## 批次 2：解析与语言契约（L1、L7、L8、L9）

文件：Lexer.cs、SelectorParser.cs、解析器/构造器回归、规范和设计决策。

1. 添加失败证明：无效 \\c 必须保留选择器分隔符和后续 OR，合法控制转义仍允许；拒绝带空白 ! ref。
2. 添加实例/静态构造器 tasklike 形状及字面声明类型成员路径正向测试。
3. 只对合法控制字符消费正则转义第三字符；检查成员引用否定跨度邻接。
4. 说明构造器形状、严格 !ref 和 Rougamo 匹配后过滤。
5. 核心及相关一致性测试，再提交。

## 批次 3：身份哈希（L6）

文件：Metadata/TypeIdentity.cs 及身份回归。

1. 添加反向包含链回归及预热分配检查。
2. 运行，证明当前哈希忽略顺序且发生分配。
3. 按链顺序乘法折叠局部哈希，不分配 Stack。
4. 身份/核心测试并提交。

## 批次 4：一致性与模糊覆盖（L10、L11、L12、L13、L15）

文件：ReviewFixture.cs、CustomAttributeRecordTests.cs、MappingDepthTests.cs、一致性项目、RoslynSignatureMapper.cs 及聚焦测试。

1. 添加 private/internal 方法映射回归，核验当前辅助方法失败。
2. 启用全部元数据导入及相关 Reflection 成员可见性标志。
3. 返回/声明类型特性比较扩展到 Roslyn PE；断言暴露隐藏 readonly 返回 CA，从 CA 支持的引用分类恢复，核验含/不含实际记录的 PE。
4. 构建时复制可运行探针及依赖到测试输出，从 AppContext.BaseDirectory 解析；默认和迁移输出均验证隔离案例。
5. 保留有界生成器不变量，增加有界数量长 Unicode 解析案例，断言终止和跨度。
6. 一致性/核心测试，再提交。

## 批次 5：发布流程与结果记录（M5、L14）

文件：.github/workflows/release.yml、docs/reviews/2026-10-11-review-followup.md。

1. 按标签并发，cancel-in-progress: false；只为发布枚举 nupkg，保留符号签名/验证。
2. 核验最终 YAML，记录全部处置、测试和提交证据。
3. 运行 dotnet build AspectN.sln -c Release --no-restore、dotnet test AspectN.sln -c Release --no-build --no-restore、dotnet format AspectN.sln --verify-no-changes --no-restore。
4. 按 requesting-code-review 独立审查，修复实质发现并重跑受影响检查。
5. 提交发布改动和完成记录，确认只剩原未跟踪文件。

当时用户已在会话批准实施及分批提交，执行无需额外设计批准或检查点暂停。
