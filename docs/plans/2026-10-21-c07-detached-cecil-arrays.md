# C07 游离 Cecil 数组关系修复计划

**目标：** 数组无所属 Module 时使用当前 Cecil 上下文合成固定关系，消除 IsAssignableTo/GetInheritanceClosure 的空引用异常，并单独提交。

**架构：** `CecilResolver.EnumerateArrayRelations` 使用 `array.Module ?? _mapper.Module`，原有 Module 优先。不扩大异常捕获，不解析元素以确定固定数组关系，不改变数组形状、泛型集合接口及未知事实规则。

## 1. 取得失败证据

- 新增 `tests/AspectN.Cecil.Tests/CecilDetachedArrayTests.cs`，由 Fody 项目现有规则共享编译、分别执行。
- 覆盖无 Module、未解析元素引用、正常元素；向量、非 SZ 一维及多维形状。
- 分别检查固定关系、完整闭包、仅向量实现的泛型集合接口、元素严格一致、原生 Module 优先及取消。
- 记录旧实现实际 NRE 和通过的控制用例，再改生产代码。

## 2. 最小修复与资料同步

- 只修改数组关系合成的上下文选择，保留泛型形参已有同类回退。
- 同步 USE-ARRAY-001、TYPE-NOMINAL-001、CROSS-AVAILABILITY-001 的具体 Cecil/Fody 输入及盘点；用户数组/可用性说明与当前证据一并更新。
- 不修改历史报告、H1 裁决、D01 原生超限通道或 N/A。

## 3. 验证与提交

- 聚焦测试绿灯后执行完整 Release 编译、完整矩阵、格式、知识库生成/-Check、文档校验和 diff 检查。
- 按 requesting-code-review 做独立只读复核；证据限定公共构造引用，不声称真实 PE 自然丢失 Module。
- 在现有检出目录单独提交，不推送，保留既有未跟踪文件。
