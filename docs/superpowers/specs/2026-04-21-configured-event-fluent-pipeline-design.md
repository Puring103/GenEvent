# GenEvent 已配置事件 Fluent 管线设计

## 概述

本设计文档定义 GenEvent 对发布链式配置模型的一次聚焦重构：移除当前 `PublishConfig<TEvent>` 的静态暂存状态，改为使用轻量包装类型承载单次发布配置。

目标不是扩展新功能，而是修正当前 fluent 发布配置的结构性问题：配置先写入静态状态，再由 `Publish()` / `PublishAsync()` 消费。这一模型带来了“配置已暂存但未发布”的残留风险，也迫使运行时暴露 `DiscardPendingSetting()` 之类的补救接口。

本次设计采用已确认的方案 A：在尽量少影响已有用户代码的前提下，引入包装类型，保留大多数现有链式调用形态，例如：

```csharp
new DamageEvent().Cancelable().Publish();
var evt = new DamageEvent().ExcludeSubscriber(target);
evt.Publish();
```

## 目标

- 移除按事件类型持有的 fluent 配置静态暂存状态。
- 保留大多数现有链式发布调用形态。
- 让单次发布配置成为显式值，而不是隐式全局状态。
- 删除因静态暂存而引入的补救型 API，例如 `DiscardPendingSetting()`。
- 保持嵌套发布、同步发布、异步发布和过滤/cancel 语义不变。

## 非目标

- 改写订阅注册表模型。
- 引入并发线程安全保证。
- 调整事件定义方式或订阅者模型。
- 变更 `Publish()` / `PublishAsync()` 的业务语义。
- 解决所有公开 API 一致性问题。
- 在本次设计中直接实现向后兼容适配层或多版本迁移工具。

## 当前问题

### 1. fluent 配置依赖静态暂存

当前 `Cancelable()`、`WithFilter()`、`OnlySubscriber()` 等 fluent API 会把配置写入 `PublishConfig<TEvent>.Setting`。直到 `Publish()` / `PublishAsync()` 被调用时，运行时才通过 `TakeForPublish()` 取走这份配置。

这意味着 fluent 调用和实际发布之间依赖一段隐式共享状态，而不是显式的单次发布对象。

### 2. “已配置但未发布”会留下残留状态

当调用方先执行：

```csharp
new DamageEvent().OnlySubscriber(target);
```

随后因为分支、异常或提前返回而没有执行发布，配置仍然停留在静态状态中。后续同事件类型的下一次 `Publish()` 可能错误继承这份配置。

`DiscardPendingSetting()` 只能作为补救手段，不能从结构上消除问题。

### 3. 类型语义不清晰

当前 fluent API 返回的仍然是 `TEvent`。从源码表面看，调用者拿到的像是“事件值”，但内部实际已经附带了一份隐藏配置。这会让 API 表意与运行时真实行为不一致。

## 设计方案

### 1. 引入 `ConfiguredEvent<TEvent>` 包装类型

新增一个轻量值类型：

- `ConfiguredEvent<TEvent>`

它至少持有两项数据：

- `TEvent Event`
- `PublishConfig<TEvent> Config`

这个类型代表“一个事件值 + 一份仅用于本次发布的配置”。它不依赖静态暂存，也不依赖全局可变状态。

### 2. fluent API 分为两层入口

#### 原始事件入口

保留现有挂在 `TEvent` 上的 fluent 扩展方法入口，例如：

- `Cancelable()`
- `WithFilter(...)`
- `OnlySubscriber(...)`
- `ExcludeSubscriber(...)`
- `OnlySubscribers(...)`
- `ExcludeSubscribers(...)`
- `OnlyType<TEvent, TSubscriber>()`
- `ExcludeType<TEvent, TSubscriber>()`

但这些入口的职责变为：

- 创建一个新的 `ConfiguredEvent<TEvent>`
- 在其中写入第一步配置
- 返回该包装值

这让大多数现有代码仍可保持原样。

#### 已配置事件入口

在 `ConfiguredEvent<TEvent>` 上提供相同语义的 fluent 扩展方法。后续链式配置都直接修改或返回新的包装值，而不是回写到任何静态状态。

### 3. 默认发布路径保持不变

保留：

- `TEvent.Publish()`
- `TEvent.PublishAsync()`

这两条路径继续表示“使用默认空配置发布”。

区别在于它们不再从 `PublishConfig<TEvent>.Setting` 中取配置，而是直接构造或获取一个空配置对象，并沿当前发布调用链下传。

### 4. 已配置发布路径改为显式传值

新增：

- `ConfiguredEvent<TEvent>.Publish()`
- `ConfiguredEvent<TEvent>.PublishAsync()`

或者通过扩展方法为 `ConfiguredEvent<TEvent>` 提供等价入口。

发布时直接使用包装值内部的 `Config`。发布完成后，如果 `PublishConfig<TEvent>` 仍保留池化复用，则在该路径内部完成回收；如果不保留池化，则释放逻辑直接消失。

### 5. `PublishConfig<TEvent>` 收敛为单次发布配置对象

`PublishConfig<TEvent>` 仍可保留为内部配置承载类型，但职责必须收敛。

保留职责：

- 保存 `Cancelable`
- 保存 subscriber filter 列表
- 提供 `IsFiltered(...)`
- 提供清理与可选的池化复用

移除职责：

- `Setting`
- `TakeForPublish()`
- `DiscardPendingSetting()`
- 与“当前事件类型待发布配置”相关的任何静态状态

### 6. 嵌套发布语义保持不变

嵌套发布应继续成立，但配置隔离方式从“交换静态 Setting”改为“每次发布各自持有自己的显式配置值”。

这意味着：

- 外层已配置发布不会污染内层新发布
- 内层 fluent 配置不会回写到外层
- 不再需要依赖当前静态 Setting 的替换/归还机制来保证重入安全

## API 兼容性

### 保持兼容的主要调用形态

以下典型写法应继续成立：

```csharp
new DamageEvent().Cancelable().Publish();
new DamageEvent().ExcludeSubscriber(this).Publish();

var evt = new DamageEvent().OnlySubscriber(target);
evt.Publish();
```

### 明确的 breaking change

以下类型假设将不再成立：

```csharp
DamageEvent evt = new DamageEvent().Cancelable();
```

原因是 fluent 配置后的返回值不再是原始 `DamageEvent`，而是 `ConfiguredEvent<DamageEvent>`。

本设计接受这一 breaking change，因为它只影响显式依赖“fluent 后仍是原始事件类型”的代码；相较之下，它换来了更清晰的类型语义和更小的整体迁移范围。

### 类型推断预期

以下写法仍应自然工作：

```csharp
var evt = new DamageEvent().Cancelable();
evt.Publish();
```

因此，在仓库内部与外部的多数示例代码中，实际修改量预计较小。

## 数据流

### 默认发布

1. 调用方创建 `TEvent`
2. 直接调用 `Publish()` / `PublishAsync()`
3. 运行时创建空配置
4. 将 `event + config` 传入现有 publisher 路径
5. 发布结束后清理该配置对象

### 已配置发布

1. 调用方创建 `TEvent`
2. 第一次 fluent 调用创建 `ConfiguredEvent<TEvent>`
3. 后续 fluent 调用继续在包装值上累积配置
4. 调用 `ConfiguredEvent<TEvent>.Publish()` / `PublishAsync()`
5. 将包装值内部的 `event + config` 传入现有 publisher 路径
6. 发布结束后清理该配置对象

## 错误处理

本次设计不改变现有初始化错误与异常传播契约。

保持不变的内容：

- 未初始化时，发布路径仍抛出清晰的 `InvalidOperationException`
- 订阅者内部抛出的异常仍沿当前路径传播
- filter 参数校验仍保留现有 `ArgumentNullException` 行为

本次变化只针对配置承载方式，不引入新的错误策略。

## 测试策略

需要新增或调整以下测试：

- `TEvent.Publish()` 默认空配置路径仍然正常
- `ConfiguredEvent<TEvent>.Publish()` 与 `PublishAsync()` 路径正常
- `Cancelable()`、`WithFilter()`、`OnlySubscriber()`、`ExcludeSubscriber()` 等组合行为保持不变
- `var evt = new TestEventA().ExcludeSubscriber(sub); evt.Publish();` 这类中间变量路径正常
- 嵌套发布配置隔离仍然成立
- 异步发布上的配置隔离仍然成立
- 显式类型 breaking change 不需要测试，但文档必须说明

需要删除或重写的测试：

- 任何依赖静态 `Setting`、`TakeForPublish()` 或 `DiscardPendingSetting()` 的测试

## 文档变更

需要更新：

- `README.md`
- `README_zh.md`

文档应明确：

- fluent 配置返回的是“已配置发布对象”，不再是原始事件值
- 大多数常见链式写法保持不变
- 如果调用方显式把 fluent 结果声明为原始事件类型，需要改为 `var` 或 `ConfiguredEvent<TEvent>`
- `DiscardPendingSetting()` 已移除，因为不再存在待消费的静态配置

## 风险

### 1. 公开 API 表面会发生类型层面的变化

虽然多数调用点可以不改，但 fluent 方法的返回类型变化仍属于公开 API 变更，需要在版本说明中明确写出。

### 2. 扩展方法重载容易出现歧义

如果 `TEvent` 和 `ConfiguredEvent<TEvent>` 两层扩展方法设计得不够清晰，可能造成重载解析混乱或链式调用断裂。实现阶段必须优先锁定最常见链路并用测试覆盖。

### 3. 池化与包装值的生命周期边界需要明确

如果保留 `PublishConfig<TEvent>` 池化，必须保证包装值在发布结束后不会继续被错误复用。必要时应优先接受少量分配增长，而不是引入隐蔽的生命周期 bug。

## 验收标准

满足以下条件时，本设计视为完成：

- `PublishConfig<TEvent>` 不再持有静态暂存状态
- `DiscardPendingSetting()` 被删除
- 大多数现有 fluent 调用示例无需改写即可继续使用
- `var evt = new DamageEvent().Cancelable(); evt.Publish();` 路径成立
- 嵌套发布与异步发布的配置隔离通过测试验证
- README 中清楚说明新的类型语义与 breaking change 边界

## 实现建议

建议按以下顺序落地：

1. 先引入 `ConfiguredEvent<TEvent>` 和最小发布通路
2. 再把第一层 fluent 入口从 `TEvent` 切到包装值创建
3. 再为 `ConfiguredEvent<TEvent>` 补齐 fluent 扩展
4. 最后移除静态 `Setting`、`TakeForPublish()`、`DiscardPendingSetting()`
5. 同步更新测试和 README

这样可以把风险集中在一条清晰迁移路径上，避免同时重写过多运行时细节。
