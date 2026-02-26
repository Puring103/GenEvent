# GenEvent

[![.NET](https://github.com/Puring103/GenEvent/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Puring103/GenEvent/actions/workflows/dotnet.yml)

GenEvent 是一个高性能、0 GC 的事件库，通过源码生成器在编译期生成全部派发代码，无运行时反射，兼容 .NET 与 Unity（netstandard2.0）。

# 目录

- [GenEvent](#genevent)
- [目录](#目录)
- [主要特性](#主要特性)
- [快速开始](#快速开始)
  - [安装](#安装)
  - [Unity 项目](#unity-项目)
  - [最小示例](#最小示例)
- [核心 API](#核心-api)
  - [定义事件](#定义事件)
  - [定义订阅者与处理器](#定义订阅者与处理器)
  - [初始化](#初始化)
  - [订阅生命周期](#订阅生命周期)
  - [发布事件](#发布事件)
  - [事件优先级](#事件优先级)
  - [取消传播](#取消传播)
  - [发布过滤](#发布过滤)
  - [异步支持](#异步支持)
- [源码生成器约束与诊断](#源码生成器约束与诊断)
- [License](#license)

# 主要特性

- **无运行时反射**：所有派发、注册代码在编译期由源码生成器生成
- **0 GC**：事件为值类型（struct），发布路径无堆分配
- **IL2CPP 友好**：不依赖运行时反射，可安全运行在 IL2CPP/AOT 环境
- **优先级**：通过 `[OnEvent(SubscriberPriority.XXX)]` 在编译期确定调用顺序，运行时零排序开销
- **取消传播**：处理器返回 `false` 配合 `Cancelable()` 可中止事件派发
- **灵活的订阅生命周期**：`StartListening` 返回 `SubscriptionHandle`（`IDisposable`），支持 `using` 自动取消，也可手动调用 `StopListening`
- **流式发布 API**：链式配置 `Cancelable`、`WithFilter`、`OnlyType` 等，可按需组合
- **异步支持**：处理器可返回 `Task` / `Task<bool>`，通过 `PublishAsync` 按序 await
- **嵌套发布**：支持在处理器内部再次发布事件，各层配置相互独立

# 快速开始

## 安装

通过 .csproj 引用主库和生成器。生成器以 **Analyzer** 形式接入，仅在编译期生成代码，不参与运行时：

```xml
<ItemGroup>
  <ProjectReference Include="path\to\GenEvent\GenEvent.csproj" />
  <ProjectReference Include="path\to\GenEvent.SourceGenerator\GenEvent.SourceGenerator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

## Unity 项目

在 Unity 中打开 `Window > Package Manager`，点击左上角 **Add**，选择 **Add package from git URL...**，输入：

```
https://github.com/Puring103/GenEvent.git?path=src/GenEvent.Unity/Assets/Plugins/GenEvent
```

**自动初始化**：当生成器检测到项目引用了 UnityEngine / UnityEditor 时，会自动为 `GenEventBootstrap.Init` 添加 `[RuntimeInitializeOnLoadMethod]`，程序启动时自动完成所有注册，无需手动调用。如需在特定时机初始化，仍可手动调用 `GenEventBootstrap.Init()`。

## 最小示例

```csharp
using GenEvent;
using GenEvent.Interface;

// 1. 定义事件：struct + IGenEvent<T>
public struct PlayerDeathEvent : IGenEvent<PlayerDeathEvent>
{
    public int PlayerId;
}

// 2. 定义订阅者：class + [OnEvent] 标记处理方法
public class GameManager
{
    [OnEvent]
    public void OnPlayerDeath(PlayerDeathEvent e)
    {
        Console.WriteLine($"Player {e.PlayerId} died.");
    }
}

// 3. 初始化（非 Unity 项目需在首次 Publish 前调用一次）
GenEventBootstrap.Init();

// 4. 订阅，StartListening 返回 SubscriptionHandle（IDisposable）
var manager = new GameManager();
using var handle = manager.StartListening(); // using 结束时自动取消订阅

// 5. 发布事件
new PlayerDeathEvent { PlayerId = 1 }.Publish();
```

---

# 核心 API

## 定义事件

事件必须是 `struct` 并实现 `IGenEvent<T>`。值类型确保发布路径 0 GC、无装箱。

```csharp
public struct DamageEvent : IGenEvent<DamageEvent>
{
    public int Amount;
    public string Source;
}
```

## 定义订阅者与处理器

订阅者是普通 `class`，用 `[OnEvent]` 特性标记处理方法。根据是否需要参与传播控制，选择对应的返回类型：

| 签名                                | 说明                                                               |
| ----------------------------------- | ------------------------------------------------------------------ |
| `void Method(TEvent e)`             | 只接收事件，不参与传播控制                                         |
| `bool Method(TEvent e)`             | 返回 `false` 可中止后续订阅者接收（需配合发布时的 `Cancelable()`） |
| `async Task Method(TEvent e)`       | 异步处理，不参与传播控制                                           |
| `async Task<bool> Method(TEvent e)` | 异步处理，返回 `false` 可中止传播（需配合发布时的 `Cancelable()`） |

```csharp
public class HUDDisplay
{
    // void：相当于永远返回true
    [OnEvent]
    public void OnDamage(DamageEvent e)
    {
        UpdateHealthBar(e.Amount);
    }
}

public class ShieldSystem
{
    // bool：可以拦截事件并阻止后续订阅者收到
    [OnEvent]
    public bool OnDamage(DamageEvent e)
    {
        if (HasShield)
        {
            AbsorbDamage(e.Amount);
            return false; // 中止传播，后续订阅者（如 HUDDisplay）不会收到
        }
        return true;
    }
}
```

同一 class 对同一事件最多定义**一个同步**和**一个异步**处理器，分别由 `Publish` 和 `PublishAsync` 触发。

## 初始化

首次发布前，调用 `GenEventBootstrap.Init()` 完成所有 Publisher 与 Subscriber 的注册。若项目有多个程序集，每个程序集需各自调用其生成的 `Init()`。

```csharp
// 在程序入口调用一次即可
GenEventBootstrap.Init();
```

Unity 项目由生成器自动插入 `[RuntimeInitializeOnLoadMethod]`，无需手动调用。

## 订阅生命周期

`StartListening()` 将订阅者注册到事件系统，并返回一个 `SubscriptionHandle`（`IDisposable`）。持有该句柄并在合适时机 `Dispose`，即可取消订阅，等价于调用 `StopListening()`。

**推荐：持有句柄，在销毁时 Dispose**

```csharp
public class Enemy : MonoBehaviour
{
    private SubscriptionHandle _handle;

    void OnEnable()  => _handle = this.StartListening();
    void OnDisable() => _handle.Dispose(); // 等价于 this.StopListening()
}
```

**或使用 `using` 限定作用域**

```csharp
using (subscriber.StartListening())
{
    new DamageEvent { Amount = 10 }.Publish(); // 正常接收
} // 离开 using 块，自动取消订阅

new DamageEvent { Amount = 5 }.Publish(); // 不再接收
```

**也可以直接调用 `StopListening()`（忽略句柄）**

```csharp
subscriber.StartListening(); // 忽略返回值，与旧写法完全相同

new DamageEvent { Amount = 10 }.Publish();

subscriber.StopListening(); // 手动取消
```

`SubscriptionHandle.Dispose()` 是幂等的，多次调用安全。

**仅订阅某一种事件**（当订阅者处理多种事件类型时）：

```csharp
// 只注册 DamageEvent，其他事件类型不受影响
using var handle = subscriber.StartListening<MySubscriber, DamageEvent>();
```

> 未取消订阅的订阅者会阻止 GC 回收，请在对象销毁时务必取消。

## 发布事件

**同步发布**：`Publish()` 返回 `bool`，表示事件是否完整派发到所有订阅者（未触发取消传播时始终为 `true`）。

```csharp
bool completed = new DamageEvent { Amount = 10 }.Publish();
```

**异步发布**：`PublishAsync()` 按优先级顺序依次 await 每个处理器，返回 `Task<bool>`（未触发取消传播时始终为 `true`）。

```csharp
bool completed = await new DamageEvent { Amount = 10 }.PublishAsync();
```

> 同步 `Publish()` 只调用 sync 处理器；`PublishAsync()` 会调用 sync 与 async 处理器。

## 事件优先级

通过 `[OnEvent(SubscriberPriority.XXX)]` 指定优先级，调用顺序在**编译期**由生成器确定，运行时零排序开销。

优先级从高到低：`Primary` > `High` > `Medium`（默认）> `Low` > `End`

```csharp
public class ShieldSystem
{
    [OnEvent(SubscriberPriority.High)] // 先于默认 Medium 执行
    public bool OnDamage(DamageEvent e)
    {
        if (HasShield) { AbsorbDamage(e.Amount); return false; }
        return true;
    }
}

public class HUDDisplay
{
    [OnEvent] // 默认 Medium，在 ShieldSystem 之后执行
    public void OnDamage(DamageEvent e) => UpdateHealthBar(e.Amount);
}
```

## 取消传播

在发布时链式调用 `.Cancelable()`，此后若某个处理器返回 `false`，传播立即中止，后续订阅者不再收到本次事件，`Publish` 返回 `false`。

不调用 `Cancelable()` 时，所有处理器的返回值被忽略，事件始终完整派发给所有订阅者。

```csharp
// 带 Cancelable：ShieldSystem（High）返回 false 时，HUDDisplay（Medium）不会收到
bool handled = new DamageEvent { Amount = 10 }
    .Cancelable()
    .Publish();
// handled == false 说明传播被中止

// 不带 Cancelable：所有订阅者都会收到，返回值无效
new DamageEvent { Amount = 10 }.Publish();
```

## 发布过滤

以下 API 均为**本次发布**的链式配置，仅影响当次派发，不修改订阅注册状态，可自由组合：

| API                                        | 说明                                            |
| ------------------------------------------ | ----------------------------------------------- |
| `evt.Cancelable()`                         | 允许处理器通过返回 `false` 中止传播             |
| `evt.WithFilter(Predicate<object> filter)` | `filter(subscriber)` 返回 `true` 时跳过该订阅者 |
| `evt.OnlyType<TEvent, TSubscriber>()`      | 仅 `TSubscriber` 类型的订阅者收到               |
| `evt.ExcludeType<TEvent, TSubscriber>()`   | 排除 `TSubscriber` 类型的订阅者                 |
| `evt.OnlySubscriber(subscriber)`           | 仅指定实例收到                                  |
| `evt.ExcludeSubscriber(subscriber)`        | 排除指定实例                                    |
| `evt.OnlySubscribers(HashSet<object>)`     | 仅集合中的实例收到                              |
| `evt.ExcludeSubscribers(HashSet<object>)`  | 排除集合中的实例                                |

```csharp
// 仅通知 UI 层，不触发游戏逻辑
new DamageEvent { Amount = 5 }
    .OnlyType<DamageEvent, HUDDisplay>()
    .Publish();

// 排除自身，避免收到自己发出的事件
new DamageEvent { Amount = 5 }
    .ExcludeSubscriber(this)
    .Publish();

// 链式组合：可取消 + 仅指定类型
new DamageEvent { Amount = 5 }
    .Cancelable()
    .OnlyType<DamageEvent, ShieldSystem>()
    .Publish();

// 排除多个实例
var exclude = new HashSet<object> { enemyA, enemyB };
new DamageEvent { Amount = 5 }.ExcludeSubscribers(exclude).Publish();
```

## 异步支持

将处理方法签名改为返回 `Task` 或 `Task<bool>` 即可定义异步处理器，无需额外配置。

```csharp
public class NetworkSync
{
    [OnEvent]
    public async Task OnDamage(DamageEvent e)
    {
        await SendToServerAsync(e);
    }
}

// 异步发布：按优先级顺序依次 await 每个处理器
bool completed = await new DamageEvent { Amount = 10 }.PublishAsync();
```

同一订阅者可以同时定义 sync 和 async 两个处理器处理同一事件，分别由 `Publish` 和 `PublishAsync` 触发：

```csharp
public class CombatLogger
{
    [OnEvent]
    public void OnDamage(DamageEvent e)              // 由 Publish() 触发
    {
        LogToFile(e);
    }

    [OnEvent]
    public async Task OnDamageAsync(DamageEvent e)   // 由 PublishAsync() 触发（注意，同步版本也会被调用）
    {
        await LogToRemoteAsync(e);
    }
}
```

---

# 源码生成器约束与诊断

生成器对事件与 `[OnEvent]` 方法有明确约束，违反时会在**编译期**报告诊断，不会静默失败。

**事件约束**

- 必须是 `struct`，并实现 `IGenEvent<T>`

**处理器方法约束**

- 必须是 `public` 实例方法
- 必须有且仅有一个参数，且类型为实现了 `IGenEvent<>` 的事件类型
- 返回类型只能是 `void`、`bool`、`Task` 或 `Task<bool>`
- 同一 class 对同一事件类型，最多一个 sync handler 和一个 async handler

**诊断码**

| 代码  | 严重性  | 含义                                                                  |
| ----- | ------- | --------------------------------------------------------------------- |
| GE001 | Warning | 未找到 `IGenEvent` 接口，请检查 GenEvent 引用                         |
| GE002 | Warning | 未找到 `OnEventAttribute`，请检查 GenEvent 引用                       |
| GE010 | Error   | `[OnEvent]` 方法必须为 public                                         |
| GE011 | Error   | `[OnEvent]` 方法必须恰好有一个参数                                    |
| GE012 | Error   | `[OnEvent]` 方法参数必须是 `IGenEvent` 类型                           |
| GE013 | Error   | 同一 class 对同一事件类型不能有两个同步或两个异步 handler             |
| GE014 | Error   | `[OnEvent]` 方法返回类型必须为 `void`、`bool`、`Task` 或 `Task<bool>` |
| GE999 | Error   | 源码生成器内部异常，请查看编译器输出获取详情                          |

出现 GE001/GE002 时请检查主库与生成器的引用是否均已正确添加；GE010–GE014 按上表修正方法签名；GE999 请查看编译器完整输出。

# License

本项目采用 [MIT License](LICENSE)。Copyright (c) 2026 Puring。详见 [LICENSE](LICENSE) 文件。
