using GenEvent;
using GenEvent.Interface;

namespace Tests;

/// <summary>
/// Source generator / bootstrap: publishers registered, subscriber registries work, priority order.
/// </summary>
[TestFixture]
public class SourceGeneratorTests
{
    [SetUp]
    public void SetUp()
    {
        GenEventBootstrap.Init();
    }

    [Test]
    public void GeneratedPublishers_RegisteredInBaseEventPublisher()
    {
        Assert.That(BaseEventPublisher.Publishers.ContainsKey(typeof(TestEventA)), Is.True);
        Assert.That(BaseEventPublisher.Publishers.ContainsKey(typeof(TestEventB)), Is.True);
        Assert.That(BaseEventPublisher.Publishers.ContainsKey(typeof(TestEventC)), Is.True);
        Assert.That(BaseEventPublisher.Publishers.ContainsKey(typeof(StructEvent)), Is.True);
    }

    [Test]
    public void GenEventBootstrap_Init_IsIdempotent()
    {
        // First init done by SetUp, call Init again and ensure publishers remain valid
        var countBefore = BaseEventPublisher.Publishers.Count;
        GenEventBootstrap.Init();
        var countAfter = BaseEventPublisher.Publishers.Count;

        Assert.That(countAfter, Is.EqualTo(countBefore));

        var subscriber = new SubscriberA();
        subscriber.StartListening();
        new TestEventA { Value = 10 }.Publish();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1));
        subscriber.StopListening();
    }

    [Test]
    public void GeneratedSubscriberRegistry_StartListening_ReceivesEvents()
    {
        var subscriber = new SubscriberA();
        subscriber.StartListening();
        new TestEventA { Value = 123 }.Publish();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1));
        Assert.That(subscriber.LastValue, Is.EqualTo(123));
        subscriber.StopListening();
    }

    [Test]
    public void Priority_GeneratedCode_CallOrderMatchesAttribute()
    {
        var primary = new PrimaryPrioritySubscriber();
        var high = new HighPrioritySubscriber();
        var medium = new MediumPrioritySubscriber();
        var low = new LowPrioritySubscriber();
        var end = new EndPrioritySubscriber();
        primary.StartListening();
        high.StartListening();
        medium.StartListening();
        low.StartListening();
        end.StartListening();

        new TestEventC { Value = 0 }.Publish();

        Assert.That(primary.CallOrder, Is.LessThan(high.CallOrder));
        Assert.That(high.CallOrder, Is.LessThan(medium.CallOrder));
        Assert.That(medium.CallOrder, Is.LessThan(low.CallOrder));
        Assert.That(low.CallOrder, Is.LessThan(end.CallOrder));
        primary.StopListening();
        high.StopListening();
        medium.StopListening();
        low.StopListening();
        end.StopListening();
    }
}
