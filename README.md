# GenEvent

[![.NET](https://github.com/Puring103/GenEvent/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Puring103/GenEvent/actions/workflows/dotnet.yml)

**中文文档 / Chinese:** [README_zh.md](README_zh.md)

GenEvent is a high‑performance, zero‑GC event library. It uses a source generator to emit all dispatching code at compile time, requires no runtime reflection, and works with both .NET and Unity (netstandard2.0).

# Table of Contents

- [GenEvent](#genevent)
- [Table of Contents](#table-of-contents)
- [Key Features](#key-features)
- [Getting Started](#getting-started)
  - [Installation](#installation)
  - [Unity Projects](#unity-projects)
  - [Minimal Example](#minimal-example)
- [Core APIs](#core-apis)
  - [Defining Events](#defining-events)
  - [Defining Subscribers and Handlers](#defining-subscribers-and-handlers)
  - [Initialization](#initialization)
  - [Subscription Lifetime](#subscription-lifetime)
  - [Publishing Events](#publishing-events)
  - [Event Priority](#event-priority)
  - [Canceling Propagation](#canceling-propagation)
  - [Publish Filters](#publish-filters)
  - [Async Support](#async-support)
- [Source Generator Constraints & Diagnostics](#source-generator-constraints--diagnostics)
- [License](#license)

# Key Features

- **No runtime reflection**: All dispatching and registration code is generated at compile time by a source generator.
- **Zero GC**: Events are value types (`struct`), so the publish path allocates nothing on the heap.
- **IL2CPP‑friendly**: Does not rely on runtime reflection and is safe to use in IL2CPP/AOT environments.
- **Priority support**: `[OnEvent(SubscriberPriority.XXX)]` determines invocation order at compile time, so there is no sorting cost at runtime.
- **Propagation cancelation**: A handler can return `false`. Combined with `Cancelable()`, this stops further event dispatch.
- **Flexible subscription lifetime**: `StartListening` returns a `SubscriptionHandle` (`IDisposable`). You can rely on `using` for automatic unsubscribe or manually call `StopListening`.
- **Fluent publish APIs**: Chain `Cancelable`, `WithFilter`, `OnlyType`, and others to compose per‑publish behavior.
- **Async support**: Handlers can return `Task` / `Task<bool>`, and `PublishAsync` awaits them in order.
- **Nested publish**: Handlers can publish other events; each publish call has its own independent configuration.

# Getting Started

## Installation

Reference the core library and the source generator in your `.csproj`. The generator is plugged in as an **Analyzer**, only used at compile time to generate code and never loaded at runtime:

```xml
<ItemGroup>
  <ProjectReference Include="path\to\GenEvent\GenEvent.csproj" />
  <ProjectReference Include="path\to\GenEvent.SourceGenerator\GenEvent.SourceGenerator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

## Unity Projects

In Unity, open `Window > Package Manager`, click **Add** in the top‑left corner, choose **Add package from git URL...**, and enter:

```
https://github.com/Puring103/GenEvent.git?path=src/GenEvent.Unity/Assets/Plugins/GenEvent
```

**Automatic initialization**: When the generator detects that the project references UnityEngine / UnityEditor, it automatically adds `[RuntimeInitializeOnLoadMethod]` to `GenEventBootstrap.Init`. This means all registrations are performed automatically when the game starts and you do not need to call it manually. If you prefer to control the exact timing, you can still call `GenEventBootstrap.Init()` yourself.

## Minimal Example

```csharp
using GenEvent;
using GenEvent.Interface;

// 1. Define the event: struct + IGenEvent<T>
public struct PlayerDeathEvent : IGenEvent<PlayerDeathEvent>
{
    public int PlayerId;
}

// 2. Define a subscriber: class + [OnEvent] on the handler method
public class GameManager
{
    [OnEvent]
    public void OnPlayerDeath(PlayerDeathEvent e)
    {
        Console.WriteLine($"Player {e.PlayerId} died.");
    }
}

// 3. Initialize (for non‑Unity projects call this once before the first Publish)
GenEventBootstrap.Init();

// 4. Subscribe; StartListening returns SubscriptionHandle (IDisposable)
var manager = new GameManager();
using var handle = manager.StartListening(); // automatically unsubscribes when the using scope ends

// 5. Publish the event
new PlayerDeathEvent { PlayerId = 1 }.Publish();
```

---

# Core APIs

## Defining Events

An event must be a `struct` and implement `IGenEvent<T>`. Using a value type guarantees zero‑GC dispatch with no boxing.

```csharp
public struct DamageEvent : IGenEvent<DamageEvent>
{
    public int Amount;
    public string Source;
}
```

## Defining Subscribers and Handlers

A subscriber is a normal `class`. Use the `[OnEvent]` attribute to mark handler methods. Choose a return type based on whether the handler should participate in propagation control:

| Signature                            | Description                                                                 |
| ------------------------------------ | --------------------------------------------------------------------------- |
| `void Method(TEvent e)`              | Only receives the event; does not control propagation                      |
| `bool Method(TEvent e)`              | Returning `false` stops subsequent subscribers (requires `Cancelable()`)   |
| `async Task Method(TEvent e)`        | Async processing; does not control propagation                             |
| `async Task<bool> Method(TEvent e)`  | Async processing; returning `false` stops propagation (requires `Cancelable()`) |

```csharp
public class HUDDisplay
{
    // void: equivalent to always returning true
    [OnEvent]
    public void OnDamage(DamageEvent e)
    {
        UpdateHealthBar(e.Amount);
    }
}

public class ShieldSystem
{
    // bool: can intercept the event and prevent later subscribers from receiving it
    [OnEvent]
    public bool OnDamage(DamageEvent e)
    {
        if (HasShield)
        {
            AbsorbDamage(e.Amount);
            return false; // stop propagation; later subscribers (such as HUDDisplay) will not be called
        }
        return true;
    }
}
```

A single class can define at most **one sync** and **one async** handler for the same event type. They are invoked by `Publish` and `PublishAsync` respectively.

## Initialization

Before the first publish, call `GenEventBootstrap.Init()` to register all publishers and subscribers. If your solution has multiple assemblies, each one that uses GenEvent must call its own generated `Init()`.

```csharp
// Call once at application startup
GenEventBootstrap.Init();
```

For Unity projects, the generator injects `[RuntimeInitializeOnLoadMethod]` automatically, so you usually do not need to call this manually.

## Subscription Lifetime

`StartListening()` registers a subscriber in the event system and returns a `SubscriptionHandle` (`IDisposable`). Keep this handle and dispose it when appropriate to unsubscribe; this is equivalent to calling `StopListening()`.

**Recommended: keep the handle and dispose it on destruction**

```csharp
public class Enemy : MonoBehaviour
{
    private SubscriptionHandle _handle;

    void OnEnable()  => _handle = this.StartListening();
    void OnDisable() => _handle.Dispose(); // equivalent to this.StopListening()
}
```

**Or use a `using` scope**

```csharp
using (subscriber.StartListening())
{
    new DamageEvent { Amount = 10 }.Publish(); // receives the event
} // leaving the using scope automatically unsubscribes

new DamageEvent { Amount = 5 }.Publish(); // no longer receives the event
```

**You can also call `StopListening()` directly (ignoring the handle)**

```csharp
subscriber.StartListening(); // ignore the return value; behaves like the old pattern

new DamageEvent { Amount = 10 }.Publish();

subscriber.StopListening(); // unsubscribe manually
```

`SubscriptionHandle.Dispose()` is idempotent and safe to call multiple times.

**Subscribe to only one event type** (when a subscriber handles multiple event types):

```csharp
// Register only DamageEvent; other event types are unaffected
using var handle = subscriber.StartListening<MySubscriber, DamageEvent>();
```

> A subscriber that is never unsubscribed will keep it alive and prevent GC. Always unsubscribe when the object is destroyed.

## Publishing Events

**Sync publish**: `Publish()` returns `bool`, indicating whether the event was fully dispatched to all subscribers (it is always `true` unless propagation was canceled).

```csharp
bool completed = new DamageEvent { Amount = 10 }.Publish();
```

**Async publish**: `PublishAsync()` awaits each handler in priority order and returns `Task<bool>` (again, `true` unless propagation was canceled).

```csharp
bool completed = await new DamageEvent { Amount = 10 }.PublishAsync();
```

> Sync `Publish()` only invokes sync handlers; `PublishAsync()` invokes both sync and async handlers.

## Event Priority

Use `[OnEvent(SubscriberPriority.XXX)]` to specify priority. The invocation order is determined by the generator **at compile time**, so there is no runtime sorting.

From highest to lowest: `Primary` > `High` > `Medium` (default) > `Low` > `End`

```csharp
public class ShieldSystem
{
    [OnEvent(SubscriberPriority.High)] // runs before default Medium
    public bool OnDamage(DamageEvent e)
    {
        if (HasShield) { AbsorbDamage(e.Amount); return false; }
        return true;
    }
}

public class HUDDisplay
{
    [OnEvent] // default Medium; runs after ShieldSystem
    public void OnDamage(DamageEvent e) => UpdateHealthBar(e.Amount);
}
```

## Canceling Propagation

Call `.Cancelable()` when publishing. After that, if any handler returns `false`, propagation stops immediately; later subscribers will not receive the event and `Publish` returns `false`.

If you do **not** call `Cancelable()`, handler return values are ignored and the event is always delivered to all subscribers.

```csharp
// With Cancelable: if ShieldSystem (High) returns false, HUDDisplay (Medium) is not called
bool handled = new DamageEvent { Amount = 10 }
    .Cancelable()
    .Publish();
// handled == false means propagation was stopped

// Without Cancelable: all subscribers are called; return values are ignored
new DamageEvent { Amount = 10 }.Publish();
```

## Publish Filters

The following APIs are **per‑publish** fluent options. They affect only the current publish call and do not change subscription registration. They can be freely combined:

| API                                        | Description                                                |
| ------------------------------------------ | ---------------------------------------------------------- |
| `evt.Cancelable()`                         | Allow handlers to stop propagation by returning `false`    |
| `evt.WithFilter(Predicate<object> filter)` | Skip a subscriber when `filter(subscriber)` returns `true` |
| `evt.OnlyType<TEvent, TSubscriber>()`      | Deliver only to subscribers of type `TSubscriber`          |
| `evt.ExcludeType<TEvent, TSubscriber>()`   | Exclude subscribers of type `TSubscriber`                  |
| `evt.OnlySubscriber(subscriber)`           | Deliver only to the specified instance                     |
| `evt.ExcludeSubscriber(subscriber)`        | Exclude the specified instance                             |
| `evt.OnlySubscribers(HashSet<object>)`     | Deliver only to the instances in the given set             |
| `evt.ExcludeSubscribers(HashSet<object>)`  | Exclude all instances in the given set                     |

```csharp
// Notify only the UI layer and avoid game logic
new DamageEvent { Amount = 5 }
    .OnlyType<DamageEvent, HUDDisplay>()
    .Publish();

// Exclude self to avoid receiving events you published
new DamageEvent { Amount = 5 }
    .ExcludeSubscriber(this)
    .Publish();

// Combine: cancelable + only a specific type
new DamageEvent { Amount = 5 }
    .Cancelable()
    .OnlyType<DamageEvent, ShieldSystem>()
    .Publish();

// Exclude multiple instances
var exclude = new HashSet<object> { enemyA, enemyB };
new DamageEvent { Amount = 5 }.ExcludeSubscribers(exclude).Publish();
```

## Async Support

Change the handler signature to return `Task` or `Task<bool>` to define an async handler—no extra configuration is required.

```csharp
public class NetworkSync
{
    [OnEvent]
    public async Task OnDamage(DamageEvent e)
    {
        await SendToServerAsync(e);
    }
}

// Async publish: awaits each handler in priority order
bool completed = await new DamageEvent { Amount = 10 }.PublishAsync();
```

A single subscriber can define both sync and async handlers for the same event type. They are triggered by `Publish` and `PublishAsync` respectively:

```csharp
public class CombatLogger
{
    [OnEvent]
    public void OnDamage(DamageEvent e)              // triggered by Publish()
    {
        LogToFile(e);
    }

    [OnEvent]
    public async Task OnDamageAsync(DamageEvent e)   // triggered by PublishAsync() (note: the sync handler is also invoked)
    {
        await LogToRemoteAsync(e);
    }
}
```

---

# Source Generator Constraints & Diagnostics

The generator enforces a clear set of rules for events and `[OnEvent]` methods. Violations are reported as **compile‑time diagnostics**; they never fail silently.

**Event constraints**

- Must be a `struct` that implements `IGenEvent<T>`.

**Handler method constraints**

- Must be a `public` instance method.
- Must take exactly one parameter whose type implements `IGenEvent<>`.
- Return type must be `void`, `bool`, `Task`, or `Task<bool>`.
- For a given class and event type, there can be at most one sync handler and one async handler.

**Diagnostic codes**

| Code | Severity | Meaning                                                                 |
| ---- | -------- | ----------------------------------------------------------------------- |
| GE001 | Warning | `IGenEvent` interface not found; check your GenEvent reference          |
| GE002 | Warning | `OnEventAttribute` not found; check your GenEvent reference             |
| GE010 | Error   | `[OnEvent]` method must be `public`                                     |
| GE011 | Error   | `[OnEvent]` method must have exactly one parameter                      |
| GE012 | Error   | `[OnEvent]` parameter type must implement `IGenEvent`                   |
| GE013 | Error   | A class cannot have two sync or two async handlers for the same event   |
| GE014 | Error   | `[OnEvent]` return type must be `void`, `bool`, `Task`, or `Task<bool>` |
| GE999 | Error   | Internal generator error; see compiler output for details               |

If you see GE001/GE002, verify that both the core library and the source generator are correctly referenced. For GE010–GE014, adjust method signatures according to the table above. For GE999, inspect the full compiler output for more details.

# License

This project is licensed under the [MIT License](LICENSE).  
Copyright (c) 2026 Puring.  
See the [LICENSE](LICENSE) file for full text.

