# 单一语言 AspectN

用户于 2026-10-03 批准移除 Legacy 兼容层，实现在现有检出目录完成。该裁决取代更早计划中的双语言、对照一致性、兼容入口和 approved-differences 要求。

AspectN 直接实现现有 Standard 语法、绑定和匹配契约。语言选择 API 及 Cecil CompositeAccessibility 有意作为公共 API 破坏性变化移除。保留现有 Standard 输入和诊断，包括空槽、para 0/para *、AN4003/4004/4005。通用 MethodSignature.IsAsync 元数据仍可使用。

实施顺序：

1. 保留共享泛型能力和有价值的 Standard 回归。
2. 移除解析、绑定、求值、扩展上下文和编译缓存的模式参数/Legacy 分支，更新 PublicAPI 基线。
3. Cecil 直接映射声明可见性，保留属性访问器合并及 LeaveOpen 生命周期，更新链接 Fody 适配器。
4. 共享测试数据改名 MatchingFixture，退休 Legacy 测试/对照项目、语料、差异、锁文件、准备脚本及 CI 任务。
5. 更新语言/架构规范、设计决策、迁移和贡献指南。
6. 验证完整保留测试矩阵、格式及本地程序包。

验证：

```powershell
dotnet restore AspectN.sln
dotnet build AspectN.sln -c Release --no-restore
dotnet test AspectN.sln -c Release --no-build --no-restore
dotnet format AspectN.sln --verify-no-changes --no-restore
powershell -NoProfile -File eng/verify-packages.ps1
```

现有检出工作；保留未跟踪文件、冻结对照缓存及相邻仓库。Rougamo 集成、子模块移除、推送和破坏性大版本仍是未来工作。

验收完成：Release 构建零警告/错误，全部 837 保留测试通过（核心 551、Cecil 71、Fody 73、Reflection 62、Roslyn 19、一致性 59、.NET Framework 2）。还原、格式及五包/临时使用方验证成功。独立审查未发现生产语义回归；其建议的空泛型槽及位置敏感变量绑定已实现，并含声明形参匹配/不匹配案例。
