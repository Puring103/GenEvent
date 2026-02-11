using System.Collections.Generic;
using GenEvent.Runtime.example;

namespace GenEvent.Runtime
{
    public static class EventCenter
    {
        public static bool Publish<TGenEvent>(this TGenEvent gameEvent, object subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            var publisher = BaseEventPublisher.Publishers[typeof(TGenEvent)];
            var result = publisher != null && publisher.Publish(gameEvent, subscriber);
            PublishConfig<TGenEvent>.Instance.Clear();
         
            return result;
        }

        public static void StartListening<TSubscriber>(this TSubscriber subscriber)
            where TSubscriber : class
        {
            if (BaseSubscriberRegistry.Subscribers.TryGetValue(typeof(TSubscriber), out var iSubscriber))
            {
                iSubscriber.StartListening(subscriber);
            }
        }

        public static void StopListening<TSubscriber>(this TSubscriber subscriber)
            where TSubscriber : class
        {
            if (BaseSubscriberRegistry.Subscribers.TryGetValue(typeof(TSubscriber), out var iSubscriber))
            {
                iSubscriber.StopListening(subscriber);
            }
        }

        public static void StartListening<TSubscriber, TGenEvent>(this TSubscriber subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            BaseSubscriberRegistry.StartListening<TSubscriber, TGenEvent>(subscriber);
        }

        public static void StopListening<TSubscriber, TGenEvent>(this TSubscriber subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            BaseSubscriberRegistry.StopListening<TSubscriber, TGenEvent>(subscriber);
        }

        public static void Cancelable<TGenEvent>()
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            PublishConfig<TGenEvent>.Instance.SetCancelable();
        }

        public static void WithFilter<TFilter, TGenEvent>(TFilter filter)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TFilter : struct, ISubscriberFilter
        {
            PublishConfig<TGenEvent>.Instance.AddFilter(filter);
        }
    }

    public class PublishConfig<TEvent>
        where TEvent : struct, IGenEvent<TEvent>
    {
        private bool _cancelable = false;
        private List<ISubscriberFilter> _subscriberFilters { get; set; } = new(1);

        public bool Cancelable => _cancelable;
        public IReadOnlyList<ISubscriberFilter> SubscriberFilters => _subscriberFilters;


        private static PublishConfig<TEvent> instance;

        public static PublishConfig<TEvent> Instance
        {
            get
            {
                instance ??= new PublishConfig<TEvent>();
                return instance;
            }
        }

        public void Clear()
        {
            _cancelable = false;
            _subscriberFilters.Clear();
        }

        public void SetCancelable()
        {
            _cancelable = true;
        }

        public void AddFilter(ISubscriberFilter filter)
        {
            _subscriberFilters.Add(filter);
        }

        public bool IsFiltered(object subscriber)
        {
            foreach (var subscriberFilter in Instance.SubscriberFilters)
            {
                if (subscriberFilter.IsFiltered(subscriber))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public interface ISubscriberFilter
    {
        bool IsFiltered(object subscriber);
    }
}