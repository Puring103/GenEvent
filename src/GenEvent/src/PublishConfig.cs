using System;
using System.Collections.Generic;
using GenEvent.Interface;

namespace GenEvent
{
    internal enum FilterRuleKind : byte
    {
        ExcludeSubscriber,
        OnlySubscriber,
        ExcludeSubscribers,
        OnlySubscribers,
        OnlyType,
        ExcludeType
    }

    internal readonly struct FilterRule
    {
        public FilterRule(FilterRuleKind kind, object payload)
        {
            Kind = kind;
            Payload = payload;
        }

        public FilterRuleKind Kind { get; }

        public object Payload { get; }

        public bool IsFiltered(object subscriber)
        {
            return Kind switch
            {
                FilterRuleKind.ExcludeSubscriber => ReferenceEquals(subscriber, Payload),
                FilterRuleKind.OnlySubscriber => !ReferenceEquals(subscriber, Payload),
                FilterRuleKind.ExcludeSubscribers => ((HashSet<object>)Payload).Contains(subscriber),
                FilterRuleKind.OnlySubscribers => !((HashSet<object>)Payload).Contains(subscriber),
                FilterRuleKind.OnlyType => !((Type)Payload).IsInstanceOfType(subscriber),
                FilterRuleKind.ExcludeType => ((Type)Payload).IsInstanceOfType(subscriber),
                _ => false
            };
        }
    }

    /// <summary>
    /// Configuration for a single event publish.
    /// </summary>
    /// <typeparam name="TGenEvent">The event type.</typeparam>
    public struct PublishConfig<TGenEvent>
        where TGenEvent : struct, IGenEvent<TGenEvent>
    {
        private const byte InlineRuleCapacity = 4;

        private Predicate<object> _customFilter;
        private byte _ruleCount;
        private FilterRule _rule0;
        private FilterRule _rule1;
        private FilterRule _rule2;
        private FilterRule _rule3;

        /// <summary>
        /// Indicates whether the event is cancelable.
        /// </summary>
        public bool Cancelable { get; private set; }

        internal bool HasFilters => _ruleCount != 0 || _customFilter != null;

        internal bool IsDefault => !Cancelable && !HasFilters;

        internal void Reset()
        {
            this = default;
        }

        /// <summary>
        /// Sets the publish config as cancelable.
        /// </summary>
        public void SetCancelable()
        {
            Cancelable = true;
        }

        /// <summary>
        /// Adds a filter to the publish config.
        /// </summary>
        /// <param name="filter">The filter to add.</param>
        public void AddFilter(Predicate<object> filter)
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            if (_customFilter == null)
            {
                _customFilter = filter;
                return;
            }

            var existingFilter = _customFilter;
            _customFilter = subscriber => existingFilter(subscriber) || filter(subscriber);
        }

        /// <summary>
        /// Checks if a subscriber should be filtered.
        /// </summary>
        /// <param name="subscriber">The subscriber to check.</param>
        /// <returns>True if the subscriber is filtered, false otherwise.</returns>
        public bool IsFiltered(object subscriber)
        {
            if (IsFilteredByInlineRules(subscriber))
            {
                return true;
            }

            return _customFilter?.Invoke(subscriber) ?? false;
        }

        internal void SetExcludeSubscriber(object subscriber)
        {
            AddInlineRule(new FilterRule(FilterRuleKind.ExcludeSubscriber, subscriber ?? throw new ArgumentNullException(nameof(subscriber))));
        }

        internal void SetOnlySubscriber(object subscriber)
        {
            AddInlineRule(new FilterRule(FilterRuleKind.OnlySubscriber, subscriber ?? throw new ArgumentNullException(nameof(subscriber))));
        }

        internal void SetExcludeSubscribers(HashSet<object> subscribers)
        {
            AddInlineRule(new FilterRule(FilterRuleKind.ExcludeSubscribers, subscribers ?? throw new ArgumentNullException(nameof(subscribers))));
        }

        internal void SetOnlySubscribers(HashSet<object> subscribers)
        {
            AddInlineRule(new FilterRule(FilterRuleKind.OnlySubscribers, subscribers ?? throw new ArgumentNullException(nameof(subscribers))));
        }

        internal void SetOnlyType(Type subscriberType)
        {
            AddInlineRule(new FilterRule(FilterRuleKind.OnlyType, subscriberType ?? throw new ArgumentNullException(nameof(subscriberType))));
        }

        internal void SetExcludeType(Type subscriberType)
        {
            AddInlineRule(new FilterRule(FilterRuleKind.ExcludeType, subscriberType ?? throw new ArgumentNullException(nameof(subscriberType))));
        }

        internal bool TryGetOnlySubscriber(out object subscriber)
        {
            if (_customFilter == null && _ruleCount == 1 && _rule0.Kind == FilterRuleKind.OnlySubscriber)
            {
                subscriber = _rule0.Payload;
                return true;
            }

            subscriber = null;
            return false;
        }

        internal bool TryGetExcludeSubscriber(out object subscriber)
        {
            if (_customFilter == null && _ruleCount == 1 && _rule0.Kind == FilterRuleKind.ExcludeSubscriber)
            {
                subscriber = _rule0.Payload;
                return true;
            }

            subscriber = null;
            return false;
        }

        internal bool TryGetOnlySubscribers(out HashSet<object> subscribers)
        {
            if (_customFilter == null && _ruleCount == 1 && _rule0.Kind == FilterRuleKind.OnlySubscribers)
            {
                subscribers = (HashSet<object>)_rule0.Payload;
                return true;
            }

            subscribers = null;
            return false;
        }

        internal bool TryGetExcludeSubscribers(out HashSet<object> subscribers)
        {
            if (_customFilter == null && _ruleCount == 1 && _rule0.Kind == FilterRuleKind.ExcludeSubscribers)
            {
                subscribers = (HashSet<object>)_rule0.Payload;
                return true;
            }

            subscribers = null;
            return false;
        }

        internal bool TryGetOnlyType(out Type subscriberType)
        {
            if (_customFilter == null && _ruleCount == 1 && _rule0.Kind == FilterRuleKind.OnlyType)
            {
                subscriberType = (Type)_rule0.Payload;
                return true;
            }

            subscriberType = null;
            return false;
        }

        internal bool TryGetExcludeType(out Type subscriberType)
        {
            if (_customFilter == null && _ruleCount == 1 && _rule0.Kind == FilterRuleKind.ExcludeType)
            {
                subscriberType = (Type)_rule0.Payload;
                return true;
            }

            subscriberType = null;
            return false;
        }

        private void AddInlineRule(FilterRule rule)
        {
            switch (_ruleCount)
            {
                case 0:
                    _rule0 = rule;
                    _ruleCount = 1;
                    return;
                case 1:
                    _rule1 = rule;
                    _ruleCount = 2;
                    return;
                case 2:
                    _rule2 = rule;
                    _ruleCount = 3;
                    return;
                case 3:
                    _rule3 = rule;
                    _ruleCount = InlineRuleCapacity;
                    return;
                default:
                    PromoteInlineRulesToCustomFilter(rule);
                    return;
            }
        }

        private bool IsFilteredByInlineRules(object subscriber)
        {
            if (_ruleCount > 0 && _rule0.IsFiltered(subscriber))
            {
                return true;
            }

            if (_ruleCount > 1 && _rule1.IsFiltered(subscriber))
            {
                return true;
            }

            if (_ruleCount > 2 && _rule2.IsFiltered(subscriber))
            {
                return true;
            }

            return _ruleCount > 3 && _rule3.IsFiltered(subscriber);
        }

        private void PromoteInlineRulesToCustomFilter(FilterRule overflowRule)
        {
            var snapshot = this;
            _rule0 = default;
            _rule1 = default;
            _rule2 = default;
            _rule3 = default;
            _ruleCount = 0;

            AddFilter(subscriber => snapshot.IsFiltered(subscriber) || overflowRule.IsFiltered(subscriber));
        }
    }
}
