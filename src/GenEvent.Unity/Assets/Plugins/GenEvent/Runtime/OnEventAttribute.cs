using System;

namespace GenEvent
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class OnEventAttribute : Attribute
    {
        public SubscriberPriority Priority { get; }

        public OnEventAttribute(SubscriberPriority priority = SubscriberPriority.Medium)
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