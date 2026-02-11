namespace GenEvent.Editor
{
    internal static class CodeGeneratorTemplates
    {
        public const string PublisherTemplate = @"{USINGS}
using System.CodeDom.Compiler;
[GeneratedCode(""GenEvent"",""V0.5"")]
public class {EVENT_NAME}Publisher : BaseEventPublisher
{
    public override bool Publish<TGenEvent>(TGenEvent @event, object emitter)
    {
        bool completed = false;

{INVOKE_CALLS}

        return true;
    }
}
";

        public const string SubscriberRegistryTemplate = @"{USINGS}
using System.CodeDom.Compiler;
[GeneratedCode(""GenEvent"",""V0.5"")]
public class {SUBSCRIBER_NAME}SubscriberRegistry : BaseSubscriberRegistry
{
    static {SUBSCRIBER_NAME}SubscriberRegistry()
    {
{INITIALIZATIONS}
    }

    public override void StartListening<TSubscriber>(TSubscriber self)
        where TSubscriber : class
    {
{START_LISTENING_CALLS}
    }

    public override void StopListening<TSubscriber>(TSubscriber self)
        where TSubscriber : class
    {
{STOP_LISTENING_CALLS}
    }
}
";

        public const string EventPublishersTemplate = @"using System;
using System.Collections.Generic;
using System.CodeDom.Compiler;
{NAMESPACE_USINGS}
[GeneratedCode(""GenEvent"",""V0.5"")]
public abstract partial class BaseEventPublisher
{
    static BaseEventPublisher()
    {
{REGISTRATIONS}
    }
}
";

        public const string SubscriberRegistrysTemplate = @"{NAMESPACE_USINGS}
using System.CodeDom.Compiler;
[GeneratedCode(""GenEvent"",""V0.5"")]
public abstract partial class BaseSubscriberRegistry
{
    static BaseSubscriberRegistry()
    {
{REGISTRATIONS}
    }
}
";
    }
}

