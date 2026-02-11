using System.Collections.Generic;
using GenEvent.Interface;

namespace GenEvent;

public delegate bool GenEventDelegate<in TGenEvent, in TSubscriber>(TGenEvent gameEvent, TSubscriber subscriber);

public static class GenEventRegistry<TGenEvent, TSubscriber>
    where TGenEvent : struct, IGenEvent<TGenEvent>
{
    private static readonly List<TSubscriber> SubscriberList = [];

    public static IReadOnlyList<TSubscriber> Subscribers => SubscriberList;
    public static GenEventDelegate<TGenEvent, TSubscriber> GenEvent { get; private set; }

    public static void Initialize(GenEventDelegate<TGenEvent, TSubscriber> genEventDelegate)
    {
        GenEvent = genEventDelegate;
    }

    public static void Register(TSubscriber observer)
    {
        SubscriberList.Add(observer);
    }

    public static void UnRegister(TSubscriber observer)
    {
        SubscriberList.Remove(observer);
    }
}