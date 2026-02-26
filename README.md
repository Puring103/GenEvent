# GenEvent

GenEvent 是一个高性能,0GC的Event库，使用代码生成器实现,无运行时反射。

目标框架是netstandard2.0，适用于 .NET 与 Unity

# 主要特性

1. 无运行时反射：使用代码生成，避免运行时反射开销
2. 0 GC：使用值类型，GC友好
3. 高易用性的流式api
4. 发布前可链式配置 Cancelable、WithFilter、OnlyType/ExcludeType 等，仅对当次发布生效
5. 基于特性的快速事件处理函数注册
6. IL2cpp友好：消除了运行时反射代码，可以在IL2CPP中正常运行
7. 支持事件优先级，事件拦截，共享事件与事件过滤
8. 支持异步事件

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

目标框架 netstandard2.0，.NET 与 Unity 均可使用。Unity 项目若使用 GenEvent.Unity 下的 Plugins 目录，该目录为构建时自动生成，请勿手改其中代码。

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

// 2. 定义订阅者：class，用 [OnEvent] 标记处理方法；返回 true 表示继续传播，false 表示拦截
public class MySubscriber
{
    public int Received;

    [OnEvent]
    public bool OnMyEvent(MyEvent e)
    {
        Received = e.Value;
        return true;
    }
}

// 3. 程序启动时调用一次 Init，注册所有生成的 Publisher/Subscriber（必须在使用 Publish 前执行）
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
- **订阅者**：`class`，事件处理方法用 `[OnEvent]` 标记，签名为 `bool Method(TEvent e)`。返回值含义：`true` 继续派发给后续订阅者，`false` 立即停止派发（需配合本次发布使用 `Cancelable()` 才生效）。

```csharp
// 事件：struct + IGenEvent<T>
public struct GameScoreEvent : IGenEvent<GameScoreEvent>
{
    public int Score;
}

// 订阅者：class + [OnEvent]，返回 bool
public class ScoreView
{
    [OnEvent]
    public bool OnScore(GameScoreEvent e)
    {
        UpdateUI(e.Score);
        return true; // 继续传播
    }
}
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

通过 `[OnEvent(SubscriberPriority.XXX)]` 指定优先级，执行顺序在编译期由生成器确定，不会排序。优先级从高到低：`Primary`、`High`、`Medium`、`Low`、`End`。

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

处理器返回 `false` 会立即停止向后续订阅者派发，`Publish`/`PublishAsync` 返回 `false`。**只有**在发布时链式加上 `Cancelable()` 时，该“返回 false”才会真正中止传播。

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


| 能力    | API                                         | 说明                                 |
| ----- | ------------------------------------------- | ---------------------------------- |
| 可取消   | `evt.Cancelable()`                          | 允许处理器通过返回 false 中止传播               |
| 自定义过滤 | `evt.WithFilter(Predicate<object> filter)`  | filter(subscriber) 为 true 时过滤掉该订阅者 |
| 仅某类型  | `evt.OnlyType<TGenEvent, TSubscriber>()`    | 仅 TSubscriber 类型收到                 |
| 排除某类型 | `evt.ExcludeType<TGenEvent, TSubscriber>()` | 排除 TSubscriber 类型                  |
| 排除某实例 | `evt.ExcludeSubscriber(subscriber)`         | 排除指定实例                             |
| 仅某实例  | `evt.OnlySubscriber(subscriber)`            | 仅该实例收到                             |
| 排除多实例 | `evt.ExcludeSubscribers(HashSet<object>)`   | 排除集合中的实例                           |
| 仅多实例  | `evt.OnlySubscribers(HashSet<object>)`      | 仅集合中的实例收到                          |


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

## 异步事件处理

异步处理器签名为返回 `Task<bool>`，例如 `[OnEvent] public async Task<bool> OnX(MyEvent e) { ... }`。`PublishAsync()` 会按优先级依次 await 这些 handler；同步 `Publish()` 不会调用**仅**有 async handler 的订阅者。同一订阅者类型可同时定义 sync 与 async 两个 handler，分别由 `Publish` 与 `PublishAsync` 触发。

```csharp
public class AsyncSubscriber
{
    [OnEvent]
    public async Task<bool> OnMyEventAsync(MyEvent e)
    {
        await DoSomethingAsync(e.Value);
        return true;
    }
}

// 仅异步发布会调用上述 handler
await new MyEvent { Value = 1 }.PublishAsync();
```

