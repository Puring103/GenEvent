using System.Collections.Generic;
using GenEvent.Interface;

namespace GenEvent
{
    public static class EventCenter
    {
        public static bool Publish<TGenEvent>(this TGenEvent gameEvent, object subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            var publisher = BaseEventPublisher.Publishers[typeof(TGenEvent)];
            var result = publisher.Publish(gameEvent, subscriber);
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

        public static bool Invoke<TSubscriber, TGenEvent>(this TGenEvent gameEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            var completed = true;
            var subscribers = GenEventRegistry<TGenEvent, TSubscriber>.Subscribers;
            var genEvent = GenEventRegistry<TGenEvent, TSubscriber>.GenEvent;

            for (int i = 0; i < subscribers.Count; i++)
            {
                var subscriber = subscribers[i];
                if (PublishConfig<TGenEvent>.Instance.IsFiltered(subscriber))
                    continue;

                var shouldContinue = genEvent?.Invoke(gameEvent, subscriber) ?? true;

                if (!PublishConfig<TGenEvent>.Instance.Cancelable || shouldContinue) continue;
                completed = false;
                break;
            }

            return completed;
        }
    }

    public class PublishConfig<TGenEvent>
        where TGenEvent : struct, IGenEvent<TGenEvent>
    {
        private bool _cancelable = false;
        public bool Cancelable => _cancelable;
        private List<ISubscriberFilter> _subscriberFilters { get; set; } = new(1);

        private static PublishConfig<TGenEvent> instance;

        public static PublishConfig<TGenEvent> Instance
        {
            get
            {
                instance ??= new PublishConfig<TGenEvent>();
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
            for (int i = 0; i < Instance._subscriberFilters.Count; i++)
            {
                if (Instance._subscriberFilters[i].IsFiltered(subscriber))
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