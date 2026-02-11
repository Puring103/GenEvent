using GenEvent;
using GenEvent.Interface;

// Event types for testing
public struct TestEventA : IGenEvent<TestEventA>
{
    public int Value;
}

public struct TestEventB : IGenEvent<TestEventB>
{
    public string Data;
}

public struct TestEventC : IGenEvent<TestEventC>
{
    public int Value;
}

// Struct event for value semantics test
public struct StructEvent : IGenEvent<StructEvent>
{
    public int Value;
}

// Subscribers for basic and multi-subscriber tests
public class SubscriberA
{
    public int ReceiveCount;
    public int LastValue;

    [OnEvent]
    public bool OnTestEventA(TestEventA e)
    {
        ReceiveCount++;
        LastValue = e.Value;
        return true;
    }
}

public class SubscriberB
{
    public int ReceiveCount;

    [OnEvent]
    public bool OnTestEventA(TestEventA e)
    {
        ReceiveCount++;
        return true;
    }
}

public class SubscriberC
{
    public int ReceiveCount;

    [OnEvent]
    public bool OnTestEventB(TestEventB e)
    {
        ReceiveCount++;
        return true;
    }
}

// Subscriber listening to multiple event types
public class MultiEventSubscriber
{
    public int EventACount;
    public int EventBCount;

    [OnEvent]
    public bool OnTestEventA(TestEventA e)
    {
        EventACount++;
        return true;
    }

    [OnEvent]
    public bool OnTestEventB(TestEventB e)
    {
        EventBCount++;
        return true;
    }
}

// Cancelable test - returns false after receiving to stop propagation
public class CancelSubscriber
{
    public int ReceiveCount;
    public bool ShouldCancel;

    [OnEvent]
    public bool OnTestEventA(TestEventA e)
    {
        ReceiveCount++;
        return !ShouldCancel;
    }
}

// Priority test subscribers
public class PrimaryPrioritySubscriber
{
    public int CallOrder = -1;

    [OnEvent(SubscriberPriority.Primary)]
    public bool OnTestEventC(TestEventC e)
    {
        CallOrder = 0;
        return true;
    }
}

public class HighPrioritySubscriber
{
    public int CallOrder = -1;
    public bool CancelPropagation;

    [OnEvent(SubscriberPriority.High)]
    public bool OnTestEventC(TestEventC e)
    {
        CallOrder = 1;
        return !CancelPropagation;
    }
}

public class MediumPrioritySubscriber
{
    public int CallOrder = -1;

    [OnEvent(SubscriberPriority.Medium)]
    public bool OnTestEventC(TestEventC e)
    {
        CallOrder = 2;
        return true;
    }
}

public class LowPrioritySubscriber
{
    public int CallOrder = -1;

    [OnEvent(SubscriberPriority.Low)]
    public bool OnTestEventC(TestEventC e)
    {
        CallOrder = 3;
        return true;
    }
}

public class EndPrioritySubscriber
{
    public int CallOrder = -1;

    [OnEvent(SubscriberPriority.End)]
    public bool OnTestEventC(TestEventC e)
    {
        CallOrder = 4;
        return true;
    }
}

// Filter test subscribers (different types for OnlyType/ExcludeType)
public class FilterSubscriberType1
{
    public int ReceiveCount;

    [OnEvent]
    public bool OnTestEventA(TestEventA e)
    {
        ReceiveCount++;
        return true;
    }
}

public class FilterSubscriberType2
{
    public int ReceiveCount;

    [OnEvent]
    public bool OnTestEventA(TestEventA e)
    {
        ReceiveCount++;
        return true;
    }
}

// Struct event subscriber (value semantics)
public class StructEventSubscriber
{
    public int ReceivedValue;

    [OnEvent]
    public bool OnStructEvent(StructEvent e)
    {
        ReceivedValue = e.Value;
        e.Value = 999; // Modify copy - should not affect caller
        return true;
    }
}

// Re-publish same event from within handler (tests that inner PublishConfig is independent)
public class RepublishSameEventSubscriber
{
    public int ReceiveCount;

    [OnEvent]
    public bool OnTestEventA(TestEventA e)
    {
        ReceiveCount++;
        if (ReceiveCount == 1)
        {
            new TestEventA { Value = 42 }.Publish();
        }
        return true;
    }
}

// Multi-level nested: level 1 republishes with OnlyType<NestedMidSub>, level 2 republishes with no filter
public class NestedOuterSub
{
    public int ReceiveCount;

    [OnEvent]
    public bool OnTestEventA(TestEventA e)
    {
        ReceiveCount++;
        if (ReceiveCount == 1)
            new TestEventA { Value = 2 }.OnlyType<TestEventA, NestedMidSub>().Publish();
        return true;
    }
}

public class NestedMidSub
{
    public int ReceiveCount;

    [OnEvent]
    public bool OnTestEventA(TestEventA e)
    {
        ReceiveCount++;
        if (ReceiveCount == 1)
            new TestEventA { Value = 3 }.Publish();
        return true;
    }
}

public class NestedInnerSub
{
    public int ReceiveCount;

    [OnEvent]
    public bool OnTestEventA(TestEventA e)
    {
        ReceiveCount++;
        return true;
    }
}

// Publish different event from within handler
public class RepublishDifferentEventSubscriber
{
    public int EventACount;
    public int EventBCount;

    [OnEvent]
    public bool OnTestEventA(TestEventA e)
    {
        EventACount++;
        if (EventACount == 1)
        {
            new TestEventB { Data = "from A" }.Publish();
        }
        return true;
    }

    [OnEvent]
    public bool OnTestEventB(TestEventB e)
    {
        EventBCount++;
        return true;
    }
}
