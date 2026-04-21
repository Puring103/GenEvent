using GenEvent.Interface;

namespace GenEvent
{
    /// <summary>
    /// Represents an event value plus a single-use publish configuration.
    /// </summary>
    /// <typeparam name="TGenEvent">The event type.</typeparam>
    public readonly struct ConfiguredEvent<TGenEvent>
        where TGenEvent : struct, IGenEvent<TGenEvent>
    {
        internal ConfiguredEvent(TGenEvent @event, PublishConfig<TGenEvent> config)
        {
            Event = @event;
            Config = config;
        }

        /// <summary>
        /// The configured event value to publish.
        /// </summary>
        public TGenEvent Event { get; }

        internal PublishConfig<TGenEvent> Config { get; }
    }
}
