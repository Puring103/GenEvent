using System.Collections.Generic;
using System.Threading.Tasks;
using GenEvent;
using NUnit.Framework;

namespace Tests;

/// <summary>
/// Async support tests: PublishAsync, sync-only vs async-only handlers, sync path skips async-only,
/// cancelable/filters with async, config cleanup, Task vs Task&lt;bool&gt;.
/// </summary>
[TestFixture]
public class AsyncTests
{
    [SetUp]
    public void SetUp()
    {
        GenEventBootstrap.Init();
    }

    [Test]
    public async Task PublishAsync_AsyncHandlerOnly_ReceivesEvent()
    {
        var subscriber = new AsyncOnlySubscriber();
        subscriber.StartListening();

        var evt = new TestEventAsync { Value = 42 };
        var result = await evt.PublishAsync();

        Assert.That(result, Is.True);
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1));
        Assert.That(subscriber.LastValue, Is.EqualTo(42));
        subscriber.StopListening();
    }

    [Test]
    public async Task PublishAsync_SyncHandlerOnly_ReceivesEvent()
    {
        var subscriber = new SyncOnlySubscriberForAsyncEvent();
        subscriber.StartListening();

        var evt = new TestEventAsync { Value = 99 };
        var result = await evt.PublishAsync();

        Assert.That(result, Is.True);
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1));
        Assert.That(subscriber.LastValue, Is.EqualTo(99));
        subscriber.StopListening();
    }

    [Test]
    public void Publish_SyncHandlerOnly_ReceivesEvent()
    {
        var subscriber = new SyncOnlySubscriberForAsyncEvent();
        subscriber.StartListening();

        var evt = new TestEventAsync { Value = 11 };
        var result = evt.Publish();

        Assert.That(result, Is.True);
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1));
        Assert.That(subscriber.LastValue, Is.EqualTo(11));
        subscriber.StopListening();
    }

    [Test]
    public void Publish_AsyncHandlerOnly_DoesNotReceive()
    {
        var subscriber = new AsyncOnlySubscriber();
        subscriber.StartListening();

        new TestEventAsync { Value = 1 }.Publish();

        Assert.That(subscriber.ReceiveCount, Is.EqualTo(0), "Sync Publish must not call async-only handlers");
        subscriber.StopListening();
    }

    [Test]
    public void SameClass_SyncAndAsync_SyncPublish_CallsSyncOnly()
    {
        var subscriber = new SyncAndAsyncSubscriber();
        subscriber.StartListening();

        new TestEventAsync { Value = 5 }.Publish();

        Assert.That(subscriber.SyncCount, Is.EqualTo(1));
        Assert.That(subscriber.AsyncCount, Is.EqualTo(0), "Sync Publish must not call async handler");
        Assert.That(subscriber.LastValueSync, Is.EqualTo(5));
        subscriber.StopListening();
    }

    [Test]
    public async Task SameClass_SyncAndAsync_AsyncPublish_CallsAsyncHandler()
    {
        var subscriber = new SyncAndAsyncSubscriber();
        subscriber.StartListening();

        await new TestEventAsync { Value = 7 }.PublishAsync();

        Assert.That(subscriber.AsyncCount, Is.EqualTo(1));
        Assert.That(subscriber.LastValueAsync, Is.EqualTo(7));
        subscriber.StopListening();
    }

    [Test]
    public async Task PublishAsync_Cancelable_AsyncReturnsFalse_StopsPropagationAndReturnsFalse()
    {
        var sub1 = new AsyncCancelSubscriber { ShouldCancel = false };
        var sub2 = new AsyncCancelSubscriber { ShouldCancel = true };
        var sub3 = new AsyncCancelSubscriber { ShouldCancel = false };
        sub1.StartListening();
        sub2.StartListening();
        sub3.StartListening();

        var evt = new TestEventAsync { Value = 1 };
        var result = await evt.Cancelable().PublishAsync();

        Assert.That(result, Is.False);
        Assert.That(sub1.ReceiveCount, Is.EqualTo(1));
        Assert.That(sub2.ReceiveCount, Is.EqualTo(1));
        Assert.That(sub3.ReceiveCount, Is.EqualTo(0), "Should not be called after cancel");
        sub1.StopListening();
        sub2.StopListening();
        sub3.StopListening();
    }

    [Test]
    public async Task PublishAsync_Cancelable_AllReturnTrue_ReturnsTrue()
    {
        var sub1 = new AsyncCancelSubscriber { ShouldCancel = false };
        var sub2 = new AsyncCancelSubscriber { ShouldCancel = false };
        sub1.StartListening();
        sub2.StartListening();

        var evt = new TestEventAsync { Value = 1 };
        var result = await evt.Cancelable().PublishAsync();

        Assert.That(result, Is.True);
        Assert.That(sub1.ReceiveCount, Is.EqualTo(1));
        Assert.That(sub2.ReceiveCount, Is.EqualTo(1));
        sub1.StopListening();
        sub2.StopListening();
    }

    [Test]
    public async Task PublishAsync_ExcludeSubscriber_ExcludedDoesNotReceive()
    {
        var subSync = new SyncOnlySubscriberForAsyncEvent();
        var subAsync = new AsyncOnlySubscriber();
        subSync.StartListening();
        subAsync.StartListening();

        await new TestEventAsync { Value = 1 }.ExcludeSubscriber(subAsync).PublishAsync();

        Assert.That(subSync.ReceiveCount, Is.EqualTo(1));
        Assert.That(subAsync.ReceiveCount, Is.EqualTo(0));
        subSync.StopListening();
        subAsync.StopListening();
    }

    [Test]
    public async Task PublishAsync_OnlySubscriber_OnlyIncludedReceives()
    {
        var subSync = new SyncOnlySubscriberForAsyncEvent();
        var subAsync = new AsyncOnlySubscriber();
        subSync.StartListening();
        subAsync.StartListening();

        await new TestEventAsync { Value = 1 }.OnlySubscriber(subAsync).PublishAsync();

        Assert.That(subSync.ReceiveCount, Is.EqualTo(0));
        Assert.That(subAsync.ReceiveCount, Is.EqualTo(1));
        subSync.StopListening();
        subAsync.StopListening();
    }

    [Test]
    public async Task PublishAsync_ConfigClearedAfterPublish_SecondPublishUnaffected()
    {
        var subSync = new SyncOnlySubscriberForAsyncEvent();
        var subAsync = new AsyncOnlySubscriber();
        subSync.StartListening();
        subAsync.StartListening();

        await new TestEventAsync { Value = 1 }.ExcludeSubscriber(subAsync).PublishAsync();

        Assert.That(subSync.ReceiveCount, Is.EqualTo(1));
        Assert.That(subAsync.ReceiveCount, Is.EqualTo(0));

        await new TestEventAsync { Value = 2 }.PublishAsync();

        Assert.That(subSync.ReceiveCount, Is.EqualTo(2));
        Assert.That(subAsync.ReceiveCount, Is.EqualTo(1), "Second publish should not have filter from first");
        subSync.StopListening();
        subAsync.StopListening();
    }

    [Test]
    public async Task PublishAsync_TaskReturnNoBool_ContinuesPropagation()
    {
        var subNoBool = new AsyncTaskNoBoolSubscriber();
        var subWithBool = new AsyncOnlySubscriber();
        subNoBool.StartListening();
        subWithBool.StartListening();

        var result = await new TestEventAsync { Value = 1 }.PublishAsync();

        Assert.That(result, Is.True);
        Assert.That(subNoBool.ReceiveCount, Is.EqualTo(1));
        Assert.That(subWithBool.ReceiveCount, Is.EqualTo(1));
        subNoBool.StopListening();
        subWithBool.StopListening();
    }

    [Test]
    public async Task PublishAsync_NonCancelable_FalseReturn_DoesNotStopPropagation()
    {
        var cancelSub = new AsyncCancelSubscriber { ShouldCancel = true }; // returns false
        var otherSub = new AsyncOnlySubscriber();
        cancelSub.StartListening();
        otherSub.StartListening();

        // No Cancelable() here
        var result = await new TestEventAsync { Value = 1 }.PublishAsync();

        Assert.That(result, Is.True, "Without Cancelable, false return should not affect PublishAsync result");
        Assert.That(cancelSub.ReceiveCount, Is.EqualTo(1));
        Assert.That(otherSub.ReceiveCount, Is.EqualTo(1), "Other async subscriber should still run when not cancelable");

        cancelSub.StopListening();
        otherSub.StopListening();
    }

    [Test]
    public async Task MultipleAsyncSubscribers_AllReceiveInOrder()
    {
        var sub1 = new AsyncOnlySubscriber();
        var sub2 = new AsyncOnlySubscriber();
        var sub3 = new SyncOnlySubscriberForAsyncEvent();
        sub1.StartListening();
        sub2.StartListening();
        sub3.StartListening();

        await new TestEventAsync { Value = 33 }.PublishAsync();

        Assert.That(sub1.ReceiveCount, Is.EqualTo(1));
        Assert.That(sub2.ReceiveCount, Is.EqualTo(1));
        Assert.That(sub3.ReceiveCount, Is.EqualTo(1));
        Assert.That(sub1.LastValue, Is.EqualTo(33));
        Assert.That(sub2.LastValue, Is.EqualTo(33));
        Assert.That(sub3.LastValue, Is.EqualTo(33));
        sub1.StopListening();
        sub2.StopListening();
        sub3.StopListening();
    }

    [Test]
    public async Task PublishAsync_NoSubscribers_ReturnsTrue()
    {
        // No subscribers for a dedicated async-only test event
        var evt = new TestEventAsync { Value = 10 };
        var result = await evt.PublishAsync();
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task AsyncPriority_CallOrder_Is_Primary_High_Medium_Low_End()
    {
        var primary = new AsyncPrimaryPrioritySubscriber();
        var high = new AsyncHighPrioritySubscriber();
        var medium = new AsyncMediumPrioritySubscriber();
        var low = new AsyncLowPrioritySubscriber();
        var end = new AsyncEndPrioritySubscriber();

        primary.StartListening();
        high.StartListening();
        medium.StartListening();
        low.StartListening();
        end.StartListening();

        await new TestEventC { Value = 0 }.PublishAsync();

        Assert.That(primary.CallOrder, Is.EqualTo(0));
        Assert.That(high.CallOrder, Is.EqualTo(1));
        Assert.That(medium.CallOrder, Is.EqualTo(2));
        Assert.That(low.CallOrder, Is.EqualTo(3));
        Assert.That(end.CallOrder, Is.EqualTo(4));

        primary.StopListening();
        high.StopListening();
        medium.StopListening();
        low.StopListening();
        end.StopListening();
    }

    [Test]
    public async Task AsyncPriority_HighCancels_LowerPrioritiesNotExecuted()
    {
        var primary = new AsyncPrimaryPrioritySubscriber();
        var high = new AsyncHighPrioritySubscriber { CancelPropagation = true };
        var medium = new AsyncMediumPrioritySubscriber();
        var low = new AsyncLowPrioritySubscriber();
        var end = new AsyncEndPrioritySubscriber();

        primary.StartListening();
        high.StartListening();
        medium.StartListening();
        low.StartListening();
        end.StartListening();

        var evt = new TestEventC { Value = 0 }.Cancelable();
        var result = await evt.PublishAsync();

        Assert.That(result, Is.False);
        Assert.That(primary.CallOrder, Is.EqualTo(0));
        Assert.That(high.CallOrder, Is.EqualTo(1));
        Assert.That(medium.CallOrder, Is.EqualTo(-1));
        Assert.That(low.CallOrder, Is.EqualTo(-1));
        Assert.That(end.CallOrder, Is.EqualTo(-1));

        primary.StopListening();
        high.StopListening();
        medium.StopListening();
        low.StopListening();
        end.StopListening();
    }

    [Test]
    public async Task PublishAsync_OnlyTypeAndExcludeType_WorkAsExpected()
    {
        var sub1 = new FilterSubscriberType1();
        var sub2 = new FilterSubscriberType2();
        sub1.StartListening();
        sub2.StartListening();

        await new TestEventA { Value = 1 }
            .OnlyType<TestEventA, FilterSubscriberType1>()
            .PublishAsync();

        Assert.That(sub1.ReceiveCount, Is.EqualTo(1));
        Assert.That(sub2.ReceiveCount, Is.EqualTo(0));

        sub1.ReceiveCount = 0;
        sub2.ReceiveCount = 0;

        await new TestEventA { Value = 2 }
            .ExcludeType<TestEventA, FilterSubscriberType1>()
            .PublishAsync();

        Assert.That(sub1.ReceiveCount, Is.EqualTo(0));
        Assert.That(sub2.ReceiveCount, Is.EqualTo(1));

        sub1.StopListening();
        sub2.StopListening();
    }

    [Test]
    public async Task PublishAsync_OnlySubscribersAndExcludeSubscribers_WorkAsExpected()
    {
        var subA = new SubscriberA();
        var subB = new SubscriberB();
        subA.StartListening();
        subB.StartListening();

        var include = new HashSet<object> { subB };
        await new TestEventA { Value = 1 }.OnlySubscribers(include).PublishAsync();

        Assert.That(subA.ReceiveCount, Is.EqualTo(0));
        Assert.That(subB.ReceiveCount, Is.EqualTo(1));

        subA.ReceiveCount = 0;
        subB.ReceiveCount = 0;

        var exclude = new HashSet<object> { subB };
        await new TestEventA { Value = 2 }.ExcludeSubscribers(exclude).PublishAsync();

        Assert.That(subA.ReceiveCount, Is.EqualTo(1));
        Assert.That(subB.ReceiveCount, Is.EqualTo(0));

        subA.StopListening();
        subB.StopListening();
    }

    [Test]
    public async Task PublishAsync_MultipleFilters_Combined_AllConditionsApply()
    {
        var sub1 = new FilterSubscriberType1();
        var sub2 = new FilterSubscriberType2();
        sub1.StartListening();
        sub2.StartListening();

        await new TestEventA { Value = 1 }
            .OnlyType<TestEventA, FilterSubscriberType1>()
            .ExcludeSubscriber(sub1)
            .PublishAsync();

        Assert.That(sub1.ReceiveCount, Is.EqualTo(0));
        Assert.That(sub2.ReceiveCount, Is.EqualTo(0));

        sub1.ReceiveCount = 0;
        sub2.ReceiveCount = 0;

        await new TestEventA { Value = 2 }
            .OnlyType<TestEventA, FilterSubscriberType1>()
            .ExcludeSubscriber(sub2)
            .PublishAsync();

        Assert.That(sub1.ReceiveCount, Is.EqualTo(1));
        Assert.That(sub2.ReceiveCount, Is.EqualTo(0));

        sub1.StopListening();
        sub2.StopListening();
    }

    [Test]
    public async Task AsyncNestedPublish_SameEvent_ConfigIndependent()
    {
        var repub = new AsyncRepublishSameEventSubscriber();
        var other = new AsyncOnlySubscriber();
        repub.StartListening();
        other.StartListening();

        await new TestEventAsync { Value = 1 }.ExcludeSubscriber(other).PublishAsync();

        // repub: outer + inner, other: only inner
        Assert.That(repub.ReceiveCount, Is.EqualTo(2));
        Assert.That(other.ReceiveCount, Is.EqualTo(1));

        repub.StopListening();
        other.StopListening();
    }

    [Test]
    public async Task AsyncNestedPublish_DifferentEvent_TypesIndependent()
    {
        var sub = new AsyncRepublishDifferentEventSubscriber();
        sub.StartListening();

        await new TestEventAsync { Value = 5 }.PublishAsync();

        Assert.That(sub.AsyncCount, Is.EqualTo(1));
        Assert.That(sub.EventACount, Is.EqualTo(1));

        sub.StopListening();
    }

    [Test]
    public async Task StartStopListening_AsyncSubscriber_WorksAsSync()
    {
        var subscriber = new AsyncOnlySubscriber();
        subscriber.StartListening();
        await new TestEventAsync { Value = 1 }.PublishAsync();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1));

        subscriber.StopListening();
        await new TestEventAsync { Value = 2 }.PublishAsync();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1), "Should not receive after StopListening");
    }

    [Test]
    public async Task PublishAsync_WithFilter_CustomPredicate_FiltersAsExpected()
    {
        var subSync = new SyncOnlySubscriberForAsyncEvent();
        var subAsync = new AsyncOnlySubscriber();
        subSync.StartListening();
        subAsync.StartListening();

        await new TestEventAsync { Value = 1 }.WithFilter(obj => obj == subAsync).PublishAsync();

        Assert.That(subSync.ReceiveCount, Is.EqualTo(1));
        Assert.That(subAsync.ReceiveCount, Is.EqualTo(0));
        subSync.StopListening();
        subAsync.StopListening();
    }
}
