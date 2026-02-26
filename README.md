# GenEvent

[GitHub stars](https://github.com/wtlllll190812/GenEvent) [.NET](https://github.com/wtlllll190812/GenEvent/actions/workflows/dotnet.yml)

GenEvent 是一个高性能,0GC的Event库，使用代码生成器实现,无运行时反射。

目标框架是netstandard2.0，适用于 .NET 与 Unity

# 目录

- [GenEvent](#genevent)
- [目录](#目录)
- [主要特性](#主要特性)
- [快速开始](#快速开始)
  - [安装](#安装)
  - [Unity 项目](#unity-项目)
  - [最小示例](#最小示例)
- [基本 API 与特性](#基本-api-与特性)
  - [事件与订阅者约定](#事件与订阅者约定)
  - [处理器返回值：void 与 bool](#处理器返回值void-与-bool)
  - [初始化与订阅生命周期](#初始化与订阅生命周期)
  - [发布：同步与异步](#发布同步与异步)
  - [事件优先级](#事件优先级)
  - [事件拦截（取消传播）](#事件拦截取消传播)
  - [发布过滤（流式 API）](#发布过滤流式-api)
  - [异步事件处理](#异步事件处理)
- [源码生成器约束与诊断](#源码生成器约束与诊断)
- [License](#license)

# 主要特性

1. 无运行时反射：使用代码生成，避免运行时反射开销
2. 0 GC：使用值类型，GC友好
3. 高易用性的流式api，发布前可链式配置 Cancelable、WithFilter、OnlyType/ExcludeType 等
4. 基于特性的快速注册
5. IL2cpp 友好：消除了运行时反射代码，可以在IL2CPP中正常运行
6. 支持事件优先级、事件拦截与事件过滤
7. 支持异步事件
8. 支持嵌套 publish

# 快速开始

## 安装

通过项目引用接入 GenEvent 与源码生成器。主库引用 `GenEvent` 项目；生成器以 **Analyzer** 形式引用（不引用程序集），这样编译时会自动生成事件派发与订阅注册代码。

```xml
<ItemGroup>
  <ProjectReference Include="path\to\GenEvent\GenEvent.csproj" />
  <ProjectReference Include="path\to\GenEvent.SourceGenerator\GenEvent.SourceGenerator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

目标框架 netstandard2.0，.NET 与 Unity 均可使用。Unity 项目推荐通过 **Unity Package Manager** 使用 Git URL 导入（见下文「Unity 项目」小节），也可以直接引用 GenEvent.Unity 工程。

## Unity 项目

- **通过 Git 导入（推荐）**：在 Unity 中打开 `Window > Package Manager`，点击左上角 **Add** 按钮选择 **Add package from git URL...**，输入  
  `https://github.com/wtlllll190812/GenEvent.git?path=src/GenEvent.Unity/Assets/Plugins/GenEvent`  
  并确认，即可将 GenEvent 作为一个 Unity 包导入项目。
- **自动初始化**：当编译时检测到引用了 **UnityEngine** 或 **UnityEditor** 时，源码生成器会为 `GenEventBootstrap.Init` 自动添加 `[UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded)]`，因此 **Unity 中可在不手动调用 Init 的情况下，在程序启动时自动完成注册**。若仍需自定义时机，可在场景加载或入口处自行调用 `GenEventBootstrap.Init()`。

## 最小示例

下面是一段可运行的最小示例：定义事件与订阅者、初始化、订阅、发布、取消订阅。

```csharp
using GenEvent;
using GenEvent.Interface;

// 1. 定义事件：必须是 struct，并实现 IGenEvent<T>（值类型，0 GC）
public struct MyEvent : IGenEvent<MyEvent>
{
    public int Value;
}

// 2. 定义订阅者：class，用 [OnEvent] 标记处理方法；无返回值（void）表示仅接收事件、始终继续传播
public class MySubscriber
{
    public int Received;

    [OnEvent]
    public void OnMyEvent(MyEvent e)
    {
        Received = e.Value;
    }
}

// 3. 程序启动时调用一次 Init，注册所有生成的 Publisher/Subscriber（必须在使用 Publish 前执行），若存在多个程序集，需要为每个程序集进行初始化
GenEventBootstrap.Init();

// 4. 创建订阅者并开始监听
var subscriber = new MySubscriber();
subscriber.StartListening();

// 5. 发布事件
new MyEvent { Value = 42 }.Publish();
// subscriber.Received == 42

// 6. 不再需要接收事件时取消订阅，否则订阅会影响 GC
subscriber.StopListening();
```

# 基本 API 与特性

## 事件与订阅者约定

- **事件**：必须为 `struct`，实现 `IGenEvent<TEvent>`，以保证值类型、无装箱、0 GC。
- **订阅者**：`class`，事件处理方法用 `[OnEvent]` 标记。**同步**方法签名为 `void Method(TEvent e)` 或 `bool Method(TEvent e)`；**异步**为 `Task Method(TEvent e)` 或 `Task<bool> Method(TEvent e)`。

```csharp
// 事件：struct + IGenEvent<T>
public struct GameScoreEvent : IGenEvent<GameScoreEvent>
{
    public int Score;
}

// 订阅者：class + [OnEvent]，可用 void 或 bool
public class ScoreView
{
    [OnEvent]
    public void OnScore(GameScoreEvent e)
    {
        UpdateUI(e.Score);
    }
}
```

## 处理器返回值：void 与 bool

- **void / Task**：不关心是否取消传播时使用，写法简单；内部视为“始终继续”（等价 true）。
- **bool / Task**：需要参与“取消传播”时使用；返回 `false` 且在发布时使用 `Cancelable()` 时，会中止向后续订阅者派发。

需要拦截事件时，将处理器改为返回 `bool`（或 `Task<bool>`），并在发布时链式调用 `Cancelable()`：

```csharp
// 返回 bool：可根据条件中止传播
public class CancelerSubscriber
{
    [OnEvent]
    public bool OnScore(GameScoreEvent e)
    {
        if (e.Score < 0) return false; // 中止传播
        UpdateUI(e.Score);
        return true;
    }
}

// 发布时标记为可取消，链式调用
var evt = new GameScoreEvent { Score = -1 }.Cancelable();
bool completed = evt.Publish(); // completed == false，后续订阅者不会收到
```

## 初始化与订阅生命周期

- **初始化**：`GenEventBootstrap.Init()` 会注册所有由源码生成器生成的 Publisher/Subscriber，必须在首次 `Publish` 前调用（例如程序入口或 Unity 场景加载时）。
- **订阅**：
  - `subscriber.StartListening()`：监听该订阅者类型所处理的**所有**事件。
  - `subscriber.StopListening()`：取消上述监听。
  - `subscriber.StartListening<TSubscriber, TGenEvent>()` / `subscriber.StopListening<TSubscriber, TGenEvent>()`：仅监听/取消某一种事件。

不调用 `StopListening` 会导致订阅常驻，订阅者难以被 GC 回收，请在不使用时务必取消订阅。

```csharp
GenEventBootstrap.Init();

var sub = new MySubscriber();
// 监听该订阅者处理的所有事件类型
sub.StartListening();

// 或仅监听某一种事件（当该订阅者处理多种事件类型时）
sub.StartListening<MySubscriber, MyEvent>();

// 不再需要时取消
sub.StopListening();
```

## 发布：同步与异步

- **同步**：`evt.Publish()` 返回 `bool`，表示是否所有订阅者都处理完；若有订阅者返回 `false` 且本次发布为 `Cancelable()`，则中止传播并返回 `false`。
- **异步**：`await evt.PublishAsync()` 返回 `Task<bool>`，语义与 `Publish()` 相同。

注意：**同步 `Publish()` 不会调用仅定义了 async handler 的订阅者**；只有 `PublishAsync()` 会调用异步 handler。

```csharp
var evt = new MyEvent { Value = 1 };
bool ok = evt.Publish();

var evt2 = new MyEvent { Value = 2 };
bool okAsync = await evt2.PublishAsync();
```

## 事件优先级

通过 `[OnEvent(SubscriberPriority.XXX)]` 指定优先级，执行顺序在编译期由生成器确定，不会在运行时排序。优先级从高到低：`Primary`、`High`、`Medium`、`Low`、`End`。

```csharp
public class EarlySubscriber
{
    [OnEvent(SubscriberPriority.High)]
    public bool OnE(MyEvent e) { /* 先执行 */ return true; }
}

public class LateSubscriber
{
    [OnEvent(SubscriberPriority.Low)]
    public bool OnE(MyEvent e) { /* 后执行 */ return true; }
}
```

## 事件拦截（取消传播）

仅当处理器返回 **bool**（或 **Task**）时，返回 `false` 才会在发布时链式加上 `Cancelable()` 的情况下中止传播；void / Task 无返回值，不能取消传播。处理器返回 `false` 会立即停止向后续订阅者派发，`Publish`/`PublishAsync` 返回 `false`。

```csharp
// 订阅者中某一位返回 false
public class Canceler
{
    [OnEvent]
    public bool OnE(MyEvent e) => false; // 中止传播
}

// 发布时标记为可取消，链式调用
var evt = new MyEvent { Value = 1 }.Cancelable();
bool completed = evt.Publish(); // completed == false，后续订阅者不会收到
```

## 发布过滤（流式 API）

以下均为**本次发布**的链式配置，仅影响这一次 `Publish`/`PublishAsync` 触达的订阅者，可组合使用。

| 能力       | API                                         | 说明                                        |
| ---------- | ------------------------------------------- | ------------------------------------------- |
| 可取消     | `evt.Cancelable()`                          | 允许处理器通过返回 false 中止传播           |
| 自定义过滤 | `evt.WithFilter(Predicate<object> filter)`  | filter(subscriber) 为 true 时过滤掉该订阅者 |
| 仅某类型   | `evt.OnlyType<TGenEvent, TSubscriber>()`    | 仅 TSubscriber 类型收到                     |
| 排除某类型 | `evt.ExcludeType<TGenEvent, TSubscriber>()` | 排除 TSubscriber 类型                       |
| 排除某实例 | `evt.ExcludeSubscriber(subscriber)`         | 排除指定实例                                |
| 仅某实例   | `evt.OnlySubscriber(subscriber)`            | 仅该实例收到                                |
| 排除多实例 | `evt.ExcludeSubscribers(HashSet<object>)`   | 排除集合中的实例                            |
| 仅多实例   | `evt.OnlySubscribers(HashSet<object>)`      | 仅集合中的实例收到                          |

示例：仅某类型收到、排除某实例。

```csharp
// 仅 UI 订阅者收到
new MyEvent { Value = 1 }.OnlyType<MyEvent, UISubscriber>().Publish();

// 排除某个具体实例
new MyEvent { Value = 2 }.ExcludeSubscriber(thatSubscriber).Publish();

// 链式组合
new MyEvent { Value = 3 }
    .Cancelable()
    .OnlyType<MyEvent, GameLogic>()
    .Publish();
```

使用 `ExcludeSubscribers` / `OnlySubscribers` 时传入的 `HashSet<object>` 不可为 null。示例：

```csharp
// 排除多个实例：仅排除 HashSet 中的订阅者
var excludeSet = new HashSet<object> { subscriberB, subscriberC };
new MyEvent { Value = 2 }.ExcludeSubscribers(excludeSet).Publish();

// 仅多实例收到：仅 HashSet 中的订阅者会收到
var includeSet = new HashSet<object> { subscriberA };
new MyEvent { Value = 3 }.OnlySubscribers(includeSet).Publish();
```

## 异步事件处理

异步处理器可为 **Task**（无返回值，视为继续）或 **Task**（返回 false 时可配合 `**Cancelable()` 中止传播），例如 `[OnEvent] public async Task OnX(MyEvent e) { ... }` 或 `[OnEvent] public async Task<bool> OnX(MyEvent e) { ... }`。`PublishAsync()` 会按优先级依次 await 这些 handler；同步 `Publish()` 不会调用仅有 async handler 的订阅者。同一订阅者类型可同时定义 sync 与 async 两个 handler，分别由 `Publish` 与 `PublishAsync` 触发。

```csharp
public class AsyncSubscriber
{
    [OnEvent]
    public async Task OnMyEventAsync(MyEvent e)  // void 风格：无返回值，始终继续
    {
        await DoSomethingAsync(e.Value);
    }

    // 或需要取消传播时使用 Task<bool>
    // public async Task<bool> OnMyEventAsync(MyEvent e) { ... return true; }
}

// 仅异步发布会调用上述 handler
await new MyEvent { Value = 1 }.PublishAsync();
```

# 源码生成器约束与诊断

源码生成器对事件与 `[OnEvent]` 方法有明确约束，违反时会产生下列诊断，便于排查编译错误。

**约束：**

- **事件**：必须为 `struct`，并实现 `IGenEvent<TEvent>`（生成器通过 `IGenEvent<T>` 的元数据名识别）。
- **[OnEvent] 方法**：
  - 必须为 **public**。
  - 必须有且仅有 **一个参数**，且该参数类型为实现了 `IGenEvent<>` 的事件类型。
  - 返回类型只能是 **void**、**bool**、**Task** 或 **Task**。
  - 同一 **class** 对同一事件类型只能有一个同步 handler 与一个异步 handler（即每事件类型最多两个方法）。

**诊断码：**

| 代码  | 严重性  | 含义                                                                              |
| ----- | ------- | --------------------------------------------------------------------------------- |
| GE001 | Warning | 未找到 IGenEvent 接口，请确保已引用 GenEvent。                                    |
| GE002 | Warning | 未找到 OnEventAttribute，请确保已引用 GenEvent。                                  |
| GE010 | Error   | [OnEvent] 方法必须为 public。                                                     |
| GE011 | Error   | [OnEvent] 方法必须有且仅有一个参数（事件类型）。                                  |
| GE012 | Error   | [OnEvent] 方法参数必须是 IGenEvent 类型。                                         |
| GE013 | Error   | 同一类对同一事件类型只能有一个 [OnEvent] 方法（可同时有一个 sync 与一个 async）。 |
| GE014 | Error   | [OnEvent] 方法返回类型必须为 void、bool、Task 或 Task。                           |
| GE999 | Error   | 源码生成器内部异常，消息中会包含具体原因。                                        |

出现 GE001/GE002 时请检查主库与生成器引用是否正确；GE010–GE014 按上表修正方法签名与数量；GE999 请查看编译器输出的异常信息。

# License

本项目采用 [MIT License](LICENSE)。Copyright (c) 2026 Puring。详见 [LICENSE](LICENSE) 文件。
