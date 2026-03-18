using System.Threading.Tasks;
using GenEvent;

namespace Tests;

/// <summary>
/// Tests that StartListening / StopListening called on a base-class reference
/// (where TSubscriber is inferred as the base type at compile time) correctly
/// registers / unregisters the actual runtime (child) type's event handlers.
///
/// This requires two coordinated fixes:
///   1. SubscriberHelper uses subscriber.GetType() instead of typeof(TSubscriber)
///      to look up the runtime registry.
///   2. The generated SubscriberRegistry casts self to the concrete type and
///      directly calls GenEventRegistry&lt;Event, ConcreteType&gt;.Register/UnRegister,
///      so the subscription ends up in the same static container that the
///      generated Publisher iterates.
/// </summary>
[TestFixture]
public class InheritanceTests
{
    [SetUp]
    public void SetUp()
    {
        GenEventBootstrap.Init();
    }

    // ------------------------------------------------------------------
    // Basic receive
    // ------------------------------------------------------------------

    [Test]
    public void ChildClass_StartListeningViaBaseMethod_ReceivesEvents()
    {
        var child = new InheritChild();
        child.Subscribe(); // calls this.StartListening() where compile-time type is InheritBase

        new InheritTestEvent { Value = 7 }.Publish();

        Assert.That(child.ReceiveCount, Is.EqualTo(1));
        Assert.That(child.LastValue, Is.EqualTo(7));
        child.Unsubscribe();
    }

    [Test]
    public void ChildClass_StopListeningViaBaseMethod_StopsReceivingEvents()
    {
        var child = new InheritChild();
        child.Subscribe();
        new InheritTestEvent { Value = 1 }.Publish();
        Assert.That(child.ReceiveCount, Is.EqualTo(1));

        child.Unsubscribe(); // calls this.StopListening() via InheritBase
        new InheritTestEvent { Value = 2 }.Publish();

        Assert.That(child.ReceiveCount, Is.EqualTo(1), "Should not receive after StopListening via base method");
    }

    // ------------------------------------------------------------------
    // SubscriptionHandle
    // ------------------------------------------------------------------

    [Test]
    public void ChildClass_SubscriptionHandle_DisposeCancelsSubscription()
    {
        var child = new InheritChild();
        var handle = child.Subscribe(); // handle returned from base-class call

        new InheritTestEvent { Value = 10 }.Publish();
        Assert.That(child.ReceiveCount, Is.EqualTo(1));

        handle.Dispose();
        new InheritTestEvent { Value = 20 }.Publish();

        Assert.That(child.ReceiveCount, Is.EqualTo(1), "Dispose of handle must unsubscribe correctly");
    }

    [Test]
    public void ChildClass_SubscriptionHandle_DisposeIsIdempotent()
    {
        var child = new InheritChild();
        var handle = child.Subscribe();
        handle.Dispose();
        Assert.DoesNotThrow(() => handle.Dispose(), "Second Dispose must be a no-op");
    }

    // ------------------------------------------------------------------
    // Multiple event types
    // ------------------------------------------------------------------

    [Test]
    public void ChildClassMultiEvent_StartListeningViaBase_AllEventTypesRegistered()
    {
        var child = new InheritChildMultiEvent();
        child.Subscribe(); // registers for both InheritTestEvent and InheritTestEventB

        new InheritTestEvent { Value = 1 }.Publish();
        new InheritTestEventB { Value = 2 }.Publish();

        Assert.That(child.EventACount, Is.EqualTo(1));
        Assert.That(child.EventBCount, Is.EqualTo(1));
        child.Unsubscribe();
    }

    [Test]
    public void ChildClassMultiEvent_StopListeningViaBase_AllEventTypesUnregistered()
    {
        var child = new InheritChildMultiEvent();
        child.Subscribe();
        child.Unsubscribe();

        new InheritTestEvent { Value = 1 }.Publish();
        new InheritTestEventB { Value = 2 }.Publish();

        Assert.That(child.EventACount, Is.EqualTo(0));
        Assert.That(child.EventBCount, Is.EqualTo(0));
    }

    // ------------------------------------------------------------------
    // Three-level hierarchy
    // ------------------------------------------------------------------

    /// <summary>
    /// GrandChild has its own [OnEvent] for InheritTestEventB.
    /// Calling Subscribe() from InheritBase must register the GrandChild instance
    /// in GrandChild's registry (InheritTestEventB), not InheritChild's registry.
    /// </summary>
    [Test]
    public void GrandChildClass_StartListeningViaBase_UsesGrandChildRegistry()
    {
        var grand = new InheritGrandChild();
        grand.Subscribe(); // compile-time type = InheritBase; runtime type = InheritGrandChild

        new InheritTestEventB { Value = 5 }.Publish();

        Assert.That(grand.GrandChildReceiveCount, Is.EqualTo(1));
        grand.Unsubscribe();
    }

    /// <summary>
    /// GrandChild inherits InheritTestEvent from InheritChild (via virtual dispatch).
    /// Calling Subscribe() once must register it for BOTH InheritTestEvent and InheritTestEventB.
    /// </summary>
    [Test]
    public void GrandChildClass_StartListeningViaBase_AlsoReceivesInheritedBaseEvent()
    {
        var grand = new InheritGrandChild();
        grand.Subscribe();

        new InheritTestEvent { Value = 99 }.Publish();

        Assert.That(grand.ReceiveCount, Is.EqualTo(1), "InheritGrandChild should also receive InheritTestEvent inherited from InheritChild.");
        grand.Unsubscribe();
    }

    [Test]
    public void GrandChildClass_Unsubscribe_StopsBothEventTypes()
    {
        var grand = new InheritGrandChild();
        grand.Subscribe();

        new InheritTestEvent { Value = 1 }.Publish();
        new InheritTestEventB { Value = 2 }.Publish();
        Assert.That(grand.ReceiveCount, Is.EqualTo(1));
        Assert.That(grand.GrandChildReceiveCount, Is.EqualTo(1));

        grand.Unsubscribe();
        new InheritTestEvent { Value = 3 }.Publish();
        new InheritTestEventB { Value = 4 }.Publish();
        Assert.That(grand.ReceiveCount, Is.EqualTo(1), "Should not receive InheritTestEvent after Unsubscribe.");
        Assert.That(grand.GrandChildReceiveCount, Is.EqualTo(1), "Should not receive InheritTestEventB after Unsubscribe.");
    }

    // ------------------------------------------------------------------
    // Multiple instances
    // ------------------------------------------------------------------

    [Test]
    public void MultipleChildInstances_EachSubscribeAndUnsubscribeIndependently()
    {
        var c1 = new InheritChild();
        var c2 = new InheritChild();
        c1.Subscribe();
        c2.Subscribe();

        new InheritTestEvent { Value = 1 }.Publish();
        Assert.That(c1.ReceiveCount, Is.EqualTo(1));
        Assert.That(c2.ReceiveCount, Is.EqualTo(1));

        c1.Unsubscribe();
        new InheritTestEvent { Value = 2 }.Publish();
        Assert.That(c1.ReceiveCount, Is.EqualTo(1), "c1 stopped");
        Assert.That(c2.ReceiveCount, Is.EqualTo(2), "c2 still active");

        c2.Unsubscribe();
    }

    // ------------------------------------------------------------------
    // Async handler via base Subscribe
    // ------------------------------------------------------------------

    [Test]
    public async Task ChildClassAsync_StartListeningViaBase_ReceivesAsyncEvents()
    {
        var child = new InheritChildAsync();
        child.Subscribe();

        await new InheritTestEvent { Value = 42 }.PublishAsync();

        Assert.That(child.ReceiveCount, Is.EqualTo(1));
        Assert.That(child.LastValue, Is.EqualTo(42));
        child.Unsubscribe();
    }

    [Test]
    public async Task ChildClassAsync_StopListeningViaBase_StopsAsyncEvents()
    {
        var child = new InheritChildAsync();
        child.Subscribe();
        await new InheritTestEvent { Value = 1 }.PublishAsync();
        Assert.That(child.ReceiveCount, Is.EqualTo(1));

        child.Unsubscribe();
        await new InheritTestEvent { Value = 2 }.PublishAsync();
        Assert.That(child.ReceiveCount, Is.EqualTo(1), "Async handler must not fire after StopListening via base");
    }

    // ------------------------------------------------------------------
    // Idempotency via base
    // ------------------------------------------------------------------

    [Test]
    public void ChildClass_StartListeningTwiceViaBase_IsIdempotent()
    {
        var child = new InheritChild();
        child.Subscribe();
        child.Subscribe(); // duplicate registration must be ignored

        new InheritTestEvent { Value = 1 }.Publish();

        Assert.That(child.ReceiveCount, Is.EqualTo(1), "Duplicate StartListening must not cause double dispatch");
        child.Unsubscribe();
    }
}
