using System;

namespace GenEvent.Runtime
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class OnGameEventAttribute : Attribute
    {
        public SubscriberPriority Priority { get; }

        public OnGameEventAttribute(SubscriberPriority priority = SubscriberPriority.Medium)
        {
            Priority = priority;
        }
    }


    public enum SubscriberPriority
    {
        Primary,
        High,
        Medium,
        Low,
        End
    }
}