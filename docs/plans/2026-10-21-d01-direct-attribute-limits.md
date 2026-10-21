# D01 直接特性查询超限通道统一计划

**维护者决定：** 2026-10-21 用户确认“统一返回 Unknown 吧”。适用于 `HasDirectAttribute` 内部映射原生特性类型遇到的深度/元数上限，保留 AN3005/AN3006。显式调用原生 Mapper 仍抛 `AspectNLimitException`。

## 1. 失败证据

- 先新增 Cecil/Fody 共享回归及同源真实 C# 发射 PE 的四视图一致性回归。
- 覆盖类型深度 64/65 及原复现的 67 层，泛型元数 64/65，已知空列表及取消控制。
- 对照显式映射仍抛异常；记录旧 Cecil 查询外抛和其他后端的 Unknown。

## 2. 最小实现及契约

- 仅在 Cecil `HasDirectAttribute` 读取/映射直接记录的边界捕获 `AspectNLimitException`，返回携带原诊断的 Unknown；Fody 复用同一源码。
- 不修改通用元数据恢复、原生映射器、取消和生命周期通道；不改写为 AN3004。
- 同步公开接口 XML、语言资源限制、可用性专题、用户及后端接入指南。
- 影响 ATTR-TARGETS-001 的新增原生直接查询场景、CROSS-RESOURCES-001/S2–S5、CROSS-AVAILABILITY-001 的阶段通道说明及有关盘点；保留旧证据范围。

## 3. 验证与交付

- 聚焦测试、完整 Release 构建/矩阵、格式、知识库生成及 -Check、文档校验和差异检查。
- 按 requesting-code-review 独立只读复核，新增当前 Finding 与证据；不修改历史综合报告。
- 在现有检出目录单独提交，不推送，保留既有未跟踪文件。
