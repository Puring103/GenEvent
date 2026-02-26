using GenEvent;

namespace Tests;

/// <summary>
/// Filter tests: WithFilter, Exclude/Include Subscriber(s), OnlyType, ExcludeType, and combinations.
/// </summary>
[TestFixture]
public class FilterTests
{
    [SetUp]
    public void SetUp()
    {
        GenEventBootstrap.Init();
    }

    [Test]
    public void WithFilter_CustomPredicate_FiltersAsExpected()
    {
        var subA = new SubscriberA();
        var subB = new SubscriberB();
        subA.StartListening();
        subB.StartListening();

        var evt = new TestEventA { Value = 1 }.WithFilter(obj => obj == subB);
        evt.Publish();

        Assert.That(subA.ReceiveCount, Is.EqualTo(1));
        Assert.That(subB.ReceiveCount, Is.EqualTo(0), "subB filtered by predicate");
        subA.StopListening();
        subB.StopListening();
    }

    [Test]
    public void ExcludeSubscriber_SingleObject_ExcludedSubscriberDoesNotReceive()
    {
        var subA = new SubscriberA();
        var subB = new SubscriberB();
        subA.StartListening();
        subB.StartListening();

        var evt = new TestEventA { Value = 1 }.ExcludeSubscriber(subB);
        evt.Publish();

        Assert.That(subA.ReceiveCount, Is.EqualTo(1));
        Assert.That(subB.ReceiveCount, Is.EqualTo(0));
        subA.StopListening();
        subB.StopListening();
    }

    [Test]
    public void IncludeSubscriber_SingleObject_OnlyIncludedSubscriberReceives()
    {
        var subA = new SubscriberA();
        var subB = new SubscriberB();
        subA.StartListening();
        subB.StartListening();

        var evt = new TestEventA { Value = 1 }.OnlySubscriber(subB);
        evt.Publish();

        Assert.That(subA.ReceiveCount, Is.EqualTo(0));
        Assert.That(subB.ReceiveCount, Is.EqualTo(1));
        subA.StopListening();
        subB.StopListening();
    }

    [Test]
    public void ExcludeSubscribers_HashSet_ExcludedSubscribersDoNotReceive()
    {
        var subA = new SubscriberA();
        var subB = new SubscriberB();
        subA.StartListening();
        subB.StartListening();

        var exclude = new HashSet<object> { subB };
        var evt = new TestEventA { Value = 1 }.ExcludeSubscribers(exclude);
        evt.Publish();

        Assert.That(subA.ReceiveCount, Is.EqualTo(1));
        Assert.That(subB.ReceiveCount, Is.EqualTo(0));
        subA.StopListening();
        subB.StopListening();
    }

    [Test]
    public void IncludeSubscribers_HashSet_OnlyIncludedSubscribersReceive()
    {
        var subA = new SubscriberA();
        var subB = new SubscriberB();
        subA.StartListening();
        subB.StartListening();

        var include = new HashSet<object> { subB };
        var evt = new TestEventA { Value = 1 }.OnlySubscribers(include);
        evt.Publish();

        Assert.That(subA.ReceiveCount, Is.EqualTo(0));
        Assert.That(subB.ReceiveCount, Is.EqualTo(1));
        subA.StopListening();
        subB.StopListening();
    }

    [Test]
    public void OnlyType_SpecificSubscriberType_OnlyThatTypeReceives()
    {
        var sub1 = new FilterSubscriberType1();
        var sub2 = new FilterSubscriberType2();
        sub1.StartListening();
        sub2.StartListening();

        var evt = new TestEventA { Value = 1 }.OnlyType<TestEventA, FilterSubscriberType1>();
        evt.Publish();

        Assert.That(sub1.ReceiveCount, Is.EqualTo(1));
        Assert.That(sub2.ReceiveCount, Is.EqualTo(0));
        sub1.StopListening();
        sub2.StopListening();
    }

    [Test]
    public void ExcludeType_SpecificSubscriberType_ThatTypeDoesNotReceive()
    {
        var sub1 = new FilterSubscriberType1();
        var sub2 = new FilterSubscriberType2();
        sub1.StartListening();
        sub2.StartListening();

        var evt = new TestEventA { Value = 1 }.ExcludeType<TestEventA, FilterSubscriberType1>();
        evt.Publish();

        Assert.That(sub1.ReceiveCount, Is.EqualTo(0));
        Assert.That(sub2.ReceiveCount, Is.EqualTo(1));
        sub1.StopListening();
        sub2.StopListening();
    }

    [Test]
    public void MultipleFilters_Combined_AllConditionsApply()
    {
        var sub1 = new FilterSubscriberType1();
        var sub2 = new FilterSubscriberType2();
        sub1.StartListening();
        sub2.StartListening();

        // OnlyType FilterSubscriberType1 + ExcludeSubscriber sub1 => no one receives
        var evt = new TestEventA { Value = 1 }
            .OnlyType<TestEventA, FilterSubscriberType1>()
            .ExcludeSubscriber(sub1);
        evt.Publish();

        Assert.That(sub1.ReceiveCount, Is.EqualTo(0));
        Assert.That(sub2.ReceiveCount, Is.EqualTo(0));

        sub1.ReceiveCount = 0;
        sub2.ReceiveCount = 0;

        // OnlyType FilterSubscriberType1 (sub1 receives) + ExcludeSubscriber sub2 (sub2 excluded - redundant)
        var evt2 = new TestEventA { Value = 2 }
            .OnlyType<TestEventA, FilterSubscriberType1>()
            .ExcludeSubscriber(sub2);
        evt2.Publish();

        Assert.That(sub1.ReceiveCount, Is.EqualTo(1));
        Assert.That(sub2.ReceiveCount, Is.EqualTo(0));
        sub1.StopListening();
        sub2.StopListening();
    }
}
