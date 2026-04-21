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
            public static readonly Predicate<object> OnlyType = s => s is not TSubscriber;
            public static readonly Predicate<object> ExcludeType = s => s is TSubscriber;
        }

        /// <summary>
        /// Creates a filter that excludes a specific subscriber.
        /// </summary>
        public static Predicate<object> ExcludeSubscriber(object subscriber)
        {
            if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
            return s => ReferenceEquals(s, subscriber);
        }

        /// <summary>
        /// Creates a filter that excludes a list of subscribers.
        /// </summary>
        public static Predicate<object> ExcludeSubscribers(HashSet<object> subscribers)
        {
            if (subscribers == null) throw new ArgumentNullException(nameof(subscribers));
            return subscribers.Contains;
        }

        /// <summary>
        /// Creates a filter that allows only a specific subscriber to pass through.
        /// </summary>
        public static Predicate<object> OnlySubscriber(object subscriber)
        {
            if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
            return s => !ReferenceEquals(s, subscriber);
        }

        /// <summary>
        /// Creates a filter that allows only a list of subscribers to pass through.
        /// </summary>
        public static Predicate<object> OnlySubscribers(HashSet<object> subscribers)
        {
            if (subscribers == null) throw new ArgumentNullException(nameof(subscribers));
            return s => !subscribers.Contains(s);
        }

        /// <summary>
        /// Creates a filter that allows only subscribers of a specific type to pass through.
        /// </summary>
        public static Predicate<object> OnlyType<TSubscriber>()
        {
            return TypeFilterCache<TSubscriber>.OnlyType;
        }

        /// <summary>
        /// Creates a filter that excludes subscribers of a specific type.
        /// </summary>
        public static Predicate<object> ExcludeType<TSubscriber>()
        {
            return TypeFilterCache<TSubscriber>.ExcludeType;
        }

        internal static Type GetOnlyTypeRuleType<TSubscriber>()
        {
            return typeof(TSubscriber);
        }

        internal static Type GetExcludeTypeRuleType<TSubscriber>()
        {
            return typeof(TSubscriber);
        }
    }
}
