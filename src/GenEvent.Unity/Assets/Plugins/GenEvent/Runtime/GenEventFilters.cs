using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace GenEvent;

public static class GenEventFilters
{
    public static Predicate<object> ExcludeSubscriber(object subscriber)
    {
        if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
        return s => ReferenceEquals(s, subscriber);
    }

    public static Predicate<object> IncludeSubscriber(object subscriber)
    {
        if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
        return s => !ReferenceEquals(s, subscriber);
    }

    public static Predicate<object> ExcludeSubscribers(HashSet<object> subscribers)
    {
        if (subscribers == null) throw new ArgumentNullException(nameof(subscribers));
        return subscribers.Contains;
    }

    public static Predicate<object> IncludeSubscribers(HashSet<object> subscribers)
    {
        if (subscribers == null) throw new ArgumentNullException(nameof(subscribers));
        return s => !subscribers.Contains(s);
    }

    [SuppressMessage("ReSharper", "StaticMemberInGenericType")]
    private static class TypeFilterCache<TSubscriber>
    {
        // OnlyType: filter out (skip) when subscriber is NOT of type - only TSubscriber receives
        public static readonly Predicate<object> OnlyType = s => s is not TSubscriber;
        // ExcludeType: filter out (skip) when subscriber IS of type
        public static readonly Predicate<object> ExcludeType = s => s is TSubscriber;
    }

    public static Predicate<object> OnlyType<TSubscriber>()
    {
        return TypeFilterCache<TSubscriber>.OnlyType;
    }

    public static Predicate<object> ExcludeType<TSubscriber>()
    {
        return TypeFilterCache<TSubscriber>.ExcludeType;
    }
}