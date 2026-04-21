# GenEvent Stability Transition Release Design

## Summary

This spec defines the next pre-v1.0 release for GenEvent as a stability transition release rather than a feature release. The goal is to harden runtime contracts, reduce integration ambiguity, and improve failure diagnostics without expanding GenEvent into a broader event framework.

This release is intentionally not labeled `v1.0`. `v1.0` is reserved for a later milestone where runtime contracts, public API shape, diagnostics, documentation, and ecosystem readiness are collectively mature enough to be treated as stable.

## Release Positioning

### Version intent

The release should be published as either:

- `v0.9.14` if changes stay mostly within behavior/documentation and avoid meaningful public API expansion.
- `v0.10.0` if new public runtime APIs are added, such as `GenEventBootstrap.IsInitialized` and minimal diagnostic helpers.

The recommended target is `v0.10.0`, because explicit runtime contract APIs are more valuable than preserving a narrower patch-level surface.

### Primary outcome

After this release, consumers should be able to answer the following questions directly from the runtime contract and documentation:

- Has GenEvent been initialized?
- What happens if `Publish()` is called before initialization?
- What thread-safety guarantees exist?
- How can I quickly verify whether a publisher or subscriber registration exists?

## Goals

- Make initialization behavior explicit, idempotent, and diagnosable.
- Replace implementation-leaking exceptions with user-facing runtime errors.
- Define and document the threading contract for the current architecture.
- Add minimal observability for registration and publish troubleshooting.
- Lock the new semantics with tests before adding more advanced runtime features.

## Non-Goals

- Full runtime thread safety.
- Lock-based or concurrent-collection rewrite of registry infrastructure.
- Sticky/replay events.
- Weak-reference subscriptions.
- Batch publish APIs.
- Unity main-thread dispatch helpers.
- One-shot subscription APIs such as `ListenOnce`.
- Large public API redesign.

These items remain candidates for later releases and should not expand scope for this transition version.

## Current Problems

### Initialization is implicit

`Publish()` and `PublishAsync()` currently assume publisher registration already exists. If initialization has not happened, the code falls through to dictionary indexing and throws `KeyNotFoundException`. This reveals an internal storage detail rather than a meaningful library contract.

### Threading semantics are undocumented

The runtime currently uses mutable static state in `PublishConfig<TGenEvent>` and mutable list/dictionary registries in `GenEventRegistry<TGenEvent, TSubscriber>`. Nested publish is supported, but concurrent publish/register/unregister behavior is not defined or protected.

### Troubleshooting is too opaque

When an event is not delivered, consumers have no direct runtime API to check whether GenEvent has been initialized or whether relevant publisher/subscriber registrations exist.

## Proposed Approach

### Approach options considered

#### Option A: Full thread-safe rewrite now

Introduce locking or concurrent data structures throughout publish config, publisher lookup, and subscriber registries.

Pros:

- Stronger concurrency guarantees immediately.

Cons:

- Large implementation surface.
- Higher risk of performance regressions.
- Higher risk of semantic bugs in a library whose main value proposition is low-overhead dispatch.

#### Option B: Explicit single-thread contract plus diagnostics

Keep the current high-performance architecture, explicitly document it as not guaranteed thread-safe, and add lightweight runtime checks and diagnostics around initialization and misuse.

Pros:

- Preserves project identity.
- Small, focused change surface.
- Solves the most common integration problems first.

Cons:

- Does not satisfy users who need true concurrent dispatch.

#### Option C: Add fault-tolerant convenience APIs first

Prioritize `TryPublish()`-style APIs and other wrappers while leaving initialization and threading semantics mostly implicit.

Pros:

- Quick consumer-facing additions.

Cons:

- Risks papering over undefined runtime behavior.
- Leaves core contract ambiguity unresolved.

### Recommended approach

Adopt Option B for this release.

GenEvent should first become explicit and predictable before it becomes broader. The release should harden the current model instead of prematurely widening its promise surface.

## Design

### 1. Initialization contract

#### Required behavior

- `GenEventBootstrap.Init()` must be idempotent.
- Runtime code must expose whether initialization has completed.
- Publish operations must fail with a clear GenEvent-specific error if initialization or publisher registration is missing.

#### API direction

Add:

- `GenEventBootstrap.IsInitialized : bool`

Behavior:

- `Init()` may be called multiple times safely.
- The first successful initialization sets `IsInitialized = true`.
- Subsequent `Init()` calls are no-ops.

#### Publish failure behavior

When `Publish()` or `PublishAsync()` is called for an event type whose publisher has not been registered, the runtime should throw `InvalidOperationException` with a message that:

- names the event type,
- states that GenEvent is not initialized or the publisher is unavailable,
- tells the user to call `GenEventBootstrap.Init()` for the relevant assembly before publishing.

This is intentionally stricter and clearer than returning `false`. Publishing without initialization is treated as a configuration/programming error, not a business-level outcome.

### 2. Threading contract

#### Contract statement

This release will formally define GenEvent as not guaranteeing concurrent thread safety for:

- `Publish()`
- `PublishAsync()`
- `StartListening()`
- `StopListening()`

Supported behavior:

- Nested publish from within handlers remains supported.
- Re-entrant use on the same thread remains supported within the existing publish flow model.

Unsupported/undefined behavior for this release:

- Concurrent publish on the same event system state from multiple threads.
- Concurrent register/unregister while other threads publish.
- Concurrent mutation of fluent publish configuration state.

#### Documentation requirements

Both English and Chinese README files must gain a dedicated runtime contract section explaining:

- initialization expectations,
- idempotent bootstrap behavior,
- thread model,
- what nested publish means,
- what is not guaranteed.

### 3. Debug-time thread misuse detection

#### Intent

Provide early feedback for common misuse without redesigning the runtime around locking.

#### Proposed behavior

Add a runtime switch:

- `GenEventRuntimeSettings.EnableThreadAccessChecks : bool`

Behavior:

- The owning thread is captured on the first successful `GenEventBootstrap.Init()` call.
- When `EnableThreadAccessChecks` is `true`, calls to publish/subscribe/unsubscribe from any other thread throw `InvalidOperationException`.
- The default value is `false` so the release path keeps minimal overhead.

#### Design constraints

- The default release-path overhead should remain minimal.
- The check must remain optional for host environments that intentionally manage cross-thread access differently.
- The exception should explain that GenEvent does not guarantee concurrent thread safety in the current version.

This feature is misuse detection only. It is not a synchronization mechanism and does not imply thread safety when disabled.

### 4. Minimal observability APIs

#### Purpose

The goal is not to build a debugging subsystem. The goal is to let consumers quickly answer whether the runtime was initialized and whether a publisher/subscriber registration exists.

#### Proposed minimum surface

Required:

- `GenEventBootstrap.IsInitialized`
- `HasPublisher<TGenEvent>()`
- `GetSubscriberCount<TGenEvent, TSubscriber>()`

These APIs should be narrow and read-only. They are intended for diagnostics, assertions, integration checks, and tooling support.

### 5. Exception semantics

This release does not introduce a configurable exception handling strategy such as fail-fast vs aggregate exceptions. Existing subscriber-thrown exceptions continue to propagate.

The only semantic change in this area is that bootstrap/publisher-missing failures should become explicit `InvalidOperationException`s rather than dictionary lookup failures.

This keeps scope focused while still improving the most confusing runtime failure mode.

## Testing Strategy

Add or update tests to lock the intended behavior:

- `Init()` is idempotent.
- `IsInitialized` is false before init and true after init.
- `Publish()` before init throws `InvalidOperationException` with a helpful message.
- `PublishAsync()` before init throws `InvalidOperationException` with a helpful message.
- repeated `Init()` does not duplicate registrations or alter correct publish behavior.
- publishing with no subscribers still returns `true` after proper initialization.
- debug-time thread misuse checks fail on cross-thread access when enabled.
- nested publish behavior remains unchanged.

Tests should prioritize semantic guarantees over implementation details.

## Documentation Changes

Update:

- `README.md`
- `README_zh.md`

Documentation additions should include:

- a runtime contract section,
- initialization examples,
- explanation of idempotent bootstrap,
- explicit thread-safety disclaimer,
- common failure troubleshooting for missing initialization.

## Migration Impact

### Source compatibility

Expected to remain high. Existing user code that correctly initializes GenEvent should continue to work with minimal or no changes.

### Behavioral changes

- Calling `Init()` multiple times becomes explicitly supported.
- Uninitialized publish changes from a generic dictionary exception to a clear library contract exception.
- Consumers relying on undocumented cross-thread use may now receive clearer failures in debug-oriented configurations.

## Risks

- Adding `IsInitialized` to generated bootstrap code changes the generated public surface and must remain consistent across assemblies.
- Thread ownership checks may be awkward in some hosting environments if the trigger point is chosen poorly.
- Documentation must be precise so that "not thread-safe" is understood as an explicit contract, not an accidental omission.

## Deferred Work

The following items are intentionally deferred beyond this release:

- `TryPublish()` / `TryPublishAsync()`
- configurable exception policies
- full thread-safe mode
- sticky/replay events
- weak subscriptions
- batch publish
- Unity main-thread scheduling helpers
- one-shot or conditional subscription helpers

These should only be reconsidered after the runtime contract introduced here has proven stable.

## Acceptance Criteria

This design is complete when all of the following are true:

- The runtime exposes explicit initialization state.
- Calling `Init()` repeatedly is safe and documented.
- Uninitialized publish failures surface as clear `InvalidOperationException`s.
- The README files explicitly document the thread model and initialization rules.
- Minimal runtime diagnostics exist for publisher/subscriber visibility.
- Tests cover the new contract and pass without regressing current publish semantics.

## Release Recommendation

Ship this work as a stability transition release before declaring `v1.0`.

`v1.0` should remain reserved for a later milestone where GenEvent's runtime contract, API stability, documentation quality, diagnostics, and ecosystem readiness are collectively strong enough to justify a stable-major label.
