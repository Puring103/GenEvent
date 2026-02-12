using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace GenEvent
{
    /// <summary>
    /// Static class containing helper methods for event filtering.
    /// Subscriber filtered out if the filter returns true.
    /// </summary>
    public static class GenEventFilters
    {
        [SuppressMessage("ReSharper", "StaticMemberInGenericType")]
        private static class TypeFilterCache<TSubscriber>
        {
            // OnlyType: filter out (skip) when subscriber is NOT of type - only TSubscriber receives
            public static readonly Predicate<object> OnlyType = s => s is not TSubscriber;
            // ExcludeType: filter out (skip) when subscriber IS of type
            public static readonly Predicate<object> ExcludeType = s => s is TSubscriber;
        }

        /// <summary>
        /// Creates a filter that excludes a specific subscriber.
        /// </summary>
        /// <param name="subscriber">The subscriber to exclude.</param>
        /// <returns>A filter that excludes the specified subscriber.</returns>
        public static Predicate<object> ExcludeSubscriber(object subscriber)
        {
            if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
            return s => ReferenceEquals(s, subscriber);
        }

        /// <summary>
        /// Creates a filter that excludes a list of subscribers.
        /// </summary>
        /// <param name="subscribers">The list of subscribers to exclude.</param>
        /// <returns>A filter that excludes the specified subscribers.</returns>
        public static Predicate<object> ExcludeSubscribers(HashSet<object> subscribers)
        {
            if (subscribers == null) throw new ArgumentNullException(nameof(subscribers));
            return subscribers.Contains;
        }

        /// <summary>
        /// Creates a filter that allows only a specific subscriber to pass through.
        /// </summary>
        /// <param name="subscriber">The subscriber to allow.</param>
        /// <returns>A filter that allows only the specified subscriber.</returns>
        public static Predicate<object> OnlySubscriber(object subscriber)
        {
            if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
            return s => !ReferenceEquals(s, subscriber);
        }

        /// <summary>
        /// Creates a filter that allows only a list of subscribers to pass through.
        /// </summary>
        /// <param name="subscribers">The list of subscribers to allow.</param>
        /// <returns>A filter that allows only the specified subscribers.</returns>
        public static Predicate<object> OnlySubscribers(HashSet<object> subscribers)
        {
            if (subscribers == null) throw new ArgumentNullException(nameof(subscribers));
            return s => !subscribers.Contains(s);
        }

        /// <summary>
        /// Creates a filter that allows only subscribers of a specific type to pass through.
        /// </summary>
        /// <typeparam name="TSubscriber">The type of subscriber to allow.</typeparam>
        /// <returns>A filter that allows only subscribers of the specified type.</returns>
        public static Predicate<object> OnlyType<TSubscriber>()
        {
            return TypeFilterCache<TSubscriber>.OnlyType;
        }

        /// <summary>
        /// Creates a filter that excludes subscribers of a specific type.
        /// </summary>
        /// <typeparam name="TSubscriber">The type of subscriber to exclude.</typeparam>
        /// <returns>A filter that excludes subscribers of the specified type.</returns>
        public static Predicate<object> ExcludeType<TSubscriber>()
        {
            return TypeFilterCache<TSubscriber>.ExcludeType;
        }
    }
}