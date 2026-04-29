# Post-Review Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix four issues found in the v0.10.0 code review: O(1) ContainsSubscriber, RegistryGuardTests isolation, missing mutation/init tests, and async path migration to BeginPublish/EndPublish.

**Architecture:** Tasks are ordered to do non-TDD mechanical fixes first (Issues B and D), then pure test additions (Issue C), then TDD-style async migration (Issue A) where we write a discriminating failing test before implementing the generator change. Each task ends with a commit. The async migration is the most involved change: the source generator's async publisher path still uses `TakeSubscribersSnapshot`/`ReturnSubscribersSnapshot`; we replace it with `BeginPublish()`/`EndPublish()` + `DirectSubscribers` to match the sync path and remove the dead snapshot pool code.

**Tech Stack:** C# / .NET 10, NUnit 4, GenEvent source generator (Roslyn ISourceGenerator), MSBuild copy targets for Unity.

---

## File Structure

- Modify: `src/GenEvent/src/GenEventRegistry.cs` — replace O(n) `ContainsSubscriber` with O(1) dict lookup; remove dead `SnapshotPool`, `TakeSubscribersSnapshot`, `ReturnSubscribersSnapshot` members (after Task 6)
- Modify: `src/GenEvent.Unity/Assets/Plugins/GenEvent/Runtime/GenEventRegistry.cs` — same changes (Unity runtime mirror, auto-synced by MSBuild build target, but we manually sync in Task 8)
- Modify: `src/GenEvent.SourceGenerator/GenEventSourceGenerator.cs` — migrate async publisher generation from snapshot to BeginPublish/EndPublish
- Modify: `Tests/RegistryGuardTests.cs` — add `[SetUp]`/`[TearDown]` calling `TestRuntimeState.Reset()`
- Modify: `Tests/BootstrapContractTests.cs` — add `StopListeningForSpecificEvent_WithoutInit` test
- Modify: `Tests/PublishMutationTests.cs` — add queue-ordering tests, post-dequeue OnlySubscriber test, async mutation tests
- Modify: `Tests/AllocationRegressionTests.cs` — add `PublishAsync_DefaultPath_HasNoSteadyStateAllocations`
- Modify: `Tests/TestTypes.cs` — add `StopOtherThenStartOtherDuringPublishSubscriber`, `StartOtherThenStopOtherDuringPublishSubscriber`, `AsyncStopSelfDuringPublishSubscriber`, `AsyncStartOtherAndCheckCountSubscriber`

---

## Task 1: Fix ContainsSubscriber O(1) (Issue B)

**Files:**
- Modify: `src/GenEvent/src/GenEventRegistry.cs`

`ContainsSubscriber` currently does a linear `ReferenceEquals` scan over `SubscriberList`. `SubscriberIndex` is already a `Dictionary<TSubscriber, int>` keyed by `ReferenceEqualityComparer`, so O(1) lookup is available. This matters because `ContainsSubscriber` is called on every `OnlySubscriber` fast-path publish.

- [ ] **Step 1: Replace linear scan with dict lookup**

In `src/GenEvent/src/GenEventRegistry.cs`, replace the `ContainsSubscriber` method body (lines 123–133):

```csharp
public static bool ContainsSubscriber(TSubscriber subscriber)
{
    return SubscriberIndex.ContainsKey(subscriber);
}
```

The full method after edit:

```csharp
public static bool ContainsSubscriber(TSubscriber subscriber)
{
    return SubscriberIndex.ContainsKey(subscriber);
}
```

- [ ] **Step 2: Run tests to verify no regression**

```bash
dotnet test Tests/Tests.csproj -c Release
```

Expected: `Passed! - Failed: 0, Passed: 148`

- [ ] **Step 3: Commit**

```bash
git add src/GenEvent/src/GenEventRegistry.cs
git commit -m "perf: replace ContainsSubscriber linear scan with O(1) dict lookup"
```

---

## Task 2: Add RegistryGuardTests Isolation (Issue D)

**Files:**
- Modify: `Tests/RegistryGuardTests.cs`

The test calls `EndPublish()` directly without any `[SetUp]`. If another test ever leaked a non-zero `_publishDepth` for `GenEventRegistry<TestEventB, ReferenceEqualitySubscriberForB>`, this test would silently pass for the wrong reason. Adding `TestRuntimeState.Reset()` in `SetUp`/`TearDown` guards against cross-test pollution and follows the pattern used by all other test fixtures.

- [ ] **Step 1: Add SetUp and TearDown**

Replace the entire `Tests/RegistryGuardTests.cs` with:

```csharp
using GenEvent;

namespace Tests;

[TestFixture]
public class RegistryGuardTests
{
    [SetUp]
    public void SetUp()
    {
        TestRuntimeState.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        TestRuntimeState.Reset();
    }

    [Test]
    public void EndPublish_WithoutMatchingBegin_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GenEventRegistry<TestEventB, ReferenceEqualitySubscriberForB>.EndPublish());
    }
}
```

- [ ] **Step 2: Run tests**

```bash
dotnet test Tests/Tests.csproj -c Release --filter RegistryGuardTests
```

Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 3: Commit**

```bash
git add Tests/RegistryGuardTests.cs
git commit -m "test: add SetUp/TearDown isolation to RegistryGuardTests"
```

---

## Task 3: Add Missing Tests — Pre-Init and Mutation Queue Ordering (Issue C)

**Files:**
- Modify: `Tests/BootstrapContractTests.cs`
- Modify: `Tests/TestTypes.cs`
- Modify: `Tests/PublishMutationTests.cs`

This task adds five missing test cases:
1. `StopListening<T, TEvent>()` before init throws (symmetric to the existing `StartListeningForSpecificEvent_WithoutInit` test)
2. Pending queue ordering: unregister-then-register → net registered
3. Pending queue ordering: register-then-unregister → net unregistered
4. Post-dequeue `OnlySubscriber` publish: after pending unregister applied, `OnlySubscriber` correctly skips the now-unregistered target

- [ ] **Step 1: Add StopListeningForSpecificEvent_WithoutInit test to BootstrapContractTests**

In `Tests/BootstrapContractTests.cs`, add this test after `StartListeningForSpecificEvent_WithoutInit_ThrowsInvalidOperationException` (line 83):

```csharp
[Test]
public void StopListeningForSpecificEvent_WithoutInit_ThrowsInvalidOperationException()
{
    var subscriber = new MultiEventSubscriber();

    var exception = Assert.Throws<InvalidOperationException>(() => subscriber.StopListening<MultiEventSubscriber, TestEventA>());

    Assert.That(exception!.Message, Does.Contain(nameof(TestEventA)));
    Assert.That(exception.Message, Does.Contain("GenEventBootstrap.Init()"));
}
```

- [ ] **Step 2: Add two new subscriber types to TestTypes.cs**

At the end of `Tests/TestTypes.cs`, append:

```csharp
public class StopOtherThenStartOtherDuringPublishSubscriber
{
    private readonly object _target;
    public int ReceiveCount;

    public StopOtherThenStartOtherDuringPublishSubscriber(object target)
    {
        _target = target;
    }

    [OnEvent]
    public bool OnTestEventA(TestEventA e)
    {
        ReceiveCount++;
        _target.StopListening();
        _target.StartListening();
        return true;
    }
}

public class StartOtherThenStopOtherDuringPublishSubscriber
{
    private readonly object _target;
    public int ReceiveCount;

    public StartOtherThenStopOtherDuringPublishSubscriber(object target)
    {
        _target = target;
    }

    [OnEvent]
    public bool OnTestEventA(TestEventA e)
    {
        ReceiveCount++;
        _target.StartListening();
        _target.StopListening();
        return true;
    }
}
```

- [ ] **Step 3: Add four mutation tests to PublishMutationTests.cs**

In `Tests/PublishMutationTests.cs`, add the following four tests inside the `PublishMutationTests` class after the last existing test:

```csharp
[Test]
public void PendingOperations_UnregisterThenRegister_NetResultIsRegistered()
{
    var target = new SubscriberA();
    var controller = new StopOtherThenStartOtherDuringPublishSubscriber(target);

    target.StartListening();
    controller.StartListening();

    // During publish: stop target then start target (deferred in FIFO order)
    new TestEventA { Value = 1 }.Publish();
    // Pending applied: Unregister(target) then Register(target) → target still registered
    new TestEventA { Value = 2 }.Publish();

    // target received first publish (was registered), then received second publish (re-registered via pending)
    Assert.That(target.ReceiveCount, Is.EqualTo(2));
    Assert.That(controller.ReceiveCount, Is.EqualTo(2));
}

[Test]
public void PendingOperations_RegisterThenUnregister_NetResultIsUnregistered()
{
    var target = new SubscriberA();
    var controller = new StartOtherThenStopOtherDuringPublishSubscriber(target);

    // target NOT registered at start
    controller.StartListening();

    // During publish: start target then stop target (deferred in FIFO order)
    new TestEventA { Value = 1 }.Publish();
    // Pending applied: Register(target) then Unregister(target) → target NOT registered
    new TestEventA { Value = 2 }.Publish();

    // target never received anything: was not registered before first publish,
    // and pending start+stop nets to unregistered before second publish
    Assert.That(target.ReceiveCount, Is.EqualTo(0));
    Assert.That(controller.ReceiveCount, Is.EqualTo(2));
}

[Test]
public void StopOther_AfterPublishEnds_OnlySubscriberPublish_DoesNotReachTarget()
{
    var target = new CountingMutationTargetSubscriber();
    var stopper = new StopOtherDuringPublishSubscriber(target);

    stopper.StartListening();
    target.StartListening();

    // First publish: stopper defers StopListening(target); target still receives current publish
    new TestEventA { Value = 1 }.Publish();
    // Pending applied: target unregistered

    // Second publish targeted only at target — target is no longer registered, should not receive
    new TestEventA { Value = 2 }.OnlySubscriber(target).Publish();

    Assert.That(stopper.ReceiveCount, Is.EqualTo(1));
    Assert.That(target.ReceiveCount, Is.EqualTo(1));
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet test Tests/Tests.csproj -c Release --filter "BootstrapContractTests|PublishMutationTests"
```

Expected: all pass (should be around 16 total from these two fixtures).

- [ ] **Step 5: Commit**

```bash
git add Tests/BootstrapContractTests.cs Tests/TestTypes.cs Tests/PublishMutationTests.cs
git commit -m "test: add missing pre-init, queue-ordering, and post-dequeue OnlySubscriber tests"
```

---

## Task 4: Add Async Test Types (for Tasks 5 and 6)

**Files:**
- Modify: `Tests/TestTypes.cs`

Two new async subscriber types needed for the async migration tests.

`AsyncStopSelfDuringPublishSubscriber` handles `TestEventAsync` with an async handler that unregisters itself. Used to verify async deferred-mutation behavioral parity with sync.

`AsyncStartOtherAndCheckCountSubscriber` is the discriminating test type: it starts a `SyncOnlySubscriberForAsyncEvent` instance during its async handler and records the subscriber count at that moment. With the old snapshot model (`_publishDepth = 0` during async publish), `StartListening` takes immediate effect, so the count is 1. With the new deferred model (`_publishDepth > 0`), `StartListening` is queued, so the count is 0 during the publish.

- [ ] **Step 1: Append two async types to TestTypes.cs**

At the end of `Tests/TestTypes.cs`, append:

```csharp
public class AsyncStopSelfDuringPublishSubscriber
{
    public int ReceiveCount;

    [OnEvent]
    public async Task<bool> OnTestEventAsync(TestEventAsync e)
    {
        await Task.Yield();
        ReceiveCount++;
        this.StopListening();
        return true;
    }
}

public class AsyncStartOtherAndCheckCountSubscriber
{
    private readonly SyncOnlySubscriberForAsyncEvent _other;
    public int CountDuringPublish = -1;

    public AsyncStartOtherAndCheckCountSubscriber(SyncOnlySubscriberForAsyncEvent other)
    {
        _other = other;
    }

    [OnEvent]
    public async Task<bool> OnTestEventAsync(TestEventAsync e)
    {
        await Task.Yield();
        _other.StartListening();
        CountDuringPublish = SubscriberHelper.GetSubscriberCount<TestEventAsync, SyncOnlySubscriberForAsyncEvent>();
        return true;
    }
}
```

- [ ] **Step 2: Build to confirm no compile errors**

```bash
dotnet build Tests/Tests.csproj -c Release 2>&1 | grep -E "error|warning|succeeded|FAILED"
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add Tests/TestTypes.cs
git commit -m "test: add async mutation subscriber types for async migration TDD"
```

---

## Task 5: Write Failing Discriminating Test for Async Migration (TDD Red)

**Files:**
- Modify: `Tests/PublishMutationTests.cs`

This test will FAIL before the async generator migration (Task 6) and PASS after. It proves the observable difference between the old snapshot model (`_publishDepth = 0` during async publish) and the new deferred model (`_publishDepth > 0`).

Mechanism: `AsyncStartOtherAndCheckCountSubscriber` starts a `SyncOnlySubscriberForAsyncEvent` during its handler and reads the subscriber count. With the snapshot model, `StartListening` immediately registers (depth=0), so count=1 during publish. With deferred, it is queued (depth>0), so count=0 during publish.

- [ ] **Step 1: Add the discriminating test to PublishMutationTests.cs**

In `Tests/PublishMutationTests.cs`, add inside the class:

```csharp
[Test]
public async Task PublishAsync_StartListening_DuringPublish_IsDeferred_NotImmediatelyVisible()
{
    var newSub = new SyncOnlySubscriberForAsyncEvent();
    var checker = new AsyncStartOtherAndCheckCountSubscriber(newSub);
    checker.StartListening();

    await new TestEventAsync { Value = 1 }.PublishAsync();

    // With deferred semantics: StartListening(newSub) is queued during async publish,
    // so the count was 0 during the handler. After publish ends, pending is applied.
    Assert.That(checker.CountDuringPublish, Is.EqualTo(0),
        "StartListening called during PublishAsync must be deferred: count must be 0 during the publish");
    Assert.That(SubscriberHelper.GetSubscriberCount<TestEventAsync, SyncOnlySubscriberForAsyncEvent>(), Is.EqualTo(1),
        "After PublishAsync ends, pending StartListening must have been applied");

    newSub.StopListening();
    checker.StopListening();
}
```

- [ ] **Step 2: Run the test and verify it fails (RED)**

```bash
dotnet test Tests/Tests.csproj -c Release --filter "PublishAsync_StartListening_DuringPublish_IsDeferred_NotImmediatelyVisible"
```

Expected: `Failed: 1` — the assertion `checker.CountDuringPublish == 0` fails because the snapshot model immediately registers `newSub` (count becomes 1 during the handler).

If it passes, re-check that `_publishDepth` is not somehow non-zero before the test, and that the test types were created correctly.

- [ ] **Step 3: Commit the failing test**

```bash
git add Tests/PublishMutationTests.cs
git commit -m "test: add failing discriminating test for async publish deferred mutation"
```

---

## Task 6: Migrate Async Path to BeginPublish/EndPublish (Issue A — TDD Green)

**Files:**
- Modify: `src/GenEvent.SourceGenerator/GenEventSourceGenerator.cs`
- Modify: `src/GenEvent/src/GenEventRegistry.cs`

Replace the async publisher generation with BeginPublish/EndPublish + DirectSubscribers (same pattern as sync), and remove the now-dead snapshot pool code from the registry.

- [ ] **Step 1: Migrate async generator in GenEventSourceGenerator.cs**

In `src/GenEvent.SourceGenerator/GenEventSourceGenerator.cs`, replace the async snapshot generation block (lines 437–451):

Old code:
```csharp
var asyncSnapshotDeclarations = new StringBuilder();
var asyncSnapshotReturns = new StringBuilder();
var asyncInvocations = new StringBuilder();
var seenAsyncTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
foreach (var sub in subscriberList)
{
    if (!seenAsyncTypes.Add(sub.SubscriberType)) continue;
    var subscriberTypeName = GetFullyQualifiedTypeName(sub.SubscriberType);
    var snapshotVariableName = GetSubscriberSnapshotVariableName(sub.SubscriberType);
    asyncSnapshotDeclarations.AppendLine($"        var {snapshotVariableName} = GenEventRegistry<TGenEvent, {subscriberTypeName}>.TakeSubscribersSnapshot();");
    asyncSnapshotReturns.AppendLine($"            GenEventRegistry<TGenEvent, {subscriberTypeName}>.ReturnSubscribersSnapshot({snapshotVariableName});");
    asyncInvocations.AppendLine($"            completed = await @event.InvokeAsync<{subscriberTypeName}, TGenEvent>(config, {snapshotVariableName});");
    asyncInvocations.AppendLine("        if (!completed) return false;");
    asyncInvocations.AppendLine();
}
```

New code:
```csharp
var asyncSnapshotDeclarations = new StringBuilder();
var asyncSnapshotReturns = new StringBuilder();
var asyncInvocations = new StringBuilder();
var seenAsyncTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
foreach (var sub in subscriberList)
{
    if (!seenAsyncTypes.Add(sub.SubscriberType)) continue;
    var subscriberTypeName = GetFullyQualifiedTypeName(sub.SubscriberType);
    asyncSnapshotDeclarations.AppendLine($"        GenEventRegistry<TGenEvent, {subscriberTypeName}>.BeginPublish();");
    asyncSnapshotReturns.AppendLine($"            GenEventRegistry<TGenEvent, {subscriberTypeName}>.EndPublish();");
    asyncInvocations.AppendLine($"            completed = await @event.InvokeAsync<{subscriberTypeName}, TGenEvent>(config, GenEventRegistry<TGenEvent, {subscriberTypeName}>.DirectSubscribers);");
    asyncInvocations.AppendLine("        if (!completed) return false;");
    asyncInvocations.AppendLine();
}
```

- [ ] **Step 2: Remove dead snapshot pool code from GenEventRegistry.cs**

In `src/GenEvent/src/GenEventRegistry.cs`, remove:

1. The constant `private const int SnapshotPoolCapacity = 16;` (line 45)
2. The field `private static readonly List<List<TSubscriber>> SnapshotPool = new(SnapshotPoolCapacity);` (line 78)
3. The entire `TakeSubscribersSnapshot()` method (lines 96–113)
4. The entire `ReturnSubscribersSnapshot()` method (lines 115–121)

After removal, the class should have no snapshot-related members. The first `PropertyGroup` section (lines 45–94) should look like:

```csharp
public static class GenEventRegistry<TGenEvent, TSubscriber>
    where TGenEvent : struct, IGenEvent<TGenEvent>
{
    private static readonly List<TSubscriber> SubscriberList = new();
    private static readonly Dictionary<TSubscriber, int> SubscriberIndex = new(ReferenceEqualityComparer<TSubscriber>.Instance);

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

    public static GenEventDelegate<TGenEvent, TSubscriber> GenEvent { get; private set; }
    public static GenEventAsyncDelegate<TGenEvent, TSubscriber> GenEventAsync { get; private set; }

    public static IReadOnlyList<TSubscriber> Subscribers => SubscriberList;
    public static IReadOnlyList<TSubscriber> DirectSubscribers => SubscriberList;
    public static int SubscriberCount => SubscriberList.Count;

    public static bool ContainsSubscriber(TSubscriber subscriber)
    {
        return SubscriberIndex.ContainsKey(subscriber);
    }
    // ... rest of methods unchanged
```

- [ ] **Step 3: Run the discriminating test — it should now PASS (GREEN)**

```bash
dotnet test Tests/Tests.csproj -c Release --filter "PublishAsync_StartListening_DuringPublish_IsDeferred_NotImmediatelyVisible"
```

Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 4: Run full test suite**

```bash
dotnet test Tests/Tests.csproj -c Release
```

Expected: all tests pass (at least 155 total with the new tests added so far).

- [ ] **Step 5: Commit**

```bash
git add src/GenEvent.SourceGenerator/GenEventSourceGenerator.cs src/GenEvent/src/GenEventRegistry.cs
git commit -m "perf: migrate async publisher path to BeginPublish/EndPublish, remove dead snapshot pool"
```

---

## Task 7: Add Async Behavioral Tests and Allocation Regression (Issue C async + Issue A regression)

**Files:**
- Modify: `Tests/PublishMutationTests.cs`
- Modify: `Tests/AllocationRegressionTests.cs`

Add behavioral async mutation tests (documenting deferred semantics for async) and an allocation regression test.

- [ ] **Step 1: Add async deferred stop-self behavioral test to PublishMutationTests.cs**

In `Tests/PublishMutationTests.cs`, add inside the class:

```csharp
[Test]
public async Task PublishAsync_StopListening_DuringPublish_TakesEffectOnNextPublish()
{
    var selfStopping = new AsyncStopSelfDuringPublishSubscriber();
    selfStopping.StartListening();

    await new TestEventAsync { Value = 1 }.PublishAsync();
    await new TestEventAsync { Value = 2 }.PublishAsync();

    Assert.That(selfStopping.ReceiveCount, Is.EqualTo(1));
}
```

- [ ] **Step 2: Add async allocation regression test to AllocationRegressionTests.cs**

In `Tests/AllocationRegressionTests.cs`, add after the existing `StartListening_ReturnHandlePath_HasNoSteadyStateAllocations` test:

```csharp
[Test]
public async Task PublishAsync_DefaultPath_HasNoSteadyStateAllocations()
{
    var subscriber = new SyncOnlySubscriberForAsyncEvent();
    subscriber.StartListening();

    try
    {
        Warmup(() => new TestEventAsync { Value = 1 }.PublishAsync().GetAwaiter().GetResult());

        var allocatedBytes = MeasureAverageAllocation(
            () => new TestEventAsync { Value = 1 }.PublishAsync().GetAwaiter().GetResult());

        Assert.That(allocatedBytes, Is.EqualTo(0),
            "PublishAsync with sync-only subscriber should not allocate in steady state.");
    }
    finally
    {
        subscriber.StopListening();
    }
}
```

- [ ] **Step 3: Run the new tests**

```bash
dotnet test Tests/Tests.csproj -c Release --filter "PublishMutationTests|AllocationRegressionTests"
```

Expected: all pass.

- [ ] **Step 4: Run full test suite**

```bash
dotnet test Tests/Tests.csproj -c Release
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add Tests/PublishMutationTests.cs Tests/AllocationRegressionTests.cs
git commit -m "test: add async deferred-mutation behavioral tests and PublishAsync allocation regression"
```

---

## Task 8: Build Release, Sync Unity Artefacts, Final Verification

**Files:**
- Verify sync: `src/GenEvent.Unity/Assets/Plugins/GenEvent/Runtime/`
- Verify sync: `src/GenEvent.Unity/Assets/Plugins/GenEvent/Editor/SourceGenerator/GenEvent.SourceGenerator.dll`

- [ ] **Step 1: Build GenEvent release (triggers Unity Runtime source copy)**

```bash
dotnet build src/GenEvent/GenEvent.csproj -c Release 2>&1 | grep -E "error|warning|复制完成|nupkg|succeeded|FAILED"
```

Expected:
- `源码复制完成！`
- `Build succeeded.`

- [ ] **Step 2: Build SourceGenerator release (triggers Unity Editor DLL copy)**

```bash
dotnet build src/GenEvent.SourceGenerator/GenEvent.SourceGenerator.csproj -c Release 2>&1 | grep -E "error|warning|复制完成|succeeded|FAILED"
```

Expected:
- `Source Generator 文件复制完成！`
- `Build succeeded.`

- [ ] **Step 3: Inspect Unity diff**

```bash
git diff --stat -- src/GenEvent.Unity/Assets/Plugins/GenEvent/
```

Expected: Runtime `.cs` files show the `ContainsSubscriber` change and snapshot pool removal; SourceGenerator DLL binary changed.

- [ ] **Step 4: Commit Unity artefacts if changed**

```bash
git add src/GenEvent.Unity/Assets/Plugins/GenEvent/
git commit -m "chore: sync Unity artefacts — ContainsSubscriber O(1), remove snapshot pool"
```

If `git diff` shows no changes, skip this commit.

- [ ] **Step 5: Run full test suite one final time**

```bash
dotnet test Tests/Tests.csproj -c Release
```

Expected: all tests pass (should be around 157+ tests with all new additions).

- [ ] **Step 6: Verify no snapshot references remain in generated code paths**

```bash
grep -rn "TakeSubscribersSnapshot\|ReturnSubscribersSnapshot\|SnapshotPool\|SnapshotPoolCapacity" src/
```

Expected: no output (all snapshot-related code removed).

---

## Rollback Strategy

**If Task 6 (async migration) breaks any existing async tests:**
1. Revert `src/GenEvent.SourceGenerator/GenEventSourceGenerator.cs` changes only.
2. Keep Tasks 1–5 (ContainsSubscriber fix, guard tests, new behavioral tests).
3. Re-run tests to confirm rollback is clean.
4. The async migration requires more investigation.

**If ContainsSubscriber fix (Task 1) breaks any filter tests:**
1. Revert the O(1) change.
2. Check if `SubscriberIndex` and `SubscriberList` are ever out of sync (they should not be — both are updated together in `RegisterImmediate`/`UnRegisterImmediate`).

---

## Self-Review

**Spec coverage:**
- Issue A (async migration): Tasks 5, 6, 7 ✓
- Issue B (O(1) ContainsSubscriber): Task 1 ✓
- Issue C (missing tests):
  - `StopListeningForSpecificEvent_WithoutInit`: Task 3 ✓
  - Queue ordering (unregister→register, register→unregister): Task 3 ✓
  - Post-dequeue OnlySubscriber: Task 3 ✓
  - Async deferred mutation behavioral test: Task 7 ✓
  - Async allocation regression: Task 7 ✓
- Issue D (RegistryGuardTests safety): Task 2 ✓
- Unity artefacts sync: Task 8 ✓

**Placeholder scan:** No TBD/TODO. All code blocks are complete. All commands include expected output.

**Type consistency:**
- `AsyncStopSelfDuringPublishSubscriber` defined in Task 4, used in Task 7 ✓
- `AsyncStartOtherAndCheckCountSubscriber` defined in Task 4, used in Task 5 ✓
- `StopOtherThenStartOtherDuringPublishSubscriber` defined in Task 3 Step 2, used in Task 3 Step 3 ✓
- `StartOtherThenStopOtherDuringPublishSubscriber` defined in Task 3 Step 2, used in Task 3 Step 3 ✓
- `SyncOnlySubscriberForAsyncEvent` already exists in `TestTypes.cs` ✓
- `SubscriberHelper.GetSubscriberCount<TestEventAsync, SyncOnlySubscriberForAsyncEvent>()` API already exists ✓
