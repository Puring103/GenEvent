# Publish Performance Optimization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce GenEvent publish hot-path CPU cost while preserving zero-allocation behavior and defining stable publish-time subscription mutation semantics.

**Architecture:** Add explicit publish-depth tracking to `GenEventRegistry<TGenEvent, TSubscriber>`, defer `Register` / `UnRegister` operations during publish, and replace publish-time snapshot iteration with direct `SubscriberList` iteration once deferred mutation is verified. Add fast paths for default config and single-target `OnlySubscriber` paths before touching broader filter behavior.

**Tech Stack:** C# / .NET 10, NUnit, BenchmarkDotNet, GenEvent source generator templates.

---

## File Structure

- Modify: `src/GenEvent/src/PublishConfig.cs`
  - Expose internal query properties for default config and single `OnlySubscriber` config.
  - Keep custom predicate fallback behavior unchanged.

- Modify: `src/GenEvent/src/GenEventRegistry.cs`
  - Add per `(event type, subscriber type)` publish-depth tracking.
  - Add pending subscription mutation storage and apply operations when outermost publish exits.
  - Expose direct subscriber list access for generated publisher hot paths.
  - Remove legacy publish-time snapshot APIs after direct iteration is adopted.

- Modify: `src/GenEvent/src/PublisherHelper.cs`
  - Add default-config fast path.
  - Keep generic fallback behavior for filtered / cancelable / custom predicate paths.

- Modify: `src/GenEvent.SourceGenerator/Templates.cs`
  - Generate publisher code that wraps each registry publish with `BeginPublish()` / `EndPublish()`.
  - Generate direct-list invocation for sync and async publish instead of always taking snapshots.

- Modify: `src/GenEvent.SourceGenerator/GenEventSourceGenerator.cs`
  - Update generated publisher snippets to use the new registry publish scope and direct subscribers property.

- Modify: `tests/TestTypes.cs`
  - Add test subscribers that start and stop subscriptions during publish.

- Create: `tests/PublishMutationTests.cs`
  - Lock down deferred subscription mutation semantics.

- Modify: `tests/AllocationRegressionTests.cs`
  - Confirm default publish and built-in filter paths remain zero-allocation after removing snapshots.

- Modify: `Benchmarks/GenEvent.Benchmarks/Benchmarks/PublishBenchmarks.cs`
  - Keep existing publish benchmark coverage and add targeted only-subscriber benchmark only if needed.

- Modify: `docs/benchmark-analysis-2026-04-29.md`
  - Update final results after implementation and benchmark verification.

---

## Task 1: Lock Down Publish-Time Subscription Mutation Semantics

**Files:**
- Modify: `tests/TestTypes.cs`
- Create: `tests/PublishMutationTests.cs`

- [ ] **Step 1: Add mutation test subscribers**

Append these classes to `tests/TestTypes.cs`:

```csharp
public class StopSelfDuringPublishSubscriber
{
    public int ReceiveCount;

    [OnEvent]
    public bool OnTestEventA(TestEventA e)
    {
        ReceiveCount++;
        this.StopListening();
        return true;
    }
}

public class StopOtherDuringPublishSubscriber
{
    private readonly object _target;

    public StopOtherDuringPublishSubscriber(object target)
    {
        _target = target;
    }

    public int ReceiveCount;

    [OnEvent]
    public bool OnTestEventA(TestEventA e)
    {
        ReceiveCount++;
        _target.StopListening();
        return true;
    }
}

public class StartOtherDuringPublishSubscriber
{
    private readonly object _target;

    public StartOtherDuringPublishSubscriber(object target)
    {
        _target = target;
    }

    public int ReceiveCount;

    [OnEvent]
    public bool OnTestEventA(TestEventA e)
    {
        ReceiveCount++;
        _target.StartListening();
        return true;
    }
}

public class CountingMutationTargetSubscriber
{
    public int ReceiveCount;

    [OnEvent]
    public bool OnTestEventA(TestEventA e)
    {
        ReceiveCount++;
        return true;
    }
}
```

- [ ] **Step 2: Add failing mutation semantics tests**

Create `tests/PublishMutationTests.cs`:

```csharp
using GenEvent;

namespace Tests;

[TestFixture]
public class PublishMutationTests
{
    [SetUp]
    public void SetUp()
    {
        TestRuntimeState.ResetAndInit();
    }

    [TearDown]
    public void TearDown()
    {
        TestRuntimeState.Reset();
    }

    [Test]
    public void StopListening_DuringPublish_TakesEffectOnNextPublish()
    {
        var selfStopping = new StopSelfDuringPublishSubscriber();

        selfStopping.StartListening();

        new TestEventA { Value = 1 }.Publish();
        new TestEventA { Value = 2 }.Publish();

        Assert.That(selfStopping.ReceiveCount, Is.EqualTo(1));
    }

    [Test]
    public void StopOther_DuringPublish_DoesNotRemoveOtherFromCurrentPublish()
    {
        var target = new CountingMutationTargetSubscriber();
        var stopper = new StopOtherDuringPublishSubscriber(target);

        stopper.StartListening();
        target.StartListening();

        new TestEventA { Value = 1 }.Publish();
        new TestEventA { Value = 2 }.Publish();

        Assert.That(stopper.ReceiveCount, Is.EqualTo(2));
        Assert.That(target.ReceiveCount, Is.EqualTo(1));
    }

    [Test]
    public void StartOther_DuringPublish_TakesEffectOnNextPublish()
    {
        var target = new CountingMutationTargetSubscriber();
        var starter = new StartOtherDuringPublishSubscriber(target);

        starter.StartListening();

        new TestEventA { Value = 1 }.Publish();
        new TestEventA { Value = 2 }.Publish();

        Assert.That(starter.ReceiveCount, Is.EqualTo(2));
        Assert.That(target.ReceiveCount, Is.EqualTo(1));
    }
}
```

- [ ] **Step 3: Run tests and verify RED**

Run:

```powershell
dotnet test Tests\Tests.csproj -c Release --filter PublishMutationTests
```

Expected:

- At least `StopListening_DuringPublish_TakesEffectOnNextPublish` fails under current snapshot behavior because `StopListening` currently applies immediately.
- The failure proves the new deferred-mutation semantics are not already implemented.

- [ ] **Step 4: Commit**

```powershell
git add tests\TestTypes.cs tests\PublishMutationTests.cs
git commit -m "test: define publish-time subscription mutation semantics"
```

---

## Task 2: Add Deferred Register / Unregister Support To Registry

**Files:**
- Modify: `src/GenEvent/src/GenEventRegistry.cs`

- [ ] **Step 1: Add pending operation state**

In `GenEventRegistry<TGenEvent, TSubscriber>`, add these members near `SnapshotPool`:

```csharp
private enum PendingOperationKind : byte
{
    Register,
    Unregister
}

private readonly struct PendingOperation
{
    public PendingOperation(PendingOperationKind kind, TSubscriber subscriber)
    {
        Kind = kind;
        Subscriber = subscriber;
    }

    public PendingOperationKind Kind { get; }

    public TSubscriber Subscriber { get; }
}

private static int _publishDepth;
private static readonly List<PendingOperation> PendingOperations = new();
```

- [ ] **Step 2: Add publish scope methods**

Add these methods to `GenEventRegistry<TGenEvent, TSubscriber>`:

```csharp
public static void BeginPublish()
{
    _publishDepth++;
}

public static void EndPublish()
{
    _publishDepth--;
    if (_publishDepth == 0)
    {
        ApplyPendingOperations();
    }
}

private static void ApplyPendingOperations()
{
    if (PendingOperations.Count == 0)
    {
        return;
    }

    for (int i = 0; i < PendingOperations.Count; i++)
    {
        var operation = PendingOperations[i];
        if (operation.Kind == PendingOperationKind.Register)
        {
            RegisterImmediate(operation.Subscriber);
        }
        else
        {
            UnRegisterImmediate(operation.Subscriber);
        }
    }

    PendingOperations.Clear();
}
```

- [ ] **Step 3: Split immediate register / unregister implementations**

Replace current `Register` and `UnRegister` implementations with:

```csharp
public static void Register(TSubscriber observer)
{
    if (_publishDepth > 0)
    {
        PendingOperations.Add(new PendingOperation(PendingOperationKind.Register, observer));
        return;
    }

    RegisterImmediate(observer);
}

public static void UnRegister(TSubscriber observer)
{
    if (_publishDepth > 0)
    {
        PendingOperations.Add(new PendingOperation(PendingOperationKind.Unregister, observer));
        return;
    }

    UnRegisterImmediate(observer);
}

private static void RegisterImmediate(TSubscriber observer)
{
    if (SubscriberIndex.ContainsKey(observer))
        return;
    var index = SubscriberList.Count;
    SubscriberList.Add(observer);
    SubscriberIndex.Add(observer, index);
}

private static void UnRegisterImmediate(TSubscriber observer)
{
    if (!SubscriberIndex.TryGetValue(observer, out var index))
        return;
    var lastIndex = SubscriberList.Count - 1;
    SubscriberIndex.Remove(observer);
    if (index != lastIndex)
    {
        var last = SubscriberList[lastIndex];
        SubscriberList[index] = last;
        SubscriberIndex[last] = index;
    }
    SubscriberList.RemoveAt(lastIndex);
}
```

- [ ] **Step 4: Run mutation tests**

Run:

```powershell
dotnet test Tests\Tests.csproj -c Release --filter PublishMutationTests
```

Expected:

- Tests may still fail until generated publisher wraps publish with `BeginPublish` / `EndPublish`.
- If they pass because snapshot still isolates traversal, continue to Task 3 to remove snapshot safely.

- [ ] **Step 5: Commit**

```powershell
git add src\GenEvent\src\GenEventRegistry.cs
git commit -m "feat: defer subscription mutations during publish"
```

---

## Task 3: Generate Direct Subscriber Iteration With Publish Scopes

**Files:**
- Modify: `src/GenEvent/src/GenEventRegistry.cs`
- Modify: `src/GenEvent.SourceGenerator/Templates.cs`
- Modify: `src/GenEvent.SourceGenerator/GenEventSourceGenerator.cs`

- [ ] **Step 1: Expose direct subscribers list internally**

In `GenEventRegistry<TGenEvent, TSubscriber>`, add:

```csharp
public static IReadOnlyList<TSubscriber> DirectSubscribers => SubscriberList;
```

Keep `Subscribers` for inspection and remove the legacy `TakeSubscribersSnapshot` / `ReturnSubscribersSnapshot` APIs after generated publishers stop using snapshots.

- [ ] **Step 2: Update generated publisher template placeholders**

In `Templates.EventPublisher`, keep the `try/finally` structure but rename intent conceptually through generated content. No template string field names must change if `GenEventSourceGenerator.cs` continues replacing the same placeholders.

The generated code should become equivalent to:

```csharp
GenEventRegistry<TGenEvent, SomeSubscriber>.BeginPublish();
try
{
    completed = @event.Invoke<SomeSubscriber, TGenEvent>(
        config,
        GenEventRegistry<TGenEvent, SomeSubscriber>.DirectSubscribers);
    if (!completed) return false;
}
finally
{
    GenEventRegistry<TGenEvent, SomeSubscriber>.EndPublish();
}
```

- [ ] **Step 3: Change source generator sync publisher generation**

In `GenerateEventPublisher`, replace sync snapshot declaration generation:

```csharp
syncSnapshotDeclarations.AppendLine($"        var {snapshotVariableName} = GenEventRegistry<TGenEvent, {subscriberTypeName}>.TakeSubscribersSnapshot();");
syncSnapshotReturns.AppendLine($"            GenEventRegistry<TGenEvent, {subscriberTypeName}>.ReturnSubscribersSnapshot({snapshotVariableName});");
syncInvocations.AppendLine($"            completed = @event.Invoke<{subscriberTypeName}, TGenEvent>(config, {snapshotVariableName});");
```

with:

```csharp
syncSnapshotDeclarations.AppendLine($"        GenEventRegistry<TGenEvent, {subscriberTypeName}>.BeginPublish();");
syncSnapshotReturns.AppendLine($"            GenEventRegistry<TGenEvent, {subscriberTypeName}>.EndPublish();");
syncInvocations.AppendLine($"            completed = @event.Invoke<{subscriberTypeName}, TGenEvent>(config, GenEventRegistry<TGenEvent, {subscriberTypeName}>.DirectSubscribers);");
```

- [ ] **Step 4: Change source generator async publisher generation**

Replace async snapshot generation:

```csharp
asyncSnapshotDeclarations.AppendLine($"        var {snapshotVariableName} = GenEventRegistry<TGenEvent, {subscriberTypeName}>.TakeSubscribersSnapshot();");
asyncSnapshotReturns.AppendLine($"            GenEventRegistry<TGenEvent, {subscriberTypeName}>.ReturnSubscribersSnapshot({snapshotVariableName});");
asyncInvocations.AppendLine($"            completed = await @event.InvokeAsync<{subscriberTypeName}, TGenEvent>(config, {snapshotVariableName});");
```

with:

```csharp
asyncSnapshotDeclarations.AppendLine($"        GenEventRegistry<TGenEvent, {subscriberTypeName}>.BeginPublish();");
asyncSnapshotReturns.AppendLine($"            GenEventRegistry<TGenEvent, {subscriberTypeName}>.EndPublish();");
asyncInvocations.AppendLine($"            completed = await @event.InvokeAsync<{subscriberTypeName}, TGenEvent>(config, GenEventRegistry<TGenEvent, {subscriberTypeName}>.DirectSubscribers);");
```

- [ ] **Step 5: Run mutation tests and core publish tests**

Run:

```powershell
dotnet test Tests\Tests.csproj -c Release --filter "PublishMutationTests|CoreFlowTests|CancelAndPriorityTests|FilterTests|AsyncTests"
```

Expected:

- All selected tests pass.
- Mutation tests prove publish-time subscription changes apply after current publish completes.

- [ ] **Step 6: Run allocation regression tests**

Run:

```powershell
dotnet test Tests\Tests.csproj -c Release --filter AllocationRegressionTests
```

Expected:

- All allocation regression tests pass.
- Default publish and built-in filter paths remain zero-allocation.

- [ ] **Step 7: Commit**

```powershell
git add src\GenEvent\src\GenEventRegistry.cs src\GenEvent.SourceGenerator\Templates.cs src\GenEvent.SourceGenerator\GenEventSourceGenerator.cs
git commit -m "perf: iterate subscribers directly during publish"
```

---

## Task 4: Add Default Publish Fast Path

**Files:**
- Modify: `src/GenEvent/src/PublishConfig.cs`
- Modify: `src/GenEvent/src/PublisherHelper.cs`

- [ ] **Step 1: Add config query properties**

In `PublishConfig<TGenEvent>`, add:

```csharp
internal bool HasFilters => _ruleCount != 0 || _customFilter != null;

internal bool IsDefault => !Cancelable && !HasFilters;
```

- [ ] **Step 2: Add fast path in sync Invoke**

At the start of `PublisherHelper.Invoke<TSubscriber, TGenEvent>`, after `genEvent` is loaded, add:

```csharp
if (config.IsDefault)
{
    if (genEvent == null)
    {
        return true;
    }

    for (int i = 0; i < subscribers.Count; i++)
    {
        genEvent(gameEvent, subscribers[i]);
    }

    return true;
}
```

Keep existing fallback loop unchanged after this block.

- [ ] **Step 3: Add fast path in async InvokeAsync sync-delegate branch**

In `PublisherHelper.InvokeAsync<TSubscriber, TGenEvent>`, add a default branch before filtered / cancelable logic:

```csharp
if (config.IsDefault && genEventAsync == null)
{
    if (genEvent == null)
    {
        return true;
    }

    for (int i = 0; i < subscribers.Count; i++)
    {
        genEvent(gameEvent, subscribers[i]);
    }

    return true;
}
```

Do not skip awaiting `genEventAsync` when an async delegate exists.

- [ ] **Step 4: Run tests**

Run:

```powershell
dotnet test Tests\Tests.csproj -c Release
```

Expected:

- All tests pass.

- [ ] **Step 5: Run targeted benchmark**

Run:

```powershell
dotnet run -c Release --project Benchmarks\GenEvent.Benchmarks\GenEvent.Benchmarks.csproj -- --filter "*PublishSubscriberBenchmarks*"
```

Expected:

- `Publish_Subscribers` improves versus the current baseline:
  - Current `SubscriberCount=1`: about `28.53 ns`
  - Current `SubscriberCount=10`: about `41.99 ns`
- Allocated remains `-`.

- [ ] **Step 6: Commit**

```powershell
git add src\GenEvent\src\PublishConfig.cs src\GenEvent\src\PublisherHelper.cs
git commit -m "perf: add default publish fast path"
```

---

## Task 5: Add Single `OnlySubscriber` Fast Path

**Files:**
- Modify: `src/GenEvent/src/PublishConfig.cs`
- Modify: `src/GenEvent/src/PublisherHelper.cs`
- Add tests if needed: `tests/FilterTests.cs`

- [ ] **Step 1: Add single target query to PublishConfig**

In `PublishConfig<TGenEvent>`, add:

```csharp
internal bool TryGetOnlySubscriber(out object subscriber)
{
    if (!Cancelable && _customFilter == null && _ruleCount == 1 && _rule0.Kind == FilterRuleKind.OnlySubscriber)
    {
        subscriber = _rule0.Payload;
        return true;
    }

    subscriber = null;
    return false;
}
```

If `FilterRule.Kind` or `Payload` is inaccessible because the properties are not visible enough, keep them as internal readonly properties.

- [ ] **Step 2: Add sync only-subscriber fast path**

In `PublisherHelper.Invoke<TSubscriber, TGenEvent>`, after default fast path and before generic fallback:

```csharp
if (config.TryGetOnlySubscriber(out var onlySubscriber))
{
    if (onlySubscriber is TSubscriber typedSubscriber)
    {
        return genEvent?.Invoke(gameEvent, typedSubscriber) ?? true;
    }

    return true;
}
```

This path is only valid when the config has exactly one `OnlySubscriber` rule and is not cancelable.

- [ ] **Step 3: Add async only-subscriber fast path**

In `PublisherHelper.InvokeAsync<TSubscriber, TGenEvent>`, after default fast path and before generic fallback:

```csharp
if (config.TryGetOnlySubscriber(out var onlySubscriber))
{
    if (onlySubscriber is not TSubscriber typedSubscriber)
    {
        return true;
    }

    if (genEventAsync != null)
    {
        return await genEventAsync(gameEvent, typedSubscriber);
    }

    return genEvent?.Invoke(gameEvent, typedSubscriber) ?? true;
}
```

- [ ] **Step 4: Add explicit test for only-subscriber behavior if coverage is insufficient**

If `tests/FilterTests.cs` does not already assert this exact behavior, add:

```csharp
[Test]
public void OnlySubscriber_OnlyTargetReceives()
{
    var target = new SubscriberA();
    var other = new SubscriberB();
    target.StartListening();
    other.StartListening();

    try
    {
        new TestEventA { Value = 5 }.OnlySubscriber(target).Publish();

        Assert.That(target.ReceiveCount, Is.EqualTo(1));
        Assert.That(target.LastValue, Is.EqualTo(5));
        Assert.That(other.ReceiveCount, Is.EqualTo(0));
    }
    finally
    {
        target.StopListening();
        other.StopListening();
    }
}
```

- [ ] **Step 5: Run filter and async tests**

Run:

```powershell
dotnet test Tests\Tests.csproj -c Release --filter "FilterTests|AsyncTests|AllocationRegressionTests"
```

Expected:

- All selected tests pass.
- Built-in filter allocation regression remains zero allocation.

- [ ] **Step 6: Run targeted benchmark**

Run:

```powershell
dotnet run -c Release --project Benchmarks\GenEvent.Benchmarks\GenEvent.Benchmarks.csproj -- --filter "*PublishSubscriberBenchmarks*|*AsyncPublishBenchmarks*"
```

Expected:

- `Publish_ConfiguredEventPipeline` improves, especially at `SubscriberCount=10`.
- `Publish_BuiltInFilter` improves only if the benchmark uses `OnlySubscriber`; `ExcludeSubscriber` still scans.
- Allocated remains `-` for built-in filter paths.

- [ ] **Step 7: Commit**

```powershell
git add src\GenEvent\src\PublishConfig.cs src\GenEvent\src\PublisherHelper.cs tests\FilterTests.cs
git commit -m "perf: add only-subscriber publish fast path"
```

---

## Task 6: Sync Unity Runtime Copy

**Files:**
- Modify generated/copied runtime files under `src/GenEvent.Unity/Assets/Plugins/GenEvent/Runtime/`

- [ ] **Step 1: Build release to trigger existing copy workflow**

Run:

```powershell
dotnet build Benchmarks\GenEvent.Benchmarks\GenEvent.Benchmarks.csproj -c Release
```

Expected:

- Build succeeds.
- Existing build hooks copy runtime source to Unity runtime.

- [ ] **Step 2: Inspect Unity runtime diff**

Run:

```powershell
git diff -- src/GenEvent.Unity/Assets/Plugins/GenEvent/Runtime
```

Expected:

- Unity runtime copies match the core runtime changes.
- Do not include unrelated generated binaries unless explicitly required.

- [ ] **Step 3: Commit**

```powershell
git add src\GenEvent.Unity\Assets\Plugins\GenEvent\Runtime
git commit -m "chore: sync Unity runtime performance changes"
```

---

## Task 7: Final Verification And Report Update

**Files:**
- Modify: `docs/benchmark-analysis-2026-04-29.md`

- [ ] **Step 1: Run full test suite**

Run:

```powershell
dotnet test Tests\Tests.csproj -c Release
```

Expected:

- All tests pass.

- [ ] **Step 2: Run full short benchmark suite**

Run:

```powershell
Write-Output "0 1 2 3 4 5 6" | dotnet run -c Release --project Benchmarks\GenEvent.Benchmarks\GenEvent.Benchmarks.csproj
```

Expected:

- Benchmark completes under 5 minutes.
- `Publish_Subscribers` improves against baseline.
- `Publish_ConfiguredEventPipeline` improves against baseline if only-subscriber fast path is active.
- Zero-allocation rows remain zero-allocation.

- [ ] **Step 3: Update benchmark analysis report**

Update `docs/benchmark-analysis-2026-04-29.md` by adding a new `## Optimization Result` section. Copy exact runtime and benchmark row values from the artifacts generated by Step 2:

- Full short benchmark runtime: copy `Global total time` from the latest `BenchmarkRun-*.log`.
- `Publish_Subscribers` before values: `28.53 ns` at `SubscriberCount=1`, `41.99 ns` at `SubscriberCount=10`.
- `Publish_Subscribers` after values: copy both rows from `BenchmarkDotNet.Artifacts/results/GenEvent.Benchmarks.Benchmarks.PublishSubscriberBenchmarks-report-github.md`.
- `Publish_ConfiguredEventPipeline` before values: `36.69 ns` at `SubscriberCount=1`, `139.57 ns` at `SubscriberCount=10`.
- `Publish_ConfiguredEventPipeline` after values: copy both rows from `BenchmarkDotNet.Artifacts/results/GenEvent.Benchmarks.Benchmarks.PublishSubscriberBenchmarks-report-github.md`.
- Allocation status: copy the `Allocated` column for default, built-in filter, configured pipeline, and custom predicate rows.

Use this exact section structure:

```markdown
## Optimization Result

After direct subscriber iteration, deferred publish-time subscription mutation, default publish fast path, and only-subscriber fast path:

- Full short benchmark runtime: use the copied `Global total time` value from the latest benchmark log.
- `Publish_Subscribers` before: `28.53 ns / 41.99 ns`.
- `Publish_Subscribers` after: use the copied `SubscriberCount=1` and `SubscriberCount=10` mean values from the latest publish subscriber report.
- `Publish_ConfiguredEventPipeline` before: `36.69 ns / 139.57 ns`.
- `Publish_ConfiguredEventPipeline` after: use the copied `SubscriberCount=1` and `SubscriberCount=10` mean values from the latest publish subscriber report.
- Built-in zero-allocation paths remain zero-allocation if their `Allocated` column is `-`.
- Custom predicate fallback remains `96 B` per operation if its `Allocated` column is `96 B`.
```

- [ ] **Step 4: Commit**

```powershell
git add docs\benchmark-analysis-2026-04-29.md
git commit -m "docs: update benchmark analysis after publish optimization"
```

---

## Rollback Strategy

If direct iteration causes semantic regressions:

1. Keep Task 4 default config fast path.
2. Revert Task 3 direct iteration changes.
3. Keep Task 1 mutation tests and adjust expected behavior back to snapshot semantics only if the product decision changes.

If only-subscriber fast path causes edge-case bugs:

1. Revert Task 5 only.
2. Keep Task 1-4 changes.
3. Add a regression test for the failing filter combination before retrying.

If full benchmark exceeds 5 minutes:

1. Keep benchmark implementation.
2. Move `DispatchBaselineBenchmarks` or lifecycle benchmarks out of the default run through README guidance or benchmark filter documentation.
3. Do not weaken production tests to reduce benchmark time.

---

## Self-Review

- Spec coverage: The plan covers deferred publish-time subscription mutation, direct subscriber iteration, default publish fast path, only-subscriber fast path, Unity runtime sync, tests, benchmark verification, and report update.
- Placeholder scan: No TBD/TODO placeholders remain. Task 7 instructs the implementer to copy exact values from generated BenchmarkDotNet artifacts instead of inventing values in advance.
- Type consistency: The plan uses existing `PublishConfig<TGenEvent>`, `GenEventRegistry<TGenEvent, TSubscriber>`, `PublisherHelper.Invoke`, and generated publisher paths.
