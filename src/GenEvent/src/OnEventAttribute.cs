using System;

namespace GenEvent;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class OnEventAttribute(SubscriberPriority priority = SubscriberPriority.Medium) : Attribute
{
    public SubscriberPriority Priority { get; } = priority;
}


public enum SubscriberPriority
{
    Primary,
    High,
    Medium,
    Low,
    End
}