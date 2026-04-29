using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using GenEvent.Benchmarks.Fixtures;

namespace GenEvent.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class DispatchBaselineBenchmarks
{
    [Params(1, 10)]
    public int SubscriberCount { get; set; }

    private PublishBenchEvent _publishEvent;
    private DirectDispatchTarget[] _directTargets = Array.Empty<DirectDispatchTarget>();
    private DispatchTargetBase[] _virtualTargets = Array.Empty<DispatchTargetBase>();
    private Func<PublishBenchEvent, bool>[] _delegateTargets = Array.Empty<Func<PublishBenchEvent, bool>>();
    private Func<PublishBenchEvent, bool>? _multicastDelegate;

    [GlobalSetup]
    public void Setup()
    {
        _publishEvent = new PublishBenchEvent { Value = 1 };
        _directTargets = new DirectDispatchTarget[SubscriberCount];
        _virtualTargets = new DispatchTargetBase[SubscriberCount];
        _delegateTargets = new Func<PublishBenchEvent, bool>[SubscriberCount];
        _multicastDelegate = null;

        for (int i = 0; i < SubscriberCount; i++)
        {
            var directTarget = new DirectDispatchTarget();
            var virtualTarget = new VirtualDispatchTarget();
            var delegateTarget = new DirectDispatchTarget();

            _directTargets[i] = directTarget;
            _virtualTargets[i] = virtualTarget;
            _delegateTargets[i] = delegateTarget.OnPublish;
            _multicastDelegate += delegateTarget.OnPublish;
        }
    }

    [Benchmark]
    public bool DirectMethodCall_Loop()
    {
        var result = true;

        for (int i = 0; i < _directTargets.Length; i++)
        {
            result &= _directTargets[i].OnPublish(_publishEvent);
        }

        return result;
    }

    [Benchmark]
    public bool VirtualMethodCall_Loop()
    {
        var result = true;

        for (int i = 0; i < _virtualTargets.Length; i++)
        {
            result &= _virtualTargets[i].OnPublish(_publishEvent);
        }

        return result;
    }

    [Benchmark]
    public bool DelegateInvoke_Loop()
    {
        var result = true;

        for (int i = 0; i < _delegateTargets.Length; i++)
        {
            result &= _delegateTargets[i](_publishEvent);
        }

        return result;
    }

    [Benchmark]
    public bool MulticastDelegateInvoke()
    {
        return _multicastDelegate!(_publishEvent);
    }

    private sealed class DirectDispatchTarget
    {
        private int _count;

        public bool OnPublish(PublishBenchEvent e)
        {
            _count += e.Value;
            return true;
        }
    }

    private abstract class DispatchTargetBase
    {
        public abstract bool OnPublish(PublishBenchEvent e);
    }

    private sealed class VirtualDispatchTarget : DispatchTargetBase
    {
        private int _count;

        public override bool OnPublish(PublishBenchEvent e)
        {
            _count += e.Value;
            return true;
        }
    }
}
