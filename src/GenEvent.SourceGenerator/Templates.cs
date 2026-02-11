namespace GenEvent.SourceGenerator
{
    internal static class Templates
    {
        public const string EventPublisher = @"{UsingNamespaces}
using System.CodeDom.Compiler;

[GeneratedCode(""GenEvent"",""V0.5"")]
public class {EventName}Publisher : BaseEventPublisher
{
    public override bool Publish<TGenEvent>(TGenEvent @event, object emitter)
    {
        bool completed = true;

{SubscriberInvocations}

        return completed;
    }
}
";

        public const string SubscriberRegistry = @"{UsingNamespaces}
using System.CodeDom.Compiler;

[GeneratedCode(""GenEvent"",""V0.5"")]
public class {SubscriberName}SubscriberRegistry : BaseSubscriberRegistry
{
    static {SubscriberName}SubscriberRegistry()
    {
{EventRegistrations}
    }

    public override void StartListening<TSubscriber>(TSubscriber self)
        where TSubscriber : class
    {
{StartListeningCalls}
    }

    public override void StopListening<TSubscriber>(TSubscriber self)
        where TSubscriber : class
    {
{StopListeningCalls}
    }
}
";

        public const string EventPublishers = @"using System;
using System.Collections.Generic;
using System.CodeDom.Compiler;

namespace GenEvent.Interface
{
[GeneratedCode(""GenEvent"",""V0.5"")]
public abstract partial class BaseEventPublisher
{
    static BaseEventPublisher()
    {
{PublisherRegistrations}
    }
}
}
";

        public const string SubscriberRegistrys = @"using System;
using System.Collections.Generic;
using System.CodeDom.Compiler;

namespace GenEvent.Interface
{
[GeneratedCode(""GenEvent"",""V0.5"")]
public abstract partial class BaseSubscriberRegistry
{
    static BaseSubscriberRegistry()
    {
{SubscriberRegistrations}
    }
}
}
";
    }
}
