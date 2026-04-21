# GenEvent 运行时稳态分配消除实施计划

## 目标

本计划用于落地 `docs/superpowers/specs/2026-04-21-runtime-allocation-elimination-design.md` 中确认的方案 B：统一处理 GenEvent 当前热路径中的三类稳态分配。

本次实施聚焦以下结果：

- 默认 `Publish()` / `PublishAsync()` 不再为每次发布创建堆对象；
- 内建 fluent filter 路径不再为每次发布创建 filter delegate / closure；
- `StartListening()` / `StartListening<TSubscriber, TEvent>()` 不再为句柄与回调额外分配；
- `WithFilter(Predicate<object>)` 继续保留，但被清晰定义为 fallback 扩展路径；
- benchmark、测试与 README 能准确反映新的分配边界。

## 范围

### 本次纳入

- 重构 `PublishConfig<TEvent>` 为值语义配置；
- 将内建 fluent filter 从 delegate 模型改为结构化规则；
- 保留 `ConfiguredEvent<TEvent>`，但切换其内部配置承载方式；
- 将 `SubscriptionHandle` 从 `class` 改为 `struct` token；
- 为 `BaseSubscriberRegistry` 和 source generator 增加 boxed 退订入口；
- 同步更新 Unity 运行时镜像；
- 补充 / 调整测试与 benchmark；
- 更新 README 与 README_zh 中的分配口径说明。

### 本次不纳入

- 不处理调用方自己创建闭包带来的分配；
- 不重做线程安全语义；
- 不新增新的公开 fluent API；
- 不引入复杂对象池或重新回到静态暂存模型；
- 不把此轮扩展为大规模架构清理。

## 关键实现决策

### 1. 先锁定零分配默认路径，再保留 `WithFilter` fallback

默认发布路径和内建 fluent 路径是本次主要目标，应优先保证它们的 steady-state 零额外堆分配。

`WithFilter(Predicate<object>)` 继续保留，但只作为：

- 用户自定义过滤逻辑入口；
- 不保证零分配的扩展路径；
- benchmark 中需要单独解释的例外项。

这样可以避免为了兼容自定义 predicate 语义，把整个内建热路径继续绑在 `Predicate<object>` 模型上。

### 2. 不采用“仅复用 `List<Predicate<object>>`”的小修方案

仅复用 `PublishConfig` 或 `List<Predicate<object>>` 只能降低默认发布路径分配，不能一起解决：

- 捕获对象的 filter closure；
- `SubscriptionHandle + Action`；
- 文档口径中“零分配热路径”的一致性。

因此本次直接执行统一模型切换，不做中间态过渡。

### 3. `SubscriptionHandle` 保留名字，但改为 `struct`

本次接受 `SubscriptionHandle` 的公开语义变化，但不新增新名字。

原因：

- 现有调用方式大多可以原样保留；
- `using var handle = subscriber.StartListening();` 是主路径，迁移成本最低；
- 若改名为 `SubscriptionToken`，会把一次内部优化放大为不必要的 API 迁移。

### 4. `Dispose()` 继续依赖退订幂等语义

`SubscriptionHandle` 改为值类型后会天然具有复制语义。本次不额外为它引入引用计数或共享状态，而是继续依赖：

- `StopListening` / `UnRegister` 为幂等 no-op；
- 多次 `Dispose()` 不产生逻辑错误。

这能把句柄模型保持在最低复杂度。

### 5. source generator 与 runtime 必须同步修改

这次优化不能只改 runtime 手写代码，因为：

- `SubscriptionHandle.Dispose()` 需要 registry 提供 boxed 退订入口；
- 当前生成代码负责具体 subscriber/event 组合的注册与退订；
- benchmark 的 steady-state 分配也受生成代码中的调用方式影响。

因此必须把 source generator、`.NET runtime`、Unity 镜像视为一个原子改动集。

## 实施阶段

### 阶段 1：建立分配回归测试与基线

#### 目标

在重构前先锁定需要改善的分配现象，避免实现过程中失去对目标的约束。

#### 代码改动

新增或调整：

- `Tests/*Allocation*` 或现有测试文件中的分配回归测试；
- 需要时增加专用 benchmark fixture 辅助。

实现内容：

- 为默认 `Publish()` 建立最小分配断言基线；
- 为 `OnlySubscriber` / `ExcludeSubscriber` 建立内建 fluent 分配基线；
- 为 `StartListening()` 建立稳态分配基线；
- 明确哪些测试只验证“分配明显下降 / 归零趋势”，哪些 benchmark 用于最终数字证明。

#### 验证

- `dotnet test Tests/Tests.csproj`
- 记录当前 benchmark 结果作为对照，不在此阶段修改 benchmark 结构

### 阶段 2：重构 `PublishConfig<TEvent>` 为值语义配置

#### 目标

移除默认发布路径对 `new PublishConfig<TEvent>()` 的依赖。

#### 代码改动

重点文件：

- `src/GenEvent/src/PublishConfig.cs`
- `src/GenEvent/src/ConfiguredEvent.cs`
- `src/GenEvent/src/PublisherHelper.cs`
- Unity 对应镜像文件

实现内容：

- 将 `PublishConfig<TEvent>` 从引用类型配置改为轻量值配置；
- 去掉内部 `List<Predicate<object>>` 存储；
- 调整默认 `Publish()` / `PublishAsync()` 直接传递默认配置值；
- 调整 `ConfiguredEvent<TEvent>` 内部持有方式，使其适配新的配置结构；
- 保持现有 fluent 链式调用形态不变。

#### 验证

- `dotnet test Tests/Tests.csproj`
- `dotnet build src/GenEvent/GenEvent.csproj -c Release --no-restore`
- 运行最小 publish benchmark / 分配探针，确认 `216 B` 固定分配已消失

### 阶段 3：将内建 fluent filter 切换为结构化规则

#### 目标

消除 `OnlySubscriber` / `ExcludeSubscriber` 等内建 fluent 路径里的 delegate / closure 分配。

#### 代码改动

重点文件：

- `src/GenEvent/src/PublishConfig.cs`
- `src/GenEvent/src/PublisherHelper.cs`
- `src/GenEvent/src/GenEventFilters.cs`
- 相关测试文件
- Unity 对应镜像文件

实现内容：

- 定义最小必要的内部过滤规则表示；
- 覆盖当前已有内建 fluent API：
  - `OnlySubscriber`
  - `ExcludeSubscriber`
  - `OnlySubscribers`
  - `ExcludeSubscribers`
  - `OnlyType`
  - `ExcludeType`
- 让 runtime 在发布循环中根据规则直接判定过滤结果；
- 保留 `WithFilter(Predicate<object>)`，但将其明确归类为 custom fallback；
- 删除或收缩已不再必要的 `GenEventFilters` delegate 工具。

#### 验证

- `dotnet test Tests/Tests.csproj`
- 定向运行 filter 相关测试
- 定向运行 publish benchmark，确认内建 fluent 路径不再出现当前 `88 B` 额外分配

### 阶段 4：重构 `SubscriptionHandle` 与 registry 退订入口

#### 目标

移除 `StartListening()` 的句柄对象与捕获回调分配。

#### 代码改动

重点文件：

- `src/GenEvent/src/SubscriberHelper.cs`
- `src/GenEvent/src/Interface/BaseSubscriberRegistry.cs`
- `src/GenEvent.SourceGenerator/*`
- Unity 对应镜像文件

实现内容：

- 将 `SubscriptionHandle` 改为 `struct`；
- 移除其内部 `Action _stop` 模型；
- 增加 boxed 退订入口，让 handle 能直接通过 registry 完成退订；
- 更新 source generator，使每个 subscriber registry 生成：
  - 全量 boxed 退订入口；
  - 按事件类型 boxed 退订入口；
- 更新 `StartListening()` / `StartListening<TSubscriber, TEvent>()` 返回新的值类型 handle。

#### 验证

- `dotnet test Tests/Tests.csproj`
- 增加 / 通过 `SubscriptionHandle` 复制、重复 `Dispose()`、继承调用路径等回归测试；
- 定向运行 lifecycle benchmark，确认稳态 `StartListening` 分配显著下降或归零

### 阶段 5：补 benchmark、文档与最终口径

#### 目标

让 benchmark 和 README 能正确表达新的零分配边界，而不是沿用旧口径。

#### 代码改动

重点文件：

- `Benchmarks/GenEvent.Benchmarks/*`
- `README.md`
- `README_zh.md`

实现内容：

- 调整 benchmark 对照组，避免继续把“不等价目标数”的路径直接比较；
- 明确区分：
  - 默认零分配路径；
  - 内建 fluent 零分配路径；
  - `WithFilter(Predicate<object>)` fallback 路径；
- 更新 README 中“zero GC / benchmark interpretation”描述，使其与实际测量一致。

#### 验证

- `dotnet build Benchmarks/GenEvent.Benchmarks/GenEvent.Benchmarks.csproj -c Release`
- 运行完整 benchmark 或至少关键过滤 benchmark 子集
- 检查 README 示例命令与结果解释一致

## 测试策略

### 功能回归

必须覆盖：

- 默认 `Publish()` / `PublishAsync()` 行为保持不变；
- 所有现有 fluent 组合行为保持不变；
- `ConfiguredEvent<TEvent>` 中间变量路径保持不变；
- `SubscriptionHandle.Dispose()` 继续等价于退订；
- `SubscriptionHandle` 复制后的重复 `Dispose()` 不会破坏语义；
- 单事件订阅句柄路径保持不变。

### 分配回归

重点验证：

- 默认发布路径不再分配 `PublishConfig`；
- 内建 fluent 路径不再分配 filter closure；
- `StartListening()` 主路径不再分配句柄对象。

### 生成器回归

必须确保：

- 生成的 publisher / subscriber registry 编译通过；
- 继承订阅者、多事件订阅者、sync + async handler 组合不回退；
- Unity 镜像与 `.NET runtime` 结构保持一致。

## 风险与应对

### 1. `PublishConfig<TEvent>` 值语义后容易被误拷贝

应对：

- 保持配置结构轻量；
- 尽量通过 `ConfiguredEvent<TEvent>` 承载；
- 用测试覆盖链式配置累积和发布消费路径。

### 2. 结构化 filter 规则分支过多

应对：

- 仅支持当前已有 fluent 入口；
- 不设计泛化规则树；
- 若内部实现出现复杂组合，优先用“单规则槽位 + custom fallback”而不是引入通用解释器。

### 3. `SubscriptionHandle` 从 `class` 到 `struct` 的兼容性变化

应对：

- 在 README 和变更说明中写明；
- 用测试证明常见调用形态无需改动；
- 接受装箱到 `IDisposable` 时仍可能有分配这一边界。

## 完成标准

完成本计划时，应满足：

- `dotnet test Tests/Tests.csproj` 全绿；
- `dotnet build src/GenEvent/GenEvent.csproj -c Release --no-restore` 通过；
- `dotnet build src/GenEvent.SourceGenerator/GenEvent.SourceGenerator.csproj -c Release --no-restore` 通过；
- `dotnet build Benchmarks/GenEvent.Benchmarks/GenEvent.Benchmarks.csproj -c Release` 通过；
- 关键 benchmark 能证明：
  - 默认发布路径不再有当前固定分配；
  - 内建 fluent 路径不再有当前固定额外分配；
  - `StartListening` 主路径不再有当前句柄固定分配；
- README / README_zh 已同步新的分配边界说明。
