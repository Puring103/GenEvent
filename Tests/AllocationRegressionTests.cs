using GenEvent;

namespace Tests;

[TestFixture]
public class AllocationRegressionTests
{
    private const int Iterations = 10_000;

    [SetUp]
    public void SetUp()
    {
        GenEventBootstrap.Init();
    }

    [Test]
    public void Publish_DefaultPath_HasNoSteadyStateAllocations()
    {
        var evt = new TestEventA { Value = 1 };

        Warmup(() => evt.Publish());

        var allocatedBytes = MeasureAverageAllocation(() => evt.Publish());

        Assert.That(allocatedBytes, Is.EqualTo(0), "Default Publish should not allocate in steady state.");
    }

    [Test]
    public void Publish_BuiltInFilterPath_HasNoSteadyStateAllocations()
    {
        var subA = new SubscriberA();
        var subB = new SubscriberB();
        subA.StartListening();
        subB.StartListening();

        try
        {
            Warmup(() => new TestEventA { Value = 1 }.OnlySubscriber(subA).Publish());

            var allocatedBytes = MeasureAverageAllocation(() => new TestEventA { Value = 1 }.OnlySubscriber(subA).Publish());

            Assert.That(allocatedBytes, Is.EqualTo(0), "Built-in fluent filter path should not allocate in steady state.");
        }
        finally
        {
            subA.StopListening();
            subB.StopListening();
        }
    }

    [Test]
    public void Publish_FourBuiltInFilters_HasNoSteadyStateAllocations()
    {
        var subA = new SubscriberA();
        var subB = new SubscriberB();
        var include = new HashSet<object> { subA };
        var exclude = new HashSet<object>();
        subA.StartListening();
        subB.StartListening();

        try
        {
            Warmup(() => new TestEventA { Value = 1 }
                .OnlyType<TestEventA, SubscriberA>()
                .OnlySubscribers(include)
                .ExcludeSubscriber(subB)
                .ExcludeSubscribers(exclude)
                .Publish());

            var allocatedBytes = MeasureAverageAllocation(() => new TestEventA { Value = 1 }
                .OnlyType<TestEventA, SubscriberA>()
                .OnlySubscribers(include)
                .ExcludeSubscriber(subB)
                .ExcludeSubscribers(exclude)
                .Publish());

            Assert.That(allocatedBytes, Is.EqualTo(0), "Built-in filter chains within inline rule capacity should not allocate in steady state.");
        }
        finally
        {
            subA.StopListening();
            subB.StopListening();
        }
    }

    [Test]
    public void StartListening_ReturnHandlePath_HasNoSteadyStateAllocations()
    {
        var subscriber = new SubscriberA();

        Warmup(() =>
        {
            using var handle = subscriber.StartListening();
        });

        var allocatedBytes = MeasureAverageAllocation(() =>
        {
            using var handle = subscriber.StartListening();
        });

        Assert.That(allocatedBytes, Is.EqualTo(0), "StartListening should not allocate in steady state when returning a handle.");
    }

    private static void Warmup(Action action)
    {
        for (int i = 0; i < 8; i++)
        {
            action();
        }
    }

    private static long MeasureAverageAllocation(Action action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < Iterations; i++)
        {
            action();
        }

        var after = GC.GetAllocatedBytesForCurrentThread();
        return (after - before) / Iterations;
    }
}
