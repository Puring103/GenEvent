using System;
using System.Collections.Generic;
using GenEvent.Interface;

namespace GenEvent;

public class PublishConfig<TGenEvent>
    where TGenEvent : struct, IGenEvent<TGenEvent>
{
    private const int PoolCapacity = 16;

    public bool Cancelable { get; private set; }
    private List<Predicate<object>> SubscriberFilters { get; } = new(16);

    private static PublishConfig<TGenEvent> _instance;
    private static PublishConfig<TGenEvent> _mainInstance;
    private static readonly Stack<PublishConfig<TGenEvent>> Stack = new(PoolCapacity);
    private static readonly List<PublishConfig<TGenEvent>> Pool = new(PoolCapacity);

    public static PublishConfig<TGenEvent> Instance
    {
        get
        {
            if (_instance != null) return _instance;
            
            _instance = new PublishConfig<TGenEvent>();
            _mainInstance = _instance;
            return _instance;
        }
    }

    /// <summary>Config used for the current Publish (stack top when in a Publish, otherwise Instance).</summary>
    public static PublishConfig<TGenEvent> GetCurrentConfig()
    {
        if (Stack.Count > 0)
            return Stack.Peek();
        if (_instance == null)
        {
            _instance = new PublishConfig<TGenEvent>();
            _mainInstance = _instance;
        }
        return _instance;
    }

    private static PublishConfig<TGenEvent> GetFromPool()
    {
        if (Pool.Count > 0)
        {
            var i = Pool.Count - 1;
            var config = Pool[i];
            Pool.RemoveAt(i);
            config.Clear();
            return config;
        }
        return new PublishConfig<TGenEvent>();
    }

    private static void ReturnToPool(PublishConfig<TGenEvent> config)
    {
        if (config == _mainInstance)
            return;
        config.Clear();
        if (Pool.Count < PoolCapacity)
            Pool.Add(config);
    }

    /// <summary>Call at Publish() entry: push current Instance, then replace Instance with a pooled config.</summary>
    internal static void PushLayer()
    {
        if (_instance == null)
        {
            _instance = new PublishConfig<TGenEvent>();
            _mainInstance = _instance;
        }
        Stack.Push(_instance);
        _instance = GetFromPool();
    }

    /// <summary>Call at Publish() exit: pop layer, return current Instance to pool, restore Instance; clear if outermost.</summary>
    internal static void PopLayer()
    {
        var popped = Stack.Pop();
        ReturnToPool(_instance);
        _instance = popped;
        if (Stack.Count == 0)
            _instance.Clear();
    }

    private void Clear()
    {
        Cancelable = false;
        SubscriberFilters.Clear();
    }

    public void SetCancelable()
    {
        Cancelable = true;
    }

    public void AddFilter(Predicate<object> filter)
    {
        SubscriberFilters.Add(filter);
    }

    public bool IsFiltered(object subscriber)
    {
        for (int i = 0; i < SubscriberFilters.Count; i++)
        {
            if (SubscriberFilters[i](subscriber))
            {
                return true;
            }
        }

        return false;
    }
}