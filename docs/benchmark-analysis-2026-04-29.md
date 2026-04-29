# GenEvent .NET Benchmark 分析 - 2026-04-29

## 摘要

本次按完整默认 benchmark 集合重新运行，使用 `ShortRunJob`，不是为了强行压进 5 分钟，而是为了拿到与之前记录更可比的结果。

运行命令：

```powershell
Write-Output "0 1 2 3 4 5 6" | dotnet run -c Release --project Benchmarks\GenEvent.Benchmarks\GenEvent.Benchmarks.csproj
```

运行结果：

- Global total time: `00:04:51 (291.31 sec)`
- Executed benchmarks: `29`
- Job: `ShortRun`
- Iteration count: `3`
- Launch count: `1`
- Warmup count: `3`
- 最新日志：`BenchmarkDotNet.Artifacts/BenchmarkRun-20260429-132319.log`

主要结论：

- 优化后的默认同步发布 `Publish_Subscribers` 从 `28.53 ns / 41.99 ns` 降到 `11.37 ns / 15.50 ns`。
- 优化后的 `OnlySubscriber` configured pipeline 从 `36.69 ns / 139.57 ns` 降到 `27.81 ns / 26.73 ns`。
- 同步、异步、内建过滤、configured pipeline、cancelable、nested publish、lifecycle 仍为零分配。
- 自定义 predicate fallback 仍然每次操作分配 `96 B`，这是当前发布期唯一稳定出现的分配来源。
- `SubscriberCount=10` 时，GenEvent 默认发布 `15.50 ns`，已接近 multicast delegate `13.65 ns`，但仍明显高于直接/虚方法/委托数组循环。

## 本次优化内容

本次实现了：

- 发布期间订阅变更延迟应用：publish 过程中 `StartListening` / `StopListening` 不直接修改订阅列表，而是在最外层 publish 结束后统一应用。
- 生成的 publisher 改为 `BeginPublish()` / `EndPublish()` + `DirectSubscribers`，默认发布不再复制 snapshot。
- 旧的 `SnapshotPoolCapacity`、`SnapshotPool`、`TakeSubscribersSnapshot()`、`ReturnSubscribersSnapshot()` 已直接删除，不保留兼容入口。
- `PublishConfig` 增加默认配置和单个 `OnlySubscriber` 查询。
- `PublisherHelper` 增加默认发布 fast path 和单目标 `OnlySubscriber` fast path。
- `OnlySubscriber` fast path 修正为先按 `ReferenceEquals` 确认目标当前已注册，避免错误调用未订阅对象。
- Unity runtime 源码副本和 Unity SourceGenerator dll 已同步。

`publish-time snapshot copy` 原来的作用是让发布过程遍历一个稳定副本，避免发布中增删订阅者破坏当前遍历。现在改为 publish depth + pending mutation 后，发布期间原始 `SubscriberList` 不会被修改，因此可以直接遍历列表，省掉 snapshot 拷贝成本，同时保留“发布期间变更下一次生效”的语义。

删除旧 snapshot API 后，`src`、`Tests`、`Benchmarks` 中不再存在 `TakeSubscribersSnapshot` / `ReturnSubscribersSnapshot` / `SnapshotPool` / `SnapshotPoolCapacity` 引用。source generator 内部也改为 `PublishScope` 命名，避免继续暴露 snapshot 概念。

## 之前 vs 当前

下表的“之前”来自本文件前一版记录的 `ShortRunJob` 结果；“当前”来自 `BenchmarkRun-20260429-132319.log`。二者都是 `ShortRunJob`，更适合做方向性对比。

| Benchmark | SubscriberCount | 之前 Mean | 当前 Mean | 变化 |
| --- | ---: | ---: | ---: | ---: |
| `Publish_NoSubscribers` | - | 22.01 ns | 10.89 ns | 快 50.5% |
| `Publish_Subscribers` | 1 | 28.53 ns | 11.37 ns | 快 60.1% |
| `Publish_Subscribers` | 10 | 41.99 ns | 15.50 ns | 快 63.1% |
| `Publish_BuiltInFilter` | 1 | 33.47 ns | 22.53 ns | 快 32.7% |
| `Publish_BuiltInFilter` | 10 | 141.36 ns | 31.34 ns | 快 77.8% |
| `Publish_ConfiguredEventPipeline` | 1 | 36.69 ns | 27.81 ns | 快 24.2% |
| `Publish_ConfiguredEventPipeline` | 10 | 139.57 ns | 26.73 ns | 快 80.8% |
| `Publish_CustomPredicateFilter` | 1 | 37.16 ns | 23.92 ns | 快 35.6% |
| `Publish_CustomPredicateFilter` | 10 | 151.42 ns | 38.13 ns | 快 74.8% |
| `Publish_Cancelable_StopsEarly` | - | 37.26 ns | 23.01 ns | 快 38.2% |
| `Publish_Nested` | - | 43.93 ns | 17.44 ns | 快 60.3% |
| `PublishAsync_Subscribers` | 1 | 42.66 ns | 30.71 ns | 快 28.0% |
| `PublishAsync_Subscribers` | 10 | 181.07 ns | 81.45 ns | 快 55.0% |
| `PublishAsync_ConfiguredEventPipeline` | 1 | 63.83 ns | 43.55 ns | 快 31.8% |
| `PublishAsync_ConfiguredEventPipeline` | 10 | 163.70 ns | 42.31 ns | 快 74.2% |
| `PublishAsync_CustomPredicateFilter` | 1 | 60.77 ns | 40.32 ns | 快 33.7% |
| `PublishAsync_CustomPredicateFilter` | 10 | 199.48 ns | 93.46 ns | 快 53.1% |

解读：

- 默认发布收益主要来自去掉 snapshot copy 和默认 config fast path。
- `ConfiguredEventPipeline` 在 10 订阅者下收益最大，主要来自单个 `OnlySubscriber` fast path，避免扫描所有订阅者。
- `BuiltInFilter` 的 10 订阅者结果也明显变好，但 `ExcludeSubscriber` 仍然需要遍历全部订阅者，不是 O(1) 单目标投递。
- 自定义 predicate 虽然 CPU 也变快，但仍保留 `96 B` 分配；这是 API 形态带来的 fallback 成本。

## 当前完整结果

### Dispatch Baseline: C# 调用基线

| Method | SubscriberCount | Mean | Error | StdDev | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: |
| `DirectMethodCall_Loop` | 1 | 1.0385 ns | 0.4657 ns | 0.0255 ns | - |
| `VirtualMethodCall_Loop` | 1 | 2.1935 ns | 2.3236 ns | 0.1274 ns | - |
| `DelegateInvoke_Loop` | 1 | 1.7620 ns | 0.0933 ns | 0.0051 ns | - |
| `MulticastDelegateInvoke` | 1 | 0.2962 ns | 0.3917 ns | 0.0215 ns | - |
| `DirectMethodCall_Loop` | 10 | 7.1831 ns | 2.6488 ns | 0.1452 ns | - |
| `VirtualMethodCall_Loop` | 10 | 6.9153 ns | 1.0884 ns | 0.0597 ns | - |
| `DelegateInvoke_Loop` | 10 | 7.5742 ns | 0.5681 ns | 0.0311 ns | - |
| `MulticastDelegateInvoke` | 10 | 13.6515 ns | 5.1180 ns | 0.2805 ns | - |

这些基线只代表 C# 调用下限，不包含 GenEvent 的订阅注册、发布器查找、过滤、取消、嵌套发布等语义。

### Publish: 无订阅者

| Method | Mean | Error | StdDev | Allocated |
| --- | ---: | ---: | ---: | ---: |
| `Publish_NoSubscribers` | 10.89 ns | 1.409 ns | 0.077 ns | - |

### Publish: 订阅者与过滤

| Method | SubscriberCount | Mean | Error | StdDev | Gen0 | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `Publish_Subscribers` | 1 | 11.37 ns | 0.208 ns | 0.011 ns | - | - |
| `Publish_BuiltInFilter` | 1 | 22.53 ns | 1.291 ns | 0.071 ns | - | - |
| `Publish_ConfiguredEventPipeline` | 1 | 27.81 ns | 1.646 ns | 0.090 ns | - | - |
| `Publish_CustomPredicateFilter` | 1 | 23.92 ns | 7.895 ns | 0.433 ns | 0.0076 | 96 B |
| `Publish_Subscribers` | 10 | 15.50 ns | 2.271 ns | 0.124 ns | - | - |
| `Publish_BuiltInFilter` | 10 | 31.34 ns | 0.098 ns | 0.005 ns | - | - |
| `Publish_ConfiguredEventPipeline` | 10 | 26.73 ns | 19.709 ns | 1.080 ns | - | - |
| `Publish_CustomPredicateFilter` | 10 | 38.13 ns | 42.857 ns | 2.349 ns | 0.0076 | 96 B |

### Publish: 可取消路径

| Method | Mean | Error | StdDev | Allocated |
| --- | ---: | ---: | ---: | ---: |
| `Publish_Cancelable_StopsEarly` | 23.01 ns | 0.539 ns | 0.030 ns | - |

### Publish: 嵌套发布

| Method | Mean | Error | StdDev | Allocated |
| --- | ---: | ---: | ---: | ---: |
| `Publish_Nested` | 17.44 ns | 0.333 ns | 0.018 ns | - |

### Async Publish

| Method | SubscriberCount | Mean | Error | StdDev | Gen0 | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `PublishAsync_Subscribers` | 1 | 30.71 ns | 2.524 ns | 0.138 ns | - | - |
| `PublishAsync_ConfiguredEventPipeline` | 1 | 43.55 ns | 4.072 ns | 0.223 ns | - | - |
| `PublishAsync_CustomPredicateFilter` | 1 | 40.32 ns | 11.653 ns | 0.639 ns | 0.0076 | 96 B |
| `PublishAsync_Subscribers` | 10 | 81.45 ns | 85.656 ns | 4.695 ns | - | - |
| `PublishAsync_ConfiguredEventPipeline` | 10 | 42.31 ns | 3.395 ns | 0.186 ns | - | - |
| `PublishAsync_CustomPredicateFilter` | 10 | 93.46 ns | 5.098 ns | 0.279 ns | 0.0076 | 96 B |

`PublishAsync_ConfiguredEventPipeline` 在 10 订阅者下比 `PublishAsync_Subscribers` 更快，是因为 benchmark 用 `OnlySubscriber` 收窄到单个目标；这不是等价工作量对比。

### Lifecycle

| Method | SubscriberCount | Mean | Error | StdDev | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: |
| `StartListening_Subscribers` | 1 | 1.533 us | 7.373 us | 0.4041 us | - |
| `StopListening_Subscribers` | 1 | 1.317 us | 8.622 us | 0.4726 us | - |
| `StartListening_Subscribers` | 10 | 2.933 us | 9.362 us | 0.5132 us | - |
| `StopListening_Subscribers` | 10 | 2.500 us | 11.963 us | 0.6557 us | - |

Lifecycle 使用 `InvocationCount=1` 和 `UnrollFactor=1`，短跑下误差较大。这里主要看分配和数量级。

## 分配边界

当前分配边界：

- C# direct / virtual / delegate / multicast delegate 调用：零分配。
- 默认直接发布：零分配。
- 内建 fluent filter：零分配。
- Configured event pipeline：零分配。
- Cancelable publish：零分配。
- Nested publish：零分配。
- Lifecycle 操作：零分配。
- 自定义 predicate filter：`96 B/op`。

## 与直接调用、虚方法、委托调用的对比

`SubscriberCount=10` 时：

- 直接方法循环：`7.18 ns`
- 虚方法循环：`6.92 ns`
- 委托数组循环：`7.57 ns`
- multicast delegate：`13.65 ns`
- GenEvent `Publish_Subscribers`：`15.50 ns`

当前默认发布已经接近 multicast delegate 调用量级，但仍高于显式循环的直接/虚方法/委托数组调用。这个差异合理，因为 GenEvent 路径保留了发布器、订阅表、生命周期和运行时扩展语义。

## 性能建议

1. 保持默认 publish fast path 极简。

   默认配置下不应进入 filter、cancelable、custom predicate 分支。后续改动要避免把冷路径判断重新放回默认循环。

2. 继续优化结构化内建过滤，而不是 custom predicate。

   `OnlySubscriber` 已经有快路径。`ExcludeSubscriber`、`OnlySubscribers`、`ExcludeSubscribers` 仍可考虑结构化优化，但必须保持组合 filter 的旧语义。

3. 保持发布期间禁止立即修改订阅列表。

   直接遍历 `SubscriberList` 的前提是发布期间列表结构稳定。若允许发布中立即增删，会出现跳过订阅者、重复调用、索引错乱、嵌套 publish 语义不稳定等问题。

4. 自定义 predicate 的 `96 B` 应作为兼容 fallback 成本记录。

   如果要消除这部分分配，需要引入新的零分配 API 形态，而不是破坏现有 `Predicate<object>` 行为。

5. benchmark profile 可以分层。

   默认本地回归保留 `ShortRunJob`。如果需要发布级数字，可以增加显式 long/full profile，不要混在默认命令里。

## 追加优化轮次

在上述完整 benchmark 之后，又补了一轮正确性和结构化过滤优化：

- `GenEventRegistry` 的订阅索引改为引用相等 comparer，避免 subscriber 重写 `Equals/GetHashCode` 后两个不同实例被误判为同一订阅者。
- `EndPublish()` 增加防御：没有匹配 `BeginPublish()` 时抛 `InvalidOperationException`，避免 publish depth 被静默改坏。
- 补充发布中 mutation 与 `OnlySubscriber` 嵌套 publish 的交叉测试。
- 单规则内建过滤增加结构化快路径：`ExcludeSubscriber`、`OnlySubscribers`、`ExcludeSubscribers`、`OnlyType`、`ExcludeType`。
- `OnlySubscriber` membership 查询保留线性 `ReferenceEquals` 扫描。实测在小订阅者数量下，它比字典查询更适合这个热路径。

追加验证：

- `dotnet test Tests\Tests.csproj -c Release --filter "FilterTests|AsyncTests|AllocationRegressionTests|PublishMutationTests|CoreFlowTests|RegistryGuardTests"`：`66/66` 通过。
- `dotnet test Tests\Tests.csproj -c Release`：`147/147` 通过。
- `dotnet build Benchmarks\GenEvent.Benchmarks\GenEvent.Benchmarks.csproj -c Release`：0 warning / 0 error。

追加定向 benchmark：

```powershell
dotnet run -c Release --project Benchmarks\GenEvent.Benchmarks\GenEvent.Benchmarks.csproj -- --filter "*PublishSubscriberBenchmarks*"
```

最新 `PublishSubscriberBenchmarks` 结果：

| Method | SubscriberCount | Mean | Allocated |
| --- | ---: | ---: | ---: |
| `Publish_Subscribers` | 1 | 14.61 ns | - |
| `Publish_BuiltInFilter` | 1 | 24.51 ns | - |
| `Publish_ConfiguredEventPipeline` | 1 | 29.90 ns | - |
| `Publish_CustomPredicateFilter` | 1 | 26.04 ns | 96 B |
| `Publish_Subscribers` | 10 | 21.73 ns | - |
| `Publish_BuiltInFilter` | 10 | 27.58 ns | - |
| `Publish_ConfiguredEventPipeline` | 10 | 30.25 ns | - |
| `Publish_CustomPredicateFilter` | 10 | 38.65 ns | 96 B |

解读：

- `Publish_BuiltInFilter` 的 10 订阅者路径从上一轮完整 benchmark 的 `31.34 ns` 降到本轮定向 benchmark 的 `27.58 ns`，说明 `ExcludeSubscriber` 单规则快路径有效。
- `Publish_ConfiguredEventPipeline` 本轮为 `30.25 ns`，仍显著低于最初优化前 `139.57 ns`。该行对机器状态和短跑 profile 有一定波动，不应只用单次结果判断微小差异。
- 分配边界保持不变：内建路径零分配，自定义 predicate fallback 仍为 `96 B/op`。

## 来源 Artifacts

本分析使用的最新 BenchmarkDotNet 报告：

- `BenchmarkDotNet.Artifacts/results/GenEvent.Benchmarks.Benchmarks.AsyncPublishBenchmarks-report-github.md`
- `BenchmarkDotNet.Artifacts/results/GenEvent.Benchmarks.Benchmarks.DispatchBaselineBenchmarks-report-github.md`
- `BenchmarkDotNet.Artifacts/results/GenEvent.Benchmarks.Benchmarks.LifecycleBenchmarks-report-github.md`
- `BenchmarkDotNet.Artifacts/results/GenEvent.Benchmarks.Benchmarks.PublishBenchmarks-report-github.md`
- `BenchmarkDotNet.Artifacts/results/GenEvent.Benchmarks.Benchmarks.PublishCancelableBenchmarks-report-github.md`
- `BenchmarkDotNet.Artifacts/results/GenEvent.Benchmarks.Benchmarks.PublishNestedBenchmarks-report-github.md`
- `BenchmarkDotNet.Artifacts/results/GenEvent.Benchmarks.Benchmarks.PublishSubscriberBenchmarks-report-github.md`

最新完整运行日志：

- `BenchmarkDotNet.Artifacts/BenchmarkRun-20260429-132319.log`
