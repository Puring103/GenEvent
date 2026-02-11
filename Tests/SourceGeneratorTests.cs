using GenEvent;
using GenEvent.Interface;

namespace GenEvent.Tests;

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
    public void GeneratedSubscriberRegistry_StartListening_ReceivesEvents()
    {
        var subscriber = new SubscriberA();
        subscriber.StartListening();
        new TestEventA { Value = 123 }.Publish();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1));
        Assert.That(subscriber.LastValue, Is.EqualTo(123));
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
    }
}
