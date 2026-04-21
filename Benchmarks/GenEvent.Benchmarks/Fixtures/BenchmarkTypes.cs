using GenEvent;
using GenEvent.Interface;
using System.Threading.Tasks;

namespace GenEvent.Benchmarks.Fixtures;

public struct PublishBenchEvent : IGenEvent<PublishBenchEvent>
{
    public int Value;
}

public struct CancelBenchEvent : IGenEvent<CancelBenchEvent>
{
    public int Value;
}

public struct AsyncBenchEvent : IGenEvent<AsyncBenchEvent>
{
    public int Value;
}

public struct OuterNestedBenchEvent : IGenEvent<OuterNestedBenchEvent>
{
    public int Value;
}

public struct InnerNestedBenchEvent : IGenEvent<InnerNestedBenchEvent>
{
    public int Value;
}

public class PublishSubscriber
{
    public int Count;

    [OnEvent]
    public bool OnPublish(PublishBenchEvent e)
    {
        Count += e.Value;
        return true;
    }
}

public class CancelingSubscriber
{
    public int Count;

    [OnEvent(SubscriberPriority.High)]
    public bool OnCancel(CancelBenchEvent e)
    {
        Count += e.Value;
        return false;
    }
}

public class TrailingCancelableSubscriber
{
    public int Count;

    [OnEvent(SubscriberPriority.Low)]
    public bool OnCancel(CancelBenchEvent e)
    {
        Count += e.Value;
        return true;
    }
}

public class AsyncSubscriber
{
    public int Count;

    [OnEvent]
    public Task<bool> OnAsync(AsyncBenchEvent e)
    {
        Count += e.Value;
        return Task.FromResult(true);
    }
}

public class NestedOuterSubscriber
{
    [OnEvent]
    public bool OnOuter(OuterNestedBenchEvent e)
    {
        new InnerNestedBenchEvent { Value = e.Value }.Publish();
        return true;
    }
}

public class NestedInnerSubscriber
{
    public int Count;

    [OnEvent]
    public bool OnInner(InnerNestedBenchEvent e)
    {
        Count += e.Value;
        return true;
    }
}

public class LifecycleSubscriber
{
    public int Count;

    [OnEvent]
    public bool OnPublish(PublishBenchEvent e)
    {
        Count += e.Value;
        return true;
    }
}
