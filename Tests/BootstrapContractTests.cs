using GenEvent;
using GenEvent.Interface;

namespace Tests;

[TestFixture]
public class BootstrapContractTests
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
    public void IsInitialized_IsFalseBeforeInit_AndTrueAfterInit()
    {
        Assert.That(GenEventBootstrap.IsInitialized, Is.False);

        GenEventBootstrap.Init();

        Assert.That(GenEventBootstrap.IsInitialized, Is.True);
    }

    [Test]
    public void Init_CalledMultipleTimes_RemainsInitialized_AndDoesNotDuplicatePublishers()
    {
        GenEventBootstrap.Init();
        var countBefore = BaseEventPublisher.Publishers.Count;

        GenEventBootstrap.Init();

        Assert.Multiple(() =>
        {
            Assert.That(GenEventBootstrap.IsInitialized, Is.True);
            Assert.That(BaseEventPublisher.Publishers.Count, Is.EqualTo(countBefore));
        });
    }

    [Test]
    public void Publish_WithoutInit_ThrowsInvalidOperationException_WithInitGuidance()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new TestEventA { Value = 1 }.Publish());

        Assert.That(exception!.Message, Does.Contain(nameof(TestEventA)));
        Assert.That(exception.Message, Does.Contain("GenEventBootstrap.Init()"));
    }

    [Test]
    public async Task PublishAsync_WithoutInit_ThrowsInvalidOperationException_WithInitGuidance()
    {
        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await new TestEventA { Value = 1 }.PublishAsync());

        Assert.That(exception!.Message, Does.Contain(nameof(TestEventA)));
        Assert.That(exception.Message, Does.Contain("GenEventBootstrap.Init()"));
    }

    [Test]
    public void StartListening_WithoutInit_ThrowsInvalidOperationException()
    {
        var subscriber = new SubscriberA();

        var exception = Assert.Throws<InvalidOperationException>(() => subscriber.StartListening());

        Assert.That(exception!.Message, Does.Contain("GenEventBootstrap.Init()"));
    }

    [Test]
    public void StartListeningForSpecificEvent_WithoutInit_ThrowsInvalidOperationException()
    {
        var subscriber = new MultiEventSubscriber();

        var exception = Assert.Throws<InvalidOperationException>(() => subscriber.StartListening<MultiEventSubscriber, TestEventA>());

        Assert.That(exception!.Message, Does.Contain(nameof(TestEventA)));
        Assert.That(exception.Message, Does.Contain("GenEventBootstrap.Init()"));
    }

    [Test]
    public void StopListening_WithoutInit_ThrowsInvalidOperationException()
    {
        var subscriber = new SubscriberA();

        var exception = Assert.Throws<InvalidOperationException>(() => subscriber.StopListening());

        Assert.That(exception!.Message, Does.Contain("GenEventBootstrap.Init()"));
    }

    [Test]
    public void HasPublisher_ReturnsFalseBeforeInit_AndTrueAfterInit()
    {
        Assert.That(PublisherHelper.HasPublisher<TestEventA>(), Is.False);

        GenEventBootstrap.Init();

        Assert.That(PublisherHelper.HasPublisher<TestEventA>(), Is.True);
    }

    [Test]
    public void GetSubscriberCount_ReturnsZeroBeforeListening_AndTracksRegistrations()
    {
        GenEventBootstrap.Init();
        var subscriber = new SubscriberA();

        Assert.That(SubscriberHelper.GetSubscriberCount<TestEventA, SubscriberA>(), Is.Zero);

        using var handle = subscriber.StartListening<SubscriberA, TestEventA>();

        Assert.That(SubscriberHelper.GetSubscriberCount<TestEventA, SubscriberA>(), Is.EqualTo(1));
    }
}
