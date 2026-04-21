# GenEvent .NET Benchmark 实施计划

## 目标

本计划用于为 GenEvent 增加一套标准化的 `.NET` 侧 benchmark 基础设施，目标不是做“大而全”的性能平台，而是先建立一组可重复、可扩展、可用于版本对比的基准面。

本计划聚焦以下结果：

- 引入独立的 `.NET benchmark` 项目；
- 使用 `BenchmarkDotNet` 跑核心同步、异步、生命周期场景；
- 明确隔离初始化和测试数据准备成本，不把非热路径噪音混入结果；
- 覆盖本次 `ConfiguredEvent<TEvent>` fluent 管线重构的关键路径；
- 为后续性能回归对比建立稳定入口。

## 范围

### 本次纳入

- 新增独立 benchmark 项目；
- 引入 `BenchmarkDotNet`；
- 建立统一 benchmark 测试类型与数据准备层；
- 增加同步发布、异步发布、订阅/反订阅的核心基准；
- 增加 `ConfiguredEvent<TEvent>` fluent 配置路径基准；
- 补充 benchmark 使用说明。

### 本次不纳入

- Unity 侧 benchmark 系统化改造；
- 跨框架对比（例如和其他事件库比较）；
- CI 中的性能门禁；
- 自动保存历史 benchmark 结果；
- 大量微基准或过细的内部方法基准；
- 多线程 benchmark。

## 关键实现决策

### 1. benchmark 使用独立项目，不混入 Tests

建议新增一个独立项目，例如：

- `Benchmarks/GenEvent.Benchmarks/GenEvent.Benchmarks.csproj`

原因：

- `BenchmarkDotNet` 与单元测试职责不同；
- 可以独立依赖和运行，不污染现有测试项目；
- 结果导出、参数化和运行配置都更清晰。

### 2. 第一版按“场景基准”组织，而不是按内部方法组织

第一版 benchmark 不去测 `PublishConfig.AddFilter()` 之类过细的方法，而是直接测用户真正关心的场景：

- 发布
- 异步发布
- 订阅
- 反订阅

原因：

- 项目卖点是发布和订阅整体路径，而不是某个内部方法的纳秒级差异；
- 更适合用来判断本次 fluent 管线重构是否有明显回退。

### 3. 单独覆盖 `ConfiguredEvent<TEvent>` 路径

新增基准时，必须有一条专门覆盖：

- `new Event().Cancelable().OnlySubscriber(x).Publish()`

或者等价的已配置发布路径。

原因：

- 这是本次结构改造后的新增关键热路径；
- 如果 benchmark 不覆盖它，就无法回答“显式配置模型的额外成本是多少”。

### 4. 初始化与注册准备不计入发布基准

`GenEventBootstrap.Init()`、订阅对象构建、HashSet 构建等准备工作必须放在：

- `GlobalSetup`
- 或 `IterationSetup`

不能直接放进 benchmark body。

原因：

- 否则结果会混入一次性准备成本；
- 无法反映真正的 steady-state 热路径性能。

### 5. 第一版优先看这 4 类指标

建议重点关注：

- `Mean`
- `Allocated`
- `Gen0`
- 参数变化下的趋势

不强求第一版就做复杂自定义诊断器。先让结果稳定、能比较，比指标花哨更重要。

## benchmark 场景设计

### 一、同步发布基准

建议建立 `PublishBenchmarks`，覆盖：

- `Publish_NoSubscribers`
- `Publish_OneSubscriber`
- `Publish_ManySubscribers`
- `Publish_Cancelable_StopsEarly`
- `Publish_WithFilter`
- `Publish_Nested`
- `Publish_ConfiguredEventPipeline`

说明：

- `Publish_ManySubscribers` 建议参数化订阅者数量，例如 `1 / 10 / 100`；
- `Publish_ConfiguredEventPipeline` 用来专门观察 fluent 配置链的成本；
- `Publish_Nested` 用来观察当前支持的嵌套发布语义在热路径上的代价。

### 二、异步发布基准

建议建立 `AsyncPublishBenchmarks`，覆盖：

- `PublishAsync_OneSubscriber`
- `PublishAsync_ManySubscribers`
- `PublishAsync_ConfiguredEventPipeline`

说明：

- 第一版不需要把所有 async 变体做全；
- 重点是建立一个稳定的 async 基线，并覆盖新 fluent 模型在 async 路径上的成本。

### 三、生命周期基准

建议建立 `LifecycleBenchmarks`，覆盖：

- `StartListening_OneSubscriber`
- `StopListening_OneSubscriber`
- `StartListening_ManySubscribers`
- `StopListening_ManySubscribers`

说明：

- 这里不需要测“创建对象 + 订阅”的总成本，而是尽量聚焦 GenEvent 自身的订阅注册开销；
- `ManySubscribers` 建议也做参数化。

## 测试数据与基准夹具设计

### 1. 独立 benchmark 测试类型

建议在 benchmark 项目中定义专用事件和订阅者类型，不复用 `Tests` 目录下的测试类型。

原因：

- benchmark 项目应该自包含；
- 避免把测试断言逻辑、测试辅助状态、NUnit 依赖带入 benchmark；
- 更容易控制数据规模和最小噪音。

### 2. 参数化设计

建议至少参数化：

- 订阅者数量：`1 / 10 / 100`

必要时后续再扩：

- 过滤器数量
- 异步订阅者比例

第一版不要过度参数化，否则结果矩阵会膨胀，反而不利于阅读。

### 3. 状态隔离

需要确保：

- 每个 benchmark 类在 setup 中清理静态发布器/订阅注册状态；
- benchmark 间不共享脏状态；
- 不把前一个 benchmark 的订阅残留到下一个 benchmark。

如果现有测试里的 `TestRuntimeState` 足够通用，可参考其思路，但 benchmark 项目不要直接依赖测试项目。

## 项目结构建议

建议新增目录：

- `Benchmarks/GenEvent.Benchmarks/`

建议文件：

- `GenEvent.Benchmarks.csproj`
- `Program.cs`
- `Benchmarks/PublishBenchmarks.cs`
- `Benchmarks/AsyncPublishBenchmarks.cs`
- `Benchmarks/LifecycleBenchmarks.cs`
- `Fixtures/BenchmarkRuntimeState.cs`
- `Fixtures/BenchmarkTypes.cs`

这样可以把：

- 基准入口
- 场景类
- 运行时清理
- benchmark 专用类型

分开管理，避免单文件过大。

## 文档与使用方式

### 文档更新

建议至少更新：

- `README.md`
- `README_zh.md`

补充一节简短说明：

- benchmark 项目位置；
- 运行命令；
- 建议用 `Release` 运行；
- 说明 benchmark 结果用于趋势对比，不应直接拿不同机器的绝对值横向比较。

### 运行命令

建议文档中给出最小命令：

```powershell
dotnet run -c Release --project Benchmarks/GenEvent.Benchmarks/GenEvent.Benchmarks.csproj
```

必要时补一个过滤示例：

```powershell
dotnet run -c Release --project Benchmarks/GenEvent.Benchmarks/GenEvent.Benchmarks.csproj -- --filter *PublishBenchmarks*
```

## 实施阶段

### 阶段 1：建立 benchmark 项目骨架

#### 目标

先把 benchmark 项目结构和运行入口搭起来，确保可以独立执行。

#### 代码改动

新增：

- `Benchmarks/GenEvent.Benchmarks/GenEvent.Benchmarks.csproj`
- `Benchmarks/GenEvent.Benchmarks/Program.cs`

实现内容：

- 引入 `BenchmarkDotNet`
- 引用 `src/GenEvent/GenEvent.csproj`
- 配置 benchmark 入口

### 阶段 2：补 benchmark 专用测试类型与运行时清理

#### 目标

建立 benchmark 专用事件、订阅者和静态状态清理机制。

#### 代码改动

新增：

- `Fixtures/BenchmarkTypes.cs`
- `Fixtures/BenchmarkRuntimeState.cs`

实现内容：

- 定义同步/异步 benchmark 事件与订阅者；
- 提供 benchmark 间静态状态清理；
- 提供订阅者批量准备辅助。

### 阶段 3：补同步发布基准

#### 目标

优先拿到同步热路径基线。

#### 代码改动

新增：

- `Benchmarks/PublishBenchmarks.cs`

实现内容：

- `Publish_NoSubscribers`
- `Publish_OneSubscriber`
- `Publish_ManySubscribers`
- `Publish_Cancelable_StopsEarly`
- `Publish_WithFilter`
- `Publish_Nested`
- `Publish_ConfiguredEventPipeline`

### 阶段 4：补异步发布基准

#### 目标

建立 async 基线，并覆盖显式 fluent 配置在 async 路径上的成本。

#### 代码改动

新增：

- `Benchmarks/AsyncPublishBenchmarks.cs`

实现内容：

- `PublishAsync_OneSubscriber`
- `PublishAsync_ManySubscribers`
- `PublishAsync_ConfiguredEventPipeline`

### 阶段 5：补生命周期基准

#### 目标

评估订阅注册与反注册成本。

#### 代码改动

新增：

- `Benchmarks/LifecycleBenchmarks.cs`

实现内容：

- `StartListening_OneSubscriber`
- `StopListening_OneSubscriber`
- `StartListening_ManySubscribers`
- `StopListening_ManySubscribers`

### 阶段 6：文档与验证

#### 目标

让 benchmark 可以被仓库内其他开发者直接运行，并确认基准项目工作正常。

#### 文档改动

修改：

- `README.md`
- `README_zh.md`

#### 验证步骤

1. 运行 benchmark 项目，确认可执行：

```powershell
dotnet run -c Release --project Benchmarks/GenEvent.Benchmarks/GenEvent.Benchmarks.csproj
```

2. 至少执行一次过滤运行，确认单类筛选可用：

```powershell
dotnet run -c Release --project Benchmarks/GenEvent.Benchmarks/GenEvent.Benchmarks.csproj -- --filter *PublishBenchmarks*
```

3. 确认 benchmark 输出包含：

- 平均耗时
- 分配信息
- 参数化场景结果

## 风险与注意事项

### 风险

- benchmark 很容易意外测到初始化、构造或清理噪音，而不是热路径本身；
- 如果复用测试项目类型，容易引入不必要依赖和状态污染；
- async benchmark 如果写法不稳，可能测到调度噪音而不是 GenEvent 路径本身；
- 参数过多会让第一版结果难读。

### 注意事项

- 第一版目标是“建立可靠基线”，不是“追求覆盖所有可能场景”；
- benchmark 输出更适合做同机、同配置、同版本序列的趋势对比；
- 不建议在第一版就把 benchmark 接进 CI 阻塞流程。

## 回滚点

如果实施过程中发现 async benchmark 显著增加复杂度，可优先保留：

- benchmark 项目骨架
- 同步发布基准
- 生命周期基准

然后将 async benchmark 延后到下一轮。

这样仍然可以先完成最有价值的性能基线建设。

## 完成标准

当以下条件全部满足时，本计划视为完成：

- 仓库中存在独立可运行的 `.NET benchmark` 项目；
- 已覆盖同步发布、异步发布、订阅/反订阅三类核心场景；
- 已包含 `ConfiguredEvent<TEvent>` fluent 管线路径 benchmark；
- README 中已有 benchmark 运行说明；
- benchmark 项目可以在 `Release` 下成功执行并输出结果。
