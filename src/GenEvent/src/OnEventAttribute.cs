using System;

namespace GenEvent
{
    /// <summary>
    /// Attribute for marking a method as an event handler.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class OnEventAttribute : Attribute
    {
        /// <summary>
        /// Constructor for the OnEventAttribute.
        /// </summary>
        /// <param name="priority">The priority of the event handler.</param>
        public OnEventAttribute(SubscriberPriority priority = SubscriberPriority.Medium)
        {
            Priority = priority;
        }

        /// <summary>
        /// The priority of the event handler.
        /// </summary>
        public SubscriberPriority Priority { get; }
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