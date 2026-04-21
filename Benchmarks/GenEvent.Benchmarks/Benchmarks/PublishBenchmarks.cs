using BenchmarkDotNet.Attributes;
using GenEvent;
using GenEvent.Benchmarks.Fixtures;

namespace GenEvent.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class PublishBenchmarks
{
    private PublishBenchEvent _publishEvent;

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkRuntimeState.ResetAndInit();
        _publishEvent = new PublishBenchEvent { Value = 1 };
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        BenchmarkRuntimeState.Reset();
    }

    [Benchmark]
    public bool Publish_NoSubscribers()
    {
        return _publishEvent.Publish();
    }
}

[MemoryDiagnoser]
public class PublishSubscriberBenchmarks
{
    [Params(1, 10, 100)]
    public int SubscriberCount { get; set; }

    private PublishBenchEvent _publishEvent;
    private readonly List<PublishSubscriber> _publishSubscribers = new();
    private readonly List<object> _configuredTargets = new();

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkRuntimeState.ResetAndInit();
        _publishEvent = new PublishBenchEvent { Value = 1 };
        _publishSubscribers.Clear();
        _configuredTargets.Clear();

        for (int i = 0; i < SubscriberCount; i++)
        {
            var subscriber = new PublishSubscriber();
            subscriber.StartListening();
            _publishSubscribers.Add(subscriber);
            _configuredTargets.Add(subscriber);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var subscriber in _publishSubscribers)
        {
            subscriber.StopListening();
        }

        _publishSubscribers.Clear();
        _configuredTargets.Clear();
        BenchmarkRuntimeState.Reset();
    }

    [Benchmark]
    public bool Publish_Subscribers()
    {
        return _publishEvent.Publish();
    }

    [Benchmark]
    public bool Publish_BuiltInFilter()
    {
        return _publishEvent.ExcludeSubscriber(_configuredTargets[0]).Publish();
    }

    [Benchmark]
    public bool Publish_ConfiguredEventPipeline()
    {
        return _publishEvent
            .Cancelable()
            .OnlySubscriber(_configuredTargets[0])
            .Publish();
    }

    [Benchmark]
    public bool Publish_CustomPredicateFilter()
    {
        return _publishEvent.WithFilter(subscriber => ReferenceEquals(subscriber, _configuredTargets[0])).Publish();
    }
}

[MemoryDiagnoser]
public class PublishCancelableBenchmarks
{
    private CancelBenchEvent _cancelEvent;
    private CancelingSubscriber? _cancelingSubscriber;
    private TrailingCancelableSubscriber? _trailingSubscriber;

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkRuntimeState.ResetAndInit();
        _cancelEvent = new CancelBenchEvent { Value = 1 };
        _cancelingSubscriber = new CancelingSubscriber();
        _trailingSubscriber = new TrailingCancelableSubscriber();
        _cancelingSubscriber.StartListening();
        _trailingSubscriber.StartListening();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cancelingSubscriber?.StopListening();
        _trailingSubscriber?.StopListening();
        BenchmarkRuntimeState.Reset();
    }

    [Benchmark]
    public bool Publish_Cancelable_StopsEarly()
    {
        return _cancelEvent.Cancelable().Publish();
    }
}

[MemoryDiagnoser]
public class PublishNestedBenchmarks
{
    private OuterNestedBenchEvent _nestedEvent;
    private NestedOuterSubscriber? _nestedOuterSubscriber;
    private NestedInnerSubscriber? _nestedInnerSubscriber;

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkRuntimeState.ResetAndInit();
        _nestedEvent = new OuterNestedBenchEvent { Value = 1 };
        _nestedOuterSubscriber = new NestedOuterSubscriber();
        _nestedInnerSubscriber = new NestedInnerSubscriber();
        _nestedOuterSubscriber.StartListening();
        _nestedInnerSubscriber.StartListening();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _nestedOuterSubscriber?.StopListening();
        _nestedInnerSubscriber?.StopListening();
        BenchmarkRuntimeState.Reset();
    }

    [Benchmark]
    public bool Publish_Nested()
    {
        return _nestedEvent.Publish();
    }
}
