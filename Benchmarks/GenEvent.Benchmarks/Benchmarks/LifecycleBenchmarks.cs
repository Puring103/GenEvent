using BenchmarkDotNet.Attributes;
using GenEvent;
using GenEvent.Benchmarks.Fixtures;

namespace GenEvent.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class LifecycleBenchmarks
{
    [Params(1, 10, 100)]
    public int SubscriberCount { get; set; }

    private LifecycleSubscriber[] _subscribers = Array.Empty<LifecycleSubscriber>();

    [IterationSetup(Target = nameof(StartListening_Subscribers))]
    public void SetupStartListening()
    {
        BenchmarkRuntimeState.ResetAndInit();
        _subscribers = CreateSubscribers(SubscriberCount);
    }

    [IterationCleanup(Target = nameof(StartListening_Subscribers))]
    public void CleanupStartListening()
    {
        foreach (var subscriber in _subscribers)
        {
            subscriber.StopListening();
        }

        BenchmarkRuntimeState.Reset();
    }

    [IterationSetup(Target = nameof(StopListening_Subscribers))]
    public void SetupStopListening()
    {
        BenchmarkRuntimeState.ResetAndInit();
        _subscribers = CreateSubscribers(SubscriberCount);

        foreach (var subscriber in _subscribers)
        {
            subscriber.StartListening();
        }
    }

    [IterationCleanup(Target = nameof(StopListening_Subscribers))]
    public void CleanupStopListening()
    {
        BenchmarkRuntimeState.Reset();
    }

    [Benchmark]
    public void StartListening_Subscribers()
    {
        for (int i = 0; i < _subscribers.Length; i++)
        {
            _subscribers[i].StartListening();
        }
    }

    [Benchmark]
    public void StopListening_Subscribers()
    {
        for (int i = 0; i < _subscribers.Length; i++)
        {
            _subscribers[i].StopListening();
        }
    }

    private static LifecycleSubscriber[] CreateSubscribers(int count)
    {
        var subscribers = new LifecycleSubscriber[count];
        for (int i = 0; i < count; i++)
        {
            subscribers[i] = new LifecycleSubscriber();
        }

        return subscribers;
    }
}
