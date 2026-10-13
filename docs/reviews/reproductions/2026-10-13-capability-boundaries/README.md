# 能力边界审查探针

此项目保存2026-10-13分类审查的真实复现，不参与常规产品测试矩阵，也不改变产品实现。

在仓库根目录执行：

```powershell
dotnet restore docs/reviews/reproductions/2026-10-13-capability-boundaries/cecil/Probe.csproj
dotnet run --project docs/reviews/reproductions/2026-10-13-capability-boundaries/cecil/Probe.csproj -c Release -- artifacts/probe-cecil.json
dotnet restore docs/reviews/reproductions/2026-10-13-capability-boundaries/fody/Probe.csproj
dotnet run --project docs/reviews/reproductions/2026-10-13-capability-boundaries/fody/Probe.csproj -c Release -- artifacts/probe-fody.json
```

调用方须先确保输出目录存在。进程退出0表示探针执行完毕；JSON中的实际观察需与规范预期比较，不代表产品契约全部通过。出现probe_error时，该分组不构成有效产品复现。

两项目分别加载普通Cecil与Fody Cecil；输出保存实际程序集身份，不在同一进程混用两种身份。包含六组观察：

- 已取消的detached继承闭包查询，对照已关联候选及未取消输入。
- 类型/方法提供部分泛型实参，对照完整构造与开放定义。
- 仅返回类型不同的完整CLI泛型重载：先由CLR加载、枚举和实际调用，再比较形参身份。
- 同一已映射候选在原Reflection上下文和新上下文中的精确/通配/复合特性表达式。
- 同名IsReadOnlyAttribute读者类型对Roslyn PE真实返回特性身份的影响；直接枚举Reflection返回CA与Cecil原生CA构造器身份作为控制。
- partial async定义、实现与PE视图的IsAsync、任务类型形状和状态机特性记录。

预期、实际差异、Finding与证据范围见[本轮审查记录](../../2026-10-13-capability-evidence-and-review.md)。探针观测不能批准新的排除，也不能把未修复的问题记为fixed。
