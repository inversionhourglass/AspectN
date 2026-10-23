# RM-20 局部键文本写入探针

这是用于复现本轮有限观察的独立工程，不加入 AspectN.sln 或 CI，也不是产品零分配契约。原生产实现即通过全部检查；没有据此提交生产优化。[报告](../../2026-10-23-rm20-cecil-cache.md)说明环境和限制。

在仓库根执行：

```powershell
dotnet test docs/performance/reproductions/2026-10-23-rm20/TokenProbe.csproj -c Release -m:1 -nr:false -p:UseSharedCompilation=false --logger trx --results-directory artifacts/rm20/reproduction
```

探针反射定位实际 CecilTypeCacheKey.Append 和内部 MappingContext，在计量外生成直接调用的委托；有精确参数类型的重载时优先选择该重载，否则使用现有 object 入口。不在计量内调用 MethodInfo.Invoke 或创建反射参数数组。

预分配StringBuilder容量、预热后分别写入 bool true/false 和 int1/2/32各256次，断言本轮运行时的累计托管分配为0，同时检查文本。另覆盖布尔文本最大长度前一位/恰好边界，以及自定义负号文化下的Invariant整数格式，共12项。

该上下文与 AppendShape 的所有实际调用点不同；JIT和运行时变化可改变分配结果。它只用于评估候选局部优化，不根据一次通过证明所有生产路径或netstandard旧宿主均无装箱。未来若分配检查失败，应先核对环境和调用上下文，不能直接认定语言或映射契约违约。
