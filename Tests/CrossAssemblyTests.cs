using CrossAssembly.Events;
using BillingDuplicateNameEvent = CrossAssembly.Events.Billing.DuplicateNameEvent;
using OrdersDuplicateNameEvent = CrossAssembly.Events.Orders.DuplicateNameEvent;
using GenEvent;
using GenEvent.Interface;

namespace Tests;

[TestFixture]
public class CrossAssemblyTests
{
    [SetUp]
    public void SetUp()
    {
        BaseEventPublisher.Publishers.Clear();
        BaseSubscriberRegistry.Subscribers.Clear();
        GenEventBootstrap.Init();
    }

    [Test]
    public void Publish_ReferencedAssemblyEvent_IsRegisteredAndDelivered()
    {
        var subscriber = new CrossAssemblySubscriber();
        subscriber.StartListening();

        new CrossAssemblyEvent { Value = 7 }.Publish();

        Assert.Multiple(() =>
        {
            Assert.That(BaseEventPublisher.Publishers.ContainsKey(typeof(CrossAssemblyEvent)), Is.True);
            Assert.That(subscriber.ReceiveCount, Is.EqualTo(1));
            Assert.That(subscriber.LastValue, Is.EqualTo(7));
        });

        subscriber.StopListening();
    }

    [Test]
    public void Publish_ReferencedAssemblyEvent_NoSubscribers_ReturnsTrue()
    {
        var result = new CrossAssemblyEvent { Value = 1 }.Publish();

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task PublishAsync_ReferencedAssemblyEvent_NoSubscribers_ReturnsTrue()
    {
        var result = await new CrossAssemblyEvent { Value = 1 }.PublishAsync();

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task PublishAsync_ReferencedAssemblyEvent_IsDeliveredToSyncSubscriber()
    {
        var subscriber = new CrossAssemblySubscriber();
        subscriber.StartListening();

        var result = await new CrossAssemblyEvent { Value = 11 }.PublishAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(subscriber.ReceiveCount, Is.EqualTo(1));
            Assert.That(subscriber.LastValue, Is.EqualTo(11));
        });

        subscriber.StopListening();
    }

    [Test]
    public void Publish_ReferencedAssemblyEvent_Cancelable_StopsPropagation()
    {
        var canceling = new CancelingCrossAssemblySubscriber { ShouldContinue = false };
        var trailing = new CrossAssemblySubscriber();
        canceling.StartListening();
        trailing.StartListening();

        var result = new CrossAssemblyEvent { Value = 3 }.Cancelable().Publish();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(canceling.ReceiveCount, Is.EqualTo(1));
            Assert.That(trailing.ReceiveCount, Is.EqualTo(0));
        });

        canceling.StopListening();
        trailing.StopListening();
    }

    [Test]
    public void Publish_ReferencedAssemblyEvent_OnlyType_FiltersToMatchingSubscriber()
    {
        var allowed = new CrossAssemblySubscriber();
        var filtered = new OtherCrossAssemblySubscriber();
        allowed.StartListening();
        filtered.StartListening();

        new CrossAssemblyEvent { Value = 5 }.OnlyType<CrossAssemblyEvent, CrossAssemblySubscriber>().Publish();

        Assert.Multiple(() =>
        {
            Assert.That(allowed.ReceiveCount, Is.EqualTo(1));
            Assert.That(filtered.ReceiveCount, Is.EqualTo(0));
        });

        allowed.StopListening();
        filtered.StopListening();
    }

    [Test]
    public void Publish_ReferencedAssemblyEvent_ExcludeType_FiltersOutMatchingSubscriber()
    {
        var excluded = new CrossAssemblySubscriber();
        var remaining = new OtherCrossAssemblySubscriber();
        excluded.StartListening();
        remaining.StartListening();

        new CrossAssemblyEvent { Value = 5 }.ExcludeType<CrossAssemblyEvent, CrossAssemblySubscriber>().Publish();

        Assert.Multiple(() =>
        {
            Assert.That(excluded.ReceiveCount, Is.EqualTo(0));
            Assert.That(remaining.ReceiveCount, Is.EqualTo(1));
        });

        excluded.StopListening();
        remaining.StopListening();
    }

    [Test]
    public void StartListeningAndStopListening_ForSpecificCrossAssemblyEvent_Work()
    {
        var subscriber = new MultiCrossAssemblySubscriber();
        subscriber.StartListening<MultiCrossAssemblySubscriber, CrossAssemblyEvent>();

        new CrossAssemblyEvent { Value = 9 }.Publish();

        Assert.That(subscriber.CrossAssemblyCount, Is.EqualTo(1));

        subscriber.StopListening<MultiCrossAssemblySubscriber, CrossAssemblyEvent>();
        new CrossAssemblyEvent { Value = 10 }.Publish();

        Assert.Multiple(() =>
        {
            Assert.That(subscriber.CrossAssemblyCount, Is.EqualTo(1));
            Assert.That(subscriber.LocalCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void Publish_ReferencedAssemblyUnusedEvent_IsNotRegistered()
    {
        Assert.That(BaseEventPublisher.Publishers.ContainsKey(typeof(UnusedExternalEvent)), Is.False);
    }

    [Test]
    public void Publish_ReferencedAssemblyDuplicateNamedEvents_AreRegisteredAndDeliveredIndependently()
    {
        var ordersSubscriber = new OrdersDuplicateNameSubscriber();
        var billingSubscriber = new BillingDuplicateNameSubscriber();
        ordersSubscriber.StartListening();
        billingSubscriber.StartListening();

        new OrdersDuplicateNameEvent { Value = 21 }.Publish();
        new BillingDuplicateNameEvent { Value = 34 }.Publish();

        Assert.Multiple(() =>
        {
            Assert.That(BaseEventPublisher.Publishers.ContainsKey(typeof(OrdersDuplicateNameEvent)), Is.True);
            Assert.That(BaseEventPublisher.Publishers.ContainsKey(typeof(BillingDuplicateNameEvent)), Is.True);
            Assert.That(ordersSubscriber.ReceiveCount, Is.EqualTo(1));
            Assert.That(ordersSubscriber.LastValue, Is.EqualTo(21));
            Assert.That(billingSubscriber.ReceiveCount, Is.EqualTo(1));
            Assert.That(billingSubscriber.LastValue, Is.EqualTo(34));
        });

        ordersSubscriber.StopListening();
        billingSubscriber.StopListening();
    }
}

public class CrossAssemblySubscriber
{
    public int ReceiveCount { get; private set; }
    public int LastValue { get; private set; }

    [OnEvent]
    public bool OnCrossAssemblyEvent(CrossAssemblyEvent evt)
    {
        ReceiveCount++;
        LastValue = evt.Value;
        return true;
    }
}

public class OtherCrossAssemblySubscriber
{
    public int ReceiveCount { get; private set; }

    [OnEvent]
    public bool OnCrossAssemblyEvent(CrossAssemblyEvent evt)
    {
        ReceiveCount++;
        return true;
    }
}

public class CancelingCrossAssemblySubscriber
{
    public int ReceiveCount { get; private set; }
    public bool ShouldContinue { get; set; } = true;

    [OnEvent(SubscriberPriority.Primary)]
    public bool OnCrossAssemblyEvent(CrossAssemblyEvent evt)
    {
        ReceiveCount++;
        return ShouldContinue;
    }
}

public class MultiCrossAssemblySubscriber
{
    public int CrossAssemblyCount { get; private set; }
    public int LocalCount { get; private set; }

    [OnEvent]
    public bool OnCrossAssemblyEvent(CrossAssemblyEvent evt)
    {
        CrossAssemblyCount++;
        return true;
    }

    [OnEvent]
    public bool OnTestEventA(TestEventA evt)
    {
        LocalCount++;
        return true;
    }
}

public class OrdersDuplicateNameSubscriber
{
    public int ReceiveCount { get; private set; }
    public int LastValue { get; private set; }

    [OnEvent]
    public bool OnOrdersDuplicateNameEvent(OrdersDuplicateNameEvent evt)
    {
        ReceiveCount++;
        LastValue = evt.Value;
        return true;
    }
}

public class BillingDuplicateNameSubscriber
{
    public int ReceiveCount { get; private set; }
    public int LastValue { get; private set; }

    [OnEvent]
    public bool OnBillingDuplicateNameEvent(BillingDuplicateNameEvent evt)
    {
        ReceiveCount++;
        LastValue = evt.Value;
        return true;
    }
}
