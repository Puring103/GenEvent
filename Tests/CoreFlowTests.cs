using GenEvent;

namespace Tests;

/// <summary>
/// Core flow tests: basic publish, lifecycle, multi-subscriber, PublishConfig clear.
/// </summary>
[TestFixture]
public class CoreFlowTests
{
    [SetUp]
    public void SetUp()
    {
        GenEventBootstrap.Init();
    }

    [Test]
    public void BasicEventPublish_AndHandle_SingleSubscriber_ReceivesEvent()
    {
        var subscriber = new SubscriberA();
        subscriber.StartListening();

        var evt = new TestEventA { Value = 42 };
        evt.Publish();

        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1));
        Assert.That(subscriber.LastValue, Is.EqualTo(42));
        subscriber.StopListening();
    }

    [Test]
    public void StartListening_WithoutStop_ReceivesEvents()
    {
        var subscriber = new SubscriberA();
        subscriber.StartListening();

        new TestEventA { Value = 1 }.Publish();
        new TestEventA { Value = 2 }.Publish();

        Assert.That(subscriber.ReceiveCount, Is.EqualTo(2));
        subscriber.StopListening();
    }

    [Test]
    public void StopListening_AfterStart_StopsReceivingEvents()
    {
        var subscriber = new SubscriberA();
        subscriber.StartListening();
        new TestEventA { Value = 1 }.Publish();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1));

        subscriber.StopListening();
        new TestEventA { Value = 2 }.Publish();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1), "Should not receive after StopListening");
    }

    [Test]
    public void MultipleStartStop_SameSubscriber_CanReceiveAgainAfterRestart()
    {
        var subscriber = new SubscriberA();
        subscriber.StartListening();
        new TestEventA { Value = 1 }.Publish();
        subscriber.StopListening();
        new TestEventA { Value = 2 }.Publish();
        subscriber.StartListening();
        new TestEventA { Value = 3 }.Publish();

        Assert.That(subscriber.ReceiveCount, Is.EqualTo(2)); // First and third
        Assert.That(subscriber.LastValue, Is.EqualTo(3));
        subscriber.StopListening();
    }

    [Test]
    public void StartListeningPerEventType_OnlyAffectsThatEvent()
    {
        var subscriber = new MultiEventSubscriber();
        subscriber.StartListening<MultiEventSubscriber, TestEventA>();
        subscriber.StartListening<MultiEventSubscriber, TestEventB>();

        new TestEventA { }.Publish();
        new TestEventB { Data = "b" }.Publish();

        Assert.That(subscriber.EventACount, Is.EqualTo(1));
        Assert.That(subscriber.EventBCount, Is.EqualTo(1));

        subscriber.StopListening<MultiEventSubscriber, TestEventA>();
        new TestEventA { }.Publish();
        new TestEventB { Data = "b2" }.Publish();

        Assert.That(subscriber.EventACount, Is.EqualTo(1), "Should not receive TestEventA after StopListening for it");
        Assert.That(subscriber.EventBCount, Is.EqualTo(2));
        subscriber.StopListening();
    }

    [Test]
    public void MultipleSubscribers_SameEvent_AllReceive()
    {
        var subA = new SubscriberA();
        var subB = new SubscriberB();
        subA.StartListening();
        subB.StartListening();

        new TestEventA { Value = 10 }.Publish();

        Assert.That(subA.ReceiveCount, Is.EqualTo(1));
        Assert.That(subB.ReceiveCount, Is.EqualTo(1));
        subA.StopListening();
        subB.StopListening();
    }

    [Test]
    public void VoidReturnSubscriber_ReceivesEvent_AndPublishReturnsTrue()
    {
        var subscriber = new VoidReturnSubscriber();
        subscriber.StartListening();

        var evt = new TestEventA { Value = 99 };
        var result = evt.Publish();

        Assert.That(result, Is.True);
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1));
        Assert.That(subscriber.LastValue, Is.EqualTo(99));
        subscriber.StopListening();
    }

    [Test]
    public void VoidAndBoolSubscribers_Mixed_VoidTreatedAsContinue_BoolCancelStopsPropagation()
    {
        var voidSub = new VoidReturnSubscriber();
        var cancelSub = new CancelSubscriber { ShouldCancel = true };
        var lowSub = new LowPrioritySubscriberForA();
        voidSub.StartListening();
        cancelSub.StartListening();
        lowSub.StartListening();

        var result = new TestEventA { Value = 1 }.Cancelable().Publish();

        Assert.That(result, Is.False);
        Assert.That(voidSub.ReceiveCount, Is.EqualTo(1), "Void handler runs first (High)");
        Assert.That(cancelSub.ReceiveCount, Is.EqualTo(1), "Bool handler runs and returns false");
        Assert.That(lowSub.ReceiveCount, Is.EqualTo(0), "Low-priority subscriber should not run after cancel");
        voidSub.StopListening();
        cancelSub.StopListening();
        lowSub.StopListening();
    }

    [Test]
    public void DifferentEventTypes_DoNotInterfere()
    {
        var subA = new SubscriberA();
        var subC = new SubscriberC();
        subA.StartListening();
        subC.StartListening();

        new TestEventA { Value = 1 }.Publish();
        new TestEventB { Data = "x" }.Publish();

        Assert.That(subA.ReceiveCount, Is.EqualTo(1));
        Assert.That(subC.ReceiveCount, Is.EqualTo(1));
        subA.StopListening();
        subC.StopListening();
    }

    [Test]
    public void SameSubscriberSubscribesMultipleEvents_EachTriggersCorrectly()
    {
        var subscriber = new MultiEventSubscriber();
        subscriber.StartListening();

        new TestEventA { }.Publish();
        new TestEventB { Data = "d" }.Publish();
        new TestEventA { }.Publish();

        Assert.That(subscriber.EventACount, Is.EqualTo(2));
        Assert.That(subscriber.EventBCount, Is.EqualTo(1));
        subscriber.StopListening();
    }

    [Test]
    public void PublishConfig_ClearedAfterPublish_SecondPublishUnaffectedByFirst()
    {
        var subA = new SubscriberA();
        var subB = new SubscriberB();
        subA.StartListening();
        subB.StartListening();

        // First publish with filter excluding subB
        var evt1 = new TestEventA { Value = 1 }.ExcludeSubscriber(subB);
        evt1.Publish();

        Assert.That(subA.ReceiveCount, Is.EqualTo(1));
        Assert.That(subB.ReceiveCount, Is.EqualTo(0), "subB excluded by filter");

        // Second publish without filter - both should receive
        new TestEventA { Value = 2 }.Publish();

        Assert.That(subA.ReceiveCount, Is.EqualTo(2));
        Assert.That(subB.ReceiveCount, Is.EqualTo(1), "Second publish should not have filter from first");
        subA.StopListening();
        subB.StopListening();
    }

    /// <summary>
    /// Outer Publish excludes subB; inner Publish (from republish subscriber) has no filter.
    /// Verifies outer config is not overwritten: subB must receive only the inner event.
    /// </summary>
    [Test]
    public void NestedPublish_OuterConfigNotOverwritten_InnerUsesOwnConfig()
    {
        var subRepub = new RepublishSameEventSubscriber();
        var subB = new SubscriberB();
        subRepub.StartListening();
        subB.StartListening();

        new TestEventA { Value = 1 }.ExcludeSubscriber(subB).Publish();

        Assert.That(subRepub.ReceiveCount, Is.EqualTo(2), "Republish sub gets outer + inner");
        Assert.That(subB.ReceiveCount, Is.EqualTo(1), "subB must get only inner; outer config excluded subB and was not overwritten by inner");
        subRepub.StopListening();
        subB.StopListening();
    }

    /// <summary>
    /// Three-level nested Publish: L1 only NestedOuterSub, L2 only NestedMidSub, L3 no filter.
    /// Verifies each level's config is preserved (not overwritten by inner levels).
    /// </summary>
    [Test]
    public void NestedPublish_ThreeLevels_EachLevelConfigPreserved()
    {
        var outerSub = new NestedOuterSub();
        var midSub = new NestedMidSub();
        var innerSub = new NestedInnerSub();
        outerSub.StartListening();
        midSub.StartListening();
        innerSub.StartListening();

        new TestEventA { Value = 1 }.OnlyType<TestEventA, NestedOuterSub>().Publish();

        Assert.That(outerSub.ReceiveCount, Is.EqualTo(2), "Outer sub: L1 + L3");
        Assert.That(midSub.ReceiveCount, Is.EqualTo(2), "Mid sub: L2 + L3");
        Assert.That(innerSub.ReceiveCount, Is.EqualTo(1), "Inner sub: L3 only");
        outerSub.StopListening();
        midSub.StopListening();
        innerSub.StopListening();
    }
}
