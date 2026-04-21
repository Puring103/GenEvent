using System;
namespace GenEvent
{
    /// <summary>
    /// Shared runtime guards for bootstrap state and missing registrations.
    /// </summary>
    public static class GenEventRuntimeGuard
    {
        /// <summary>
        /// Creates a missing publisher exception for the given event type.
        /// </summary>
        public static InvalidOperationException CreateMissingPublisherException(Type eventType, string operationName)
        {
            return new InvalidOperationException(
                $"No publisher is registered for event type '{eventType.FullName}' while calling '{operationName}'. " +
                "Ensure GenEventBootstrap.Init() has been called for the relevant assembly before publishing or event-specific subscription.");
        }

        /// <summary>
        /// Creates a missing subscriber registry exception for the given subscriber type.
        /// </summary>
        public static InvalidOperationException CreateMissingSubscriberRegistryException(Type subscriberType, string operationName)
        {
            return new InvalidOperationException(
                $"No subscriber registry is registered for subscriber type '{subscriberType.FullName}' while calling '{operationName}'. " +
                "Ensure GenEventBootstrap.Init() has been called for the relevant assembly before subscribing or unsubscribing.");
        }
    }
}
