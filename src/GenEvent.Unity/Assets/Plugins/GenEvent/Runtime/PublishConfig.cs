using System;
using System.Collections.Generic;
using GenEvent.Interface;

namespace GenEvent;

public class PublishConfig<TGenEvent>
    where TGenEvent : struct, IGenEvent<TGenEvent>
{
    private const int PoolCapacity = 16;

    public bool Cancelable { get; private set; } = false;
    private List<Predicate<object>> SubscriberFilters { get; set; } = new(16);

    private static PublishConfig<TGenEvent> _instance;
    private static PublishConfig<TGenEvent> _mainInstance;
    private static readonly Stack<PublishConfig<TGenEvent>> _stack = new();
    private static readonly List<PublishConfig<TGenEvent>> _pool = new(PoolCapacity);

    public static PublishConfig<TGenEvent> Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new PublishConfig<TGenEvent>();
                _mainInstance = _instance;
            }
            return _instance;
        }
    }

    /// <summary>Config used for the current Publish (stack top when in a Publish, otherwise Instance).</summary>
    public static PublishConfig<TGenEvent> GetCurrentConfig()
    {
        if (_stack.Count > 0)
            return _stack.Peek();
        if (_instance == null)
        {
            _instance = new PublishConfig<TGenEvent>();
            _mainInstance = _instance;
        }
        return _instance;
    }

    private static PublishConfig<TGenEvent> GetFromPool()
    {
        if (_pool.Count > 0)
        {
            var i = _pool.Count - 1;
            var config = _pool[i];
            _pool.RemoveAt(i);
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
        if (_pool.Count < PoolCapacity)
            _pool.Add(config);
    }

    /// <summary>Call at Publish() entry: push current Instance, then replace Instance with a pooled config.</summary>
    internal static void PushLayer()
    {
        if (_instance == null)
        {
            _instance = new PublishConfig<TGenEvent>();
            _mainInstance = _instance;
        }
        _stack.Push(_instance);
        _instance = GetFromPool();
    }

    /// <summary>Call at Publish() exit: pop layer, return current Instance to pool, restore Instance; clear if outermost.</summary>
    internal static void PopLayer()
    {
        var popped = _stack.Pop();
        ReturnToPool(_instance);
        _instance = popped;
        if (_stack.Count == 0)
            _instance.Clear();
    }

    public void Clear()
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