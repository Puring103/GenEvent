# GenEvent 稳定性过渡版本实施计划

## 目标

基于已确认的稳定性过渡版本设计，本计划将实现工作拆解为可执行步骤，覆盖代码改动、测试补充、文档更新和验证方式。

本计划的目标不是扩展功能面，而是完成以下收口：

- 初始化契约显式化；
- 未初始化失败从隐式/内部异常变为清晰契约错误；
- 当前线程模型正式落地；
- 增加最小可观测 API；
- 用测试锁定新的运行时语义；
- 保持 .NET 主运行时与 Unity 包镜像的一致性。

## 范围

### 本次纳入

- `GenEventBootstrap.Init()` 幂等化；
- `GenEventBootstrap.IsInitialized`；
- 发布路径的清晰初始化错误；
- 订阅路径的清晰初始化错误；
- `HasPublisher<TGenEvent>()`；
- `GetSubscriberCount<TGenEvent, TSubscriber>()`；
- README 契约补充；
- 测试与构建验证；
- Unity 运行时代码与 Source Generator DLL 同步验证。

### 本次不纳入

- `TryPublish()` / `TryPublishAsync()`；
- 完整线程安全模式；
- 粘性事件 / 回放事件；
- 弱引用订阅；
- 批量发布；
- Unity 主线程调度；
- 一次性订阅辅助；
- 异常策略配置化。

## 关键实现决策

### 1. 未初始化的订阅行为也要显式化

当前 `StartListening()` / `StopListening()` 在注册表尚未初始化时会静默 no-op。这个行为比发布前 `KeyNotFoundException` 更隐蔽，因为调用方会误以为订阅已经成功。

实施时应统一收紧为显式失败：

- 在未初始化时调用 `StartListening()` / `StopListening()`，抛出 `InvalidOperationException`；
- 异常消息与发布路径保持同一风格，明确提示先执行 `GenEventBootstrap.Init()`；
- 文档中同步说明：初始化应发生在首次订阅或发布之前，而不仅是首次发布之前。

这是本计划相较于设计文档的一个实现细化，用来避免保留静默失败路径。

### 2. 不提供线程访问检查开关

本次版本只通过运行时契约与文档声明“不支持并发访问”，不再提供额外的线程检查开关或调试期开关。

### 3. 诊断 API 放在调用入口层，而不是底层字典类型上

为保持公开 API 易发现且语义明确：

- `HasPublisher<TGenEvent>()` 放在 `PublisherHelper`；
- `GetSubscriberCount<TGenEvent, TSubscriber>()` 放在 `SubscriberHelper`；
- 底层注册表与基类仍保持偏基础设施定位，不直接扩成“调试控制台”。

## 实施阶段

### 阶段 1：初始化状态与运行时守卫基础设施

#### 目标

搭建初始化状态和统一错误消息所需的基础设施，为后续发布/订阅入口改造提供公共支撑。

#### 代码改动

1. 更新生成器模板，修改文件：

- [Templates.cs](C:/wtl/WTL/Project/GenEvent/src/GenEvent.SourceGenerator/Templates.cs)

实现内容：

- 为生成的 `GenEventBootstrap` 增加 `public static bool IsInitialized { get; private set; }`；
- 将 `Init()` 改为幂等；
- 在首次成功初始化时写入 `IsInitialized = true`；
- 在 `Init()` 中只负责注册与初始化状态切换，不引入线程归属逻辑。

2. 新增或收敛运行时守卫文件：

- `src/GenEvent/src/GenEventRuntimeGuard.cs`

实现内容：

- 提供统一的异常构造方法，避免每个入口各写一套文案；
- 保持守卫职责聚焦在初始化缺失和缺少注册项的诊断上，不承担并发检查。

#### 注意事项

- `GenEventBootstrap` 是生成代码，状态与守卫调用必须通过模板统一注入，不能靠手工修改生成产物；
- 多程序集场景下，每个程序集的 `GenEventBootstrap` 都有各自的初始化状态，计划中的错误消息要明确指出“对应程序集”。

### 阶段 2：发布与订阅入口改造

#### 目标

将新的契约真正落实到对外调用入口，消除静默失败与内部异常泄漏。

#### 代码改动

1. 改造发布入口，修改文件：

- [PublisherHelper.cs](C:/wtl/WTL/Project/GenEvent/src/GenEvent/src/PublisherHelper.cs)

实现内容：

- 改用 `TryGetValue` 检查发布器是否存在，不再直接索引字典；
- 当发布器缺失或未初始化时抛出统一 `InvalidOperationException`；
- 新增 `HasPublisher<TGenEvent>()` 公开方法。

2. 改造订阅入口，修改文件：

- [SubscriberHelper.cs](C:/wtl/WTL/Project/GenEvent/src/GenEvent/src/SubscriberHelper.cs)
- [BaseSubscriberRegistry.cs](C:/wtl/WTL/Project/GenEvent/src/GenEvent/src/Interface/BaseSubscriberRegistry.cs)

实现内容：

- 在 `StartListening()` / `StopListening()` 以及特定事件类型重载入口增加初始化检查；
- 将当前未初始化时的静默 no-op 改为显式异常；
- 新增 `GetSubscriberCount<TGenEvent, TSubscriber>()` 公开方法；
- 在 `GenEventRegistry<TGenEvent, TSubscriber>` 暴露只读计数能力，供 helper 层读取。

3. 补充底层注册表查询支持，修改文件：

- [GenEventRegistry.cs](C:/wtl/WTL/Project/GenEvent/src/GenEvent/src/GenEventRegistry.cs)
- [BaseEventPublisher.cs](C:/wtl/WTL/Project/GenEvent/src/GenEvent/src/Interface/BaseEventPublisher.cs)

实现内容：

- 为订阅计数提供稳定读取入口；
- 保持公开 API 尽量收敛，不把底层基类改造成大量诊断接口集合。

#### 注意事项

- `PublishConfig<TGenEvent>.Setting` 当前是静态可变状态，本阶段不修改其线程模型，只通过契约与文档说明其不支持并发访问；
- `StartListening<TSubscriber, TGenEvent>()` 这一特定事件类型入口不能漏掉守卫，否则用户仍可绕过主入口。

### 阶段 3：文档更新

#### 目标

让 README 正式承载运行时契约，而不是只靠代码行为隐含表达。

#### 文档改动

修改文件：

- [README.md](C:/wtl/WTL/Project/GenEvent/README.md)
- [README_zh.md](C:/wtl/WTL/Project/GenEvent/README_zh.md)

更新内容：

- 新增“运行时契约”章节；
- 明确初始化应在首次订阅或发布前完成；
- 说明 `Init()` 幂等；
- 说明当前版本不保证并发线程安全；
- 说明嵌套发布与并发发布的区别；
- 增加“未初始化常见错误”排查说明；
- 如保留 `HasPublisher<TGenEvent>()`、`GetSubscriberCount<TGenEvent, TSubscriber>()` 为公开 API，则在文档中给出简短示例。

### 阶段 4：测试补齐

#### 目标

在进入实现完成态之前，用测试锁住新契约，避免后续继续回到隐式行为。

#### 建议测试改动

优先新增独立测试文件，避免把契约测试继续堆进现有“鲁棒性”文件中：

- `Tests/BootstrapContractTests.cs`

如果现有测试数据类型不足，则同步调整：

- [RobustnessTests.cs](C:/wtl/WTL/Project/GenEvent/Tests/RobustnessTests.cs)
- [TestTypes.cs](C:/wtl/WTL/Project/GenEvent/Tests/TestTypes.cs)
- [TestRuntimeState.cs](C:/wtl/WTL/Project/GenEvent/Tests/TestRuntimeState.cs)

测试项：

1. 初始化契约

- 初始化前 `IsInitialized == false`
- 初始化后 `IsInitialized == true`
- 多次调用 `Init()` 不抛异常
- 多次调用 `Init()` 后发布行为不重复、不异常

2. 发布契约

- 未初始化 `Publish()` 抛 `InvalidOperationException`
- 未初始化 `PublishAsync()` 抛 `InvalidOperationException`
- 异常消息包含事件类型与 `GenEventBootstrap.Init()` 提示
- 初始化后无订阅者发布仍返回 `true`

3. 订阅契约

- 未初始化 `StartListening()` 抛 `InvalidOperationException`
- 未初始化特定事件订阅重载也抛 `InvalidOperationException`
- 初始化后订阅正常建立
- `GetSubscriberCount<TGenEvent, TSubscriber>()` 结果正确

4. 回归语义

- 嵌套发布行为不变
- 取消传播、过滤、异步发布现有语义不回归

#### 注意事项

- 生成器初始化状态和静态字典状态需要在测试间正确清理，否则容易造成假通过。

### 阶段 5：构建与同步验证

#### 目标

确认 .NET 主项目、测试、Unity 运行时镜像和 Unity Source Generator 副本都与本次改动保持一致。

#### 验证步骤

1. 运行测试：

```powershell
dotnet test
```

2. 构建主库并触发 Unity Runtime 镜像同步：

```powershell
dotnet build src/GenEvent/GenEvent.csproj -c Release
```

3. 构建 Source Generator 并触发 Unity Editor DLL 同步：

```powershell
dotnet build src/GenEvent.SourceGenerator/GenEvent.SourceGenerator.csproj -c Release
```

4. 抽查同步结果：

- `src/GenEvent.Unity/Assets/Plugins/GenEvent/Runtime/` 下的运行时代码已更新；
- `src/GenEvent.Unity/Assets/Plugins/GenEvent/Editor/SourceGenerator/GenEvent.SourceGenerator.dll` 已更新；
- 相关 README 文档变更未破坏 NuGet 打包路径。

## 变更文件清单

### 预计修改

- [Templates.cs](C:/wtl/WTL/Project/GenEvent/src/GenEvent.SourceGenerator/Templates.cs)
- [PublisherHelper.cs](C:/wtl/WTL/Project/GenEvent/src/GenEvent/src/PublisherHelper.cs)
- [SubscriberHelper.cs](C:/wtl/WTL/Project/GenEvent/src/GenEvent/src/SubscriberHelper.cs)
- [GenEventRegistry.cs](C:/wtl/WTL/Project/GenEvent/src/GenEvent/src/GenEventRegistry.cs)
- [BaseEventPublisher.cs](C:/wtl/WTL/Project/GenEvent/src/GenEvent/src/Interface/BaseEventPublisher.cs)
- [BaseSubscriberRegistry.cs](C:/wtl/WTL/Project/GenEvent/src/GenEvent/src/Interface/BaseSubscriberRegistry.cs)
- [README.md](C:/wtl/WTL/Project/GenEvent/README.md)
- [README_zh.md](C:/wtl/WTL/Project/GenEvent/README_zh.md)
- [RobustnessTests.cs](C:/wtl/WTL/Project/GenEvent/Tests/RobustnessTests.cs)
- [TestTypes.cs](C:/wtl/WTL/Project/GenEvent/Tests/TestTypes.cs)
- [TestRuntimeState.cs](C:/wtl/WTL/Project/GenEvent/Tests/TestRuntimeState.cs)

### 预计新增

- `Tests/BootstrapContractTests.cs`

## 建议执行顺序

1. 先做模板与运行时守卫基础设施，确保初始化状态和统一错误消息有共同落点；
2. 再改发布/订阅入口，把“显式契约”落实到所有对外入口；
3. 然后补诊断 API；
4. 再更新 README；
5. 最后补测试并跑完整构建验证；
6. 构建成功后检查 Unity 镜像同步结果。

这个顺序能避免先写测试时因为契约基础设施未成型而频繁返工。

## 风险与回滚点

### 风险

- 生成器模板改动会影响所有生成的 `GenEventBootstrap`；
- 如果订阅入口显式失败处理不一致，容易造成“发布报错、订阅静默失败”的新一轮语义裂缝；
- 静态状态测试隔离不充分时，测试结果可能不稳定。

### 回滚点

如果实现过程中某一部分诊断 API 影响过大，可优先保留：

- `IsInitialized`
- `Init()` 幂等
- 发布/订阅初始化异常显式化

再评估是否暂缓 `HasPublisher<TGenEvent>()` 或 `GetSubscriberCount<TGenEvent, TSubscriber>()`。这样仍然不影响本次版本的主要价值。

## 完成标准

当以下条件全部满足时，本实施计划视为完成：

- 初始化、发布、订阅契约已按计划落地；
- 最小诊断 API 可用；
- README 中已写清初始化与线程契约；
- 新增测试通过，旧测试无回归；
- Unity Runtime 与 Source Generator 副本同步验证通过。
