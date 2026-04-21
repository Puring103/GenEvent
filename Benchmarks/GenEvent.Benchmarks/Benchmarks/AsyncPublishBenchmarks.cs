using BenchmarkDotNet.Attributes;
using GenEvent;
using GenEvent.Benchmarks.Fixtures;

namespace GenEvent.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class AsyncPublishBenchmarks
{
    [Params(1, 10, 100)]
    public int SubscriberCount { get; set; }

    private AsyncBenchEvent _asyncEvent;
    private readonly List<AsyncSubscriber> _subscribers = new();

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkRuntimeState.ResetAndInit();
        _asyncEvent = new AsyncBenchEvent { Value = 1 };
        _subscribers.Clear();

        for (int i = 0; i < SubscriberCount; i++)
        {
            var subscriber = new AsyncSubscriber();
            subscriber.StartListening();
            _subscribers.Add(subscriber);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var subscriber in _subscribers)
        {
            subscriber.StopListening();
        }

        _subscribers.Clear();
        BenchmarkRuntimeState.Reset();
    }

    [Benchmark]
    public Task<bool> PublishAsync_Subscribers()
    {
        return _asyncEvent.PublishAsync();
    }

    [Benchmark]
    public Task<bool> PublishAsync_ConfiguredEventPipeline()
    {
        return _asyncEvent
            .Cancelable()
            .OnlySubscriber(_subscribers[0])
            .PublishAsync();
    }
}
