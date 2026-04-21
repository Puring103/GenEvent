# GenEvent 已配置事件 Fluent 管线实施计划

## 目标

基于已确认的“已配置事件 Fluent 管线”设计，本计划将实现工作拆解为可执行步骤，目标是在尽量少影响已有用户代码的前提下，移除当前 `PublishConfig<TEvent>` 的静态暂存模型。

本计划聚焦以下结果：

- 用 `ConfiguredEvent<TEvent>` 承载单次发布配置；
- 保留绝大多数现有 fluent 调用形态；
- 删除 `PublishConfig<TEvent>.Setting`、`TakeForPublish()`、`DiscardPendingSetting()`；
- 保持同步/异步发布、过滤、取消传播、嵌套发布的现有业务语义；
- 让 README、测试和 Unity 镜像与新的发布模型保持一致。

## 范围

### 本次纳入

- 引入 `ConfiguredEvent<TEvent>`；
- 重构 fluent 发布扩展方法；
- 收敛 `PublishConfig<TEvent>` 职责；
- 移除静态暂存相关 API；
- 更新运行时、测试、README 和 Unity Runtime 镜像；
- 完整验证 .NET 运行时与 Source Generator 同步结果。

### 本次不纳入

- 并发线程安全改造；
- 订阅注册表结构调整；
- 新增新的业务功能型 API；
- 为旧 fluent 形态保留长期兼容层；
- 基于 analyzer 的自动迁移提示；
- 对外发布脚本或版本自动化。

## 关键实现决策

### 1. 包装类型采用轻量值类型

新增：

- `ConfiguredEvent<TGenEvent>`

推荐实现为轻量 `struct`，包含：

- `TGenEvent Event`
- `PublishConfig<TGenEvent> Config`

原因：

- 保持与事件值类型一致的轻量语义；
- 避免为每次 fluent 配置再引入额外托管包装对象；
- 让 `var evt = new DamageEvent().Cancelable();` 这类常见用法保持自然。

### 2. 第一层 fluent 入口继续挂在 `TEvent` 上

为了最小化调用方改动，保留现有 fluent 入口，例如：

- `Cancelable()`
- `WithFilter(...)`
- `OnlySubscriber(...)`
- `ExcludeSubscriber(...)`
- `OnlySubscribers(...)`
- `ExcludeSubscribers(...)`
- `OnlyType<TEvent, TSubscriber>()`
- `ExcludeType<TEvent, TSubscriber>()`

这些入口不再写静态状态，而是：

- 创建 `ConfiguredEvent<TEvent>`
- 写入第一步配置
- 返回包装值

### 3. 第二层 fluent 入口挂在 `ConfiguredEvent<TEvent>` 上

后续链式配置全部转到 `ConfiguredEvent<TEvent>` 上继续累积。

这样可保持以下代码继续工作：

```csharp
new DamageEvent().Cancelable().Publish();
var evt = new DamageEvent().ExcludeSubscriber(target);
evt.Publish();
```

### 4. `PublishConfig<TEvent>` 第一版去掉池化

本阶段建议直接移除 `PublishConfig<TEvent>` 的对象池逻辑，而不是保留池化。

原因：

- 当前最大结构问题不是分配，而是生命周期边界不清；
- `ConfiguredEvent<TEvent>` 可能被创建后不发布，如果继续沿用池化，需要额外处理“未消费配置对象如何归还”的边界；
- 先拿到简单、稳定、可验证的模型，再决定是否需要按测量结果恢复池化，风险更低。

这是一个有意识的实现取舍：先优先正确性和语义清晰度，而不是过早保留复杂复用逻辑。

### 5. 默认发布路径仍走空配置

保留：

- `TEvent.Publish()`
- `TEvent.PublishAsync()`

但它们改为直接创建空配置对象，并把它显式下传到当前发布调用链，而不是从静态状态取配置。

### 6. 删除 `DiscardPendingSetting()`

由于不再存在“已暂存但未消费”的静态配置，本接口应彻底删除，包括：

- 主运行时实现；
- Unity Runtime 镜像；
- README 和 README_zh 文档说明；
- 对应测试。

## 实施阶段

### 阶段 1：引入已配置事件基础类型

#### 目标

先建立新的显式配置承载类型，为后续 fluent 管线迁移提供稳定落点。

#### 代码改动

新增文件建议：

- `src/GenEvent/src/ConfiguredEvent.cs`
- `src/GenEvent.Unity/Assets/Plugins/GenEvent/Runtime/ConfiguredEvent.cs`

实现内容：

- 定义 `ConfiguredEvent<TGenEvent>`；
- 提供内部构造能力；
- 暴露 `Event` 与 `Config` 读取入口；
- 保持类型职责单一，不把 fluent 逻辑直接塞进该类型本体。

#### 注意事项

- 类型名要足够明确，避免与生成代码中的事件类型混淆；
- 优先保持实现简单，不在该类型中塞入过多辅助逻辑。

### 阶段 2：重构 `PublishConfig<TEvent>`

#### 目标

将 `PublishConfig<TEvent>` 收敛为“单次发布配置对象”，完全切断与静态暂存的关系。

#### 代码改动

修改文件：

- [PublishConfig.cs](C:/wtl/WTL/Project/GenEvent/src/GenEvent/src/PublishConfig.cs)
- [PublishConfig.cs](C:/wtl/WTL/Project/GenEvent/src/GenEvent.Unity/Assets/Plugins/GenEvent/Runtime/PublishConfig.cs)

实现内容：

- 删除 `Setting`
- 删除 `TakeForPublish()`
- 删除 `DiscardPendingSetting()`
- 删除池化字段与相关方法
- 保留 `Cancelable`
- 保留 filter 列表
- 保留 `SetCancelable()`、`AddFilter(...)`、`IsFiltered(...)`
- 新增或保留一个明确的“清空配置”内部方法，仅服务单次发布对象的生命周期

#### 注意事项

- 如果某些调用链仍依赖“发布后清理配置”，要将清理逻辑改为显式针对当前配置实例，而不是回收给全局池；
- 主运行时和 Unity Runtime 必须同时更新，不允许阶段性分叉。

### 阶段 3：改造 fluent 扩展方法

#### 目标

将 fluent 入口从“写静态状态”切换为“创建或继续返回已配置事件”。

#### 代码改动

修改文件：

- [PublisherHelper.cs](C:/wtl/WTL/Project/GenEvent/src/GenEvent/src/PublisherHelper.cs)
- [PublisherHelper.cs](C:/wtl/WTL/Project/GenEvent/src/GenEvent.Unity/Assets/Plugins/GenEvent/Runtime/PublisherHelper.cs)

实现内容：

1. 保留 `TEvent` 上的第一层 fluent 扩展：

- 第一次调用时创建新的 `PublishConfig<TEvent>`
- 构造 `ConfiguredEvent<TEvent>`
- 写入第一项配置
- 返回 `ConfiguredEvent<TEvent>`

2. 为 `ConfiguredEvent<TEvent>` 增加第二层 fluent 扩展：

- `Cancelable()`
- `WithFilter(...)`
- `OnlySubscriber(...)`
- `ExcludeSubscriber(...)`
- `OnlySubscribers(...)`
- `ExcludeSubscribers(...)`
- `OnlyType<TEvent, TSubscriber>()`
- `ExcludeType<TEvent, TSubscriber>()`

3. 保留 `TEvent.Publish()` / `PublishAsync()`：

- 直接用空配置调用现有 publisher 路径

4. 新增 `ConfiguredEvent<TEvent>.Publish()` / `PublishAsync()` 对应入口：

- 使用包装值内的 `Config`
- 走与默认发布一致的核心调用链

#### 注意事项

- 要特别注意扩展方法重载解析，避免让编译器在 `TEvent` 与 `ConfiguredEvent<TEvent>` 的链式调用上出现歧义；
- filter 参数空值校验必须在重构后继续保留；
- 不要改变 `HasPublisher<TEvent>()` 等已存在诊断 API 的行为。

### 阶段 4：清理旧模型残留

#### 目标

彻底删除静态暂存模型的遗留接口和说明，避免新旧模型并存。

#### 代码改动

需要清理：

- `DiscardPendingSetting()` 的实现与引用
- 任何提到“暂存 setting”“take for publish”的注释
- README 中关于手动清除待发布配置的说明
- 对应测试

涉及文件：

- [PublishConfig.cs](C:/wtl/WTL/Project/GenEvent/src/GenEvent/src/PublishConfig.cs)
- [PublishConfig.cs](C:/wtl/WTL/Project/GenEvent/src/GenEvent.Unity/Assets/Plugins/GenEvent/Runtime/PublishConfig.cs)
- [README.md](C:/wtl/WTL/Project/GenEvent/README.md)
- [README_zh.md](C:/wtl/WTL/Project/GenEvent/README_zh.md)
- [RobustnessTests.cs](C:/wtl/WTL/Project/GenEvent/Tests/RobustnessTests.cs)

#### 注意事项

- 这是结构迁移，不建议保留过渡兼容 API；
- 如果实现过程中发现仓库内部仍有多个残留引用，应在同一阶段一次性清干净。

### 阶段 5：测试迁移与补齐

#### 目标

用测试锁定新的显式配置模型，确保行为保持，结构切换可验证。

#### 建议测试改动

重点检查或新增：

1. 默认发布路径

- `TEvent.Publish()` 在无 fluent 配置时行为不变
- `TEvent.PublishAsync()` 在无 fluent 配置时行为不变

2. 已配置事件路径

- `new TestEventA { Value = 1 }.Cancelable().Publish()` 正常
- `var evt = new TestEventA { Value = 1 }.ExcludeSubscriber(sub); evt.Publish();` 正常
- `var evt = new TestEventA { Value = 1 }.OnlySubscriber(sub); evt.Publish();` 正常

3. 语义回归

- 过滤逻辑不变
- 取消传播逻辑不变
- 异步发布逻辑不变
- 嵌套发布配置隔离仍然成立
- 异步嵌套发布配置隔离仍然成立

4. 残留清理

- 删除 `DiscardPendingSetting()` 相关测试
- 删除任何依赖静态 Setting 语义的测试假设

建议重点修改文件：

- [FilterTests.cs](C:/wtl/WTL/Project/GenEvent/Tests/FilterTests.cs)
- [CancelAndPriorityTests.cs](C:/wtl/WTL/Project/GenEvent/Tests/CancelAndPriorityTests.cs)
- [AsyncTests.cs](C:/wtl/WTL/Project/GenEvent/Tests/AsyncTests.cs)
- [ExtensionAndSemanticsTests.cs](C:/wtl/WTL/Project/GenEvent/Tests/ExtensionAndSemanticsTests.cs)
- [RobustnessTests.cs](C:/wtl/WTL/Project/GenEvent/Tests/RobustnessTests.cs)
- [TestTypes.cs](C:/wtl/WTL/Project/GenEvent/Tests/TestTypes.cs)

#### 注意事项

- 重点不是补更多新功能测试，而是让旧行为在新结构下继续成立；
- 对于“breaking change 发生在静态类型层面”这一点，不需要写编译失败测试，但 README 必须明确说明。

### 阶段 6：文档更新

#### 目标

让 README 正式反映新的 fluent 类型语义，而不是继续描述旧的静态暂存模型。

#### 文档改动

修改文件：

- [README.md](C:/wtl/WTL/Project/GenEvent/README.md)
- [README_zh.md](C:/wtl/WTL/Project/GenEvent/README_zh.md)

更新内容：

- 明确 fluent 配置后返回的是“已配置发布对象”
- 说明大多数常见链式写法保持不变
- 补一条 breaking change 说明：
  - 如果显式声明为原始事件类型，需要改为 `var` 或 `ConfiguredEvent<TEvent>`
- 删除 `DiscardPendingSetting()` 的所有说明
- 如有必要，增加一个最小示例：

```csharp
var evt = new DamageEvent { Amount = 1 }.ExcludeSubscriber(target);
evt.Publish();
```

### 阶段 7：构建与同步验证

#### 目标

确认主运行时、测试项目、Unity Runtime 镜像和 Source Generator 同步产物全部一致。

#### 验证步骤

1. 清理并运行测试：

```powershell
dotnet clean Tests/Tests.csproj
dotnet test Tests/Tests.csproj
```

2. 构建主库：

```powershell
dotnet build src/GenEvent/GenEvent.csproj -c Release --no-restore
```

3. 构建 Source Generator：

```powershell
dotnet build src/GenEvent.SourceGenerator/GenEvent.SourceGenerator.csproj -c Release --no-restore
```

4. 抽查 Unity 同步结果：

- `src/GenEvent.Unity/Assets/Plugins/GenEvent/Runtime/`
- `src/GenEvent.Unity/Assets/Plugins/GenEvent/Editor/SourceGenerator/GenEvent.SourceGenerator.dll`

## 变更文件清单

### 预计新增

- `src/GenEvent/src/ConfiguredEvent.cs`
- `src/GenEvent.Unity/Assets/Plugins/GenEvent/Runtime/ConfiguredEvent.cs`

### 预计修改

- [PublishConfig.cs](C:/wtl/WTL/Project/GenEvent/src/GenEvent/src/PublishConfig.cs)
- [PublisherHelper.cs](C:/wtl/WTL/Project/GenEvent/src/GenEvent/src/PublisherHelper.cs)
- [README.md](C:/wtl/WTL/Project/GenEvent/README.md)
- [README_zh.md](C:/wtl/WTL/Project/GenEvent/README_zh.md)
- [FilterTests.cs](C:/wtl/WTL/Project/GenEvent/Tests/FilterTests.cs)
- [CancelAndPriorityTests.cs](C:/wtl/WTL/Project/GenEvent/Tests/CancelAndPriorityTests.cs)
- [AsyncTests.cs](C:/wtl/WTL/Project/GenEvent/Tests/AsyncTests.cs)
- [ExtensionAndSemanticsTests.cs](C:/wtl/WTL/Project/GenEvent/Tests/ExtensionAndSemanticsTests.cs)
- [RobustnessTests.cs](C:/wtl/WTL/Project/GenEvent/Tests/RobustnessTests.cs)
- [TestTypes.cs](C:/wtl/WTL/Project/GenEvent/Tests/TestTypes.cs)
- `src/GenEvent.Unity/Assets/Plugins/GenEvent/Runtime/PublishConfig.cs`
- `src/GenEvent.Unity/Assets/Plugins/GenEvent/Runtime/PublisherHelper.cs`
- `src/GenEvent.Unity/Assets/Plugins/GenEvent/Editor/SourceGenerator/GenEvent.SourceGenerator.dll`

## 建议执行顺序

1. 先引入 `ConfiguredEvent<TEvent>` 和最小已配置发布通路；
2. 再收敛 `PublishConfig<TEvent>`，把静态状态和池化一起删除；
3. 再改造 `PublisherHelper` 中的两层 fluent 扩展；
4. 然后清理 `DiscardPendingSetting()` 与旧注释；
5. 再迁移测试；
6. 最后更新 README，并跑完整构建验证。

这个顺序的好处是：先把新模型立住，再切 fluent 接口，最后再做清理和验证，不容易在新旧模型混合状态下丢失问题。

## 风险与回滚点

### 风险

- fluent 返回类型变化属于公开 API breaking change；
- 扩展方法重载如果设计不稳，容易让链式调用在某些泛型场景下失效；
- 如果在结构迁移过程中同时保留旧静态状态，容易产生“部分路径走新模型、部分路径走旧模型”的混合 bug；
- 去掉池化后可能带来额外分配，但这是可接受且可测量的第一阶段成本。

### 回滚点

如果实现过程中发现两层 fluent 扩展在编译器解析上存在明显问题，可优先保留：

- `ConfiguredEvent<TEvent>` 类型本身；
- `TEvent.Publish()` / `PublishAsync()` 默认路径稳定；
- `ConfiguredEvent<TEvent>.Publish()` / `PublishAsync()` 新路径可用；

然后再重新评估是否需要把第一层 fluent 入口改为更显式的 `Configure()` 方案。

## 完成标准

当以下条件全部满足时，本实施计划视为完成：

- `PublishConfig<TEvent>` 已不再持有静态暂存状态；
- `DiscardPendingSetting()` 已被删除；
- 主运行时与 Unity Runtime 镜像都完成同构更新；
- 常见 fluent 示例代码仍可工作；
- README 已清楚写出新的类型语义与 breaking change 边界；
- 测试通过且构建验证通过。
