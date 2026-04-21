# GenEvent 运行时稳态分配消除设计

## 概述

本设计用于统一处理当前 GenEvent `.NET` 运行时热路径中的三类稳态分配：

- 发布路径每次创建 `PublishConfig<TEvent>` 带来的固定分配；
- fluent 内建过滤器因捕获对象产生的 delegate / closure 分配；
- `StartListening()` 返回 `SubscriptionHandle` 时因 `class + Action` 产生的分配。

目标不是追求“所有扩展入口绝对零分配”，而是为 GenEvent 建立一条默认热路径和内建 fluent 热路径上的零额外堆分配模型，同时保留 `WithFilter(Predicate<object>)` 作为开放扩展入口。

## 目标

- 消除默认 `Publish()` / `PublishAsync()` 的固定分配。
- 消除内建 fluent 过滤器路径的固定分配。
- 消除 `StartListening()` / `StartListening<TSubscriber, TEvent>()` 的句柄分配。
- 保持大多数现有调用方式不变。
- 允许 `WithFilter(Predicate<object>)` 继续存在，但明确它不是零分配保证路径。

## 非目标

- 不在本次设计中解决调用方自己创建 lambda / closure 的分配。
- 不重做线程安全模型。
- 不扩大到 Unity 侧之外的额外功能重构。
- 不在本次设计里重新引入静态暂存或对象池复杂生命周期。

## 当前分配来源

当前调查结果已经可以把 benchmark 中的主要分配与运行时对象对应起来：

- `Publish()` / `PublishAsync()` 的 `216 B`，来自每次 `new PublishConfig<TEvent>()`，且其内部立即创建 `List<Predicate<object>>(16)`。
- `OnlySubscriber(...)` / `ExcludeSubscriber(...)` 等路径的额外 `88 B`，来自捕获对象的 filter delegate / closure。
- `StartListening()` 的约 `120 B`，主要来自 `new SubscriptionHandle(...)` 和 `() => subscriber.StopListening()`。

这意味着如果仍然保留：

- `PublishConfig<TEvent>` 为引用类型；
- 内建 fluent filter 以 `Predicate<object>` 存储；
- `SubscriptionHandle` 以 `class + Action` 实现；

则这三类分配无法一起消除。

## 方案对比

### 方案 A：仅复用 `PublishConfig` / `List<Predicate<object>>`

优点：

- 改动最小；
- 可以消掉默认发布路径的一部分分配。

缺点：

- 无法消除捕获对象的 filter delegate 分配；
- 无法消除 `SubscriptionHandle` 分配；
- 生命周期管理容易重新引入复用边界复杂度。

### 方案 B：统一零分配热路径模型（推荐）

核心思路：

- 将发布配置改为值语义承载；
- 将内建 filter 改为结构化规则，而不是 delegate 列表；
- 将 `SubscriptionHandle` 改为值类型 token，而不是引用类型回调对象；
- 保留 `WithFilter(Predicate<object>)` 作为 fallback 扩展路径。

优点：

- 能一次性覆盖当前三类主要稳态分配；
- 设计边界清晰，行为可解释；
- benchmark 可直接反映默认热路径是否达到预期。

缺点：

- 涉及 runtime、source generator、Unity 镜像三处同步修改；
- `SubscriptionHandle` 将发生公开 API 语义变化。

### 方案 C：双轨保留旧模型，同时新增零分配专用 API

优点：

- 向后兼容压力最小。

缺点：

- API 会变得重复；
- 默认路径仍保留分配，基准和文档口径会变复杂；
- 后续维护成本高。

## 推荐设计

本次采用方案 B。

### 1. `PublishConfig<TEvent>` 改为值语义配置

`PublishConfig<TEvent>` 不再是持有 `List<Predicate<object>>` 的引用类型对象，而改为一个轻量值配置，直接存储：

- `Cancelable`
- 内建过滤规则描述
- 可选的外部 `Predicate<object>` fallback

默认 `Publish()` / `PublishAsync()` 直接传递默认配置值，不再分配空配置对象。

### 2. 内建 fluent filter 改为结构化规则

内建 fluent API：

- `OnlySubscriber`
- `ExcludeSubscriber`
- `OnlySubscribers`
- `ExcludeSubscribers`
- `OnlyType`
- `ExcludeType`

不再通过 `GenEventFilters.*` 创建 delegate，而是写入一组结构化过滤规则。发布时由运行时根据规则直接判定当前 subscriber 是否应被跳过。

`WithFilter(Predicate<object>)` 继续存在，但被明确视为“自定义扩展路径”：

- 允许分配；
- 不参与零分配承诺；
- 在文档和 benchmark 解释中单独说明。

### 3. `ConfiguredEvent<TEvent>` 继续保留

当前已落地的 `ConfiguredEvent<TEvent>` 模型继续保留，作为单次发布配置承载体。

本次只调整其内部持有的配置结构，不再回退到静态暂存或对象池回收模型。

### 4. `SubscriptionHandle` 改为 `struct`

`SubscriptionHandle` 从“持有 `Action stop` 的 class”改为“持有退订元数据的值类型 token”。

推荐字段：

- `object? subscriber`
- `BaseSubscriberRegistry? registry`
- `Type? eventType`

`Dispose()` 不再执行捕获 lambda，而是直接调用 registry 提供的 boxed 退订入口。

这样：

- `using var handle = subscriber.StartListening();` 不再创建堆对象；
- 重复 `Dispose()` 继续保持幂等 no-op 语义；
- 若调用方把它装箱成 `IDisposable`，仍可能产生装箱分配，这属于可接受边界。

### 5. `BaseSubscriberRegistry` 增加 boxed 退订入口

为支持值类型 `SubscriptionHandle` 在运行时完成退订，`BaseSubscriberRegistry` 需要新增 boxed 退订方法，供 handle 在 `Dispose()` 时调用。

source generator 为每个生成的 subscriber registry 产出：

- 全量退订入口；
- 按事件类型退订入口。

这样可以避免在 handle 中保存委托。

## API 与兼容性边界

### 保持不变

- `new Event().Publish()`
- `new Event().Cancelable().Publish()`
- `new Event().OnlySubscriber(target).Publish()`
- `using var handle = subscriber.StartListening();`

### 发生变化

- `SubscriptionHandle` 从 `class` 变为 `struct`
- `WithFilter(Predicate<object>)` 不再被视为零分配路径

### 接受的边界

- 调用方自己创建的捕获 lambda 分配不由库消除
- 将 `SubscriptionHandle` 赋给 `IDisposable` 时可能发生装箱

## benchmark 预期

本次设计完成后，应至少满足以下方向性结果：

- 默认 `Publish_NoSubscribers` 不再出现当前 `216 B` 固定分配；
- 默认 `Publish_Subscribers` / `PublishAsync_Subscribers` 不再出现当前固定配置分配；
- `OnlySubscriber` / `ExcludeSubscriber` 这类内建 fluent 路径不再额外出现当前 `88 B` filter 分配；
- `StartListening` 稳态路径不再出现当前约 `120 B` 的句柄分配；
- `WithFilter(Predicate<object>)` 如仍有分配，应在 benchmark 说明中明确为预期行为。

## 风险

### 1. `SubscriptionHandle` 语义变化

从引用类型改为值类型后，复制行为会改变，但由于退订本身是幂等 no-op，本次接受该语义变化。

### 2. 结构化过滤规则容易膨胀

如果内部规则建模过度泛化，反而会增加分支复杂度。本次应只覆盖当前已有 fluent 入口，不扩展新的过滤 DSL。

### 3. benchmark 需要重新设计对照组

当前 `ConfiguredEventPipeline` benchmark 与基础发布并非完全同口径。实现完成后需要重新整理基准，以便准确说明“零分配默认路径”和“自定义 filter fallback 路径”的差异。

## 实施范围建议

实现应只覆盖以下文件族：

- `src/GenEvent/src/*`
- `src/GenEvent.SourceGenerator/*`
- `src/GenEvent.Unity/Assets/Plugins/GenEvent/Runtime/*`
- `README.md`
- `README_zh.md`
- `Benchmarks/GenEvent.Benchmarks/*`

不额外扩展到新的功能模块。
