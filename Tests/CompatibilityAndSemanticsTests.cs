using GenEvent;
using GenEvent.Interface;

namespace Tests;

[TestFixture]
public class CompatibilityAndSemanticsTests
{
    [SetUp]
    public void SetUp()
    {
        GenEventBootstrap.Init();
    }

    [Test]
    public void GenEventFilters_LegacyPublicHelpers_StillReturnCompatiblePredicates()
    {
        var target = new object();
        var other = new object();
        var set = new HashSet<object> { target };

        Assert.That(GenEventFilters.ExcludeSubscriber(target)(target), Is.True);
        Assert.That(GenEventFilters.ExcludeSubscriber(target)(other), Is.False);

        Assert.That(GenEventFilters.OnlySubscriber(target)(target), Is.False);
        Assert.That(GenEventFilters.OnlySubscriber(target)(other), Is.True);

        Assert.That(GenEventFilters.ExcludeSubscribers(set)(target), Is.True);
        Assert.That(GenEventFilters.ExcludeSubscribers(set)(other), Is.False);

        Assert.That(GenEventFilters.OnlySubscribers(set)(target), Is.False);
        Assert.That(GenEventFilters.OnlySubscribers(set)(other), Is.True);

        Assert.That(GenEventFilters.OnlyType<SubscriberA>()(new SubscriberA()), Is.False);
        Assert.That(GenEventFilters.OnlyType<SubscriberA>()(new SubscriberB()), Is.True);

        Assert.That(GenEventFilters.ExcludeType<SubscriberA>()(new SubscriberA()), Is.True);
        Assert.That(GenEventFilters.ExcludeType<SubscriberA>()(new SubscriberB()), Is.False);
    }

    [Test]
    public void ConfiguredEvent_ReusedVariable_RetainsConfigurationAcrossPublishes()
    {
        var subA = new SubscriberA();
        var subB = new SubscriberB();
        subA.StartListening();
        subB.StartListening();

        try
        {
            var configured = new TestEventA { Value = 1 }.OnlySubscriber(subA);

            configured.Publish();
            configured.Publish();

            Assert.That(subA.ReceiveCount, Is.EqualTo(2));
            Assert.That(subB.ReceiveCount, Is.EqualTo(0));
        }
        finally
        {
            subA.StopListening();
            subB.StopListening();
        }
    }
}

internal sealed class LegacyCompatibleRegistry : BaseSubscriberRegistry
{
    public override void StartListening<TSubscriber>(TSubscriber self)
    {
    }

    public override void StopListening<TSubscriber>(TSubscriber self)
    {
    }
}
