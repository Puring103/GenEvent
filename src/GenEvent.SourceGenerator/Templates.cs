namespace GenEvent.SourceGenerator
{
    internal static class Templates
    {
        public const string GenEventBootstrap = @"using System;
using System.CodeDom.Compiler;
using GenEvent.Interface;

namespace GenEvent
{
[GeneratedCode(""GenEvent"",""V0.5"")]
internal static class GenEventBootstrap
{
{InitAttribute}
    public static void Init()
    {
{PublisherRegistrations}
{SubscriberRegistrations}
    }
}
}
";
        public const string EventPublisher = @"{UsingNamespaces}
using System.CodeDom.Compiler;

namespace GenEvent
{
[GeneratedCode(""GenEvent"",""V0.5"")]
public class {EventName}Publisher : BaseEventPublisher
{
    public override bool Publish<TGenEvent>(TGenEvent @event, PublishConfig<TGenEvent> config)
    {
        bool completed = true;

{SubscriberInvocations}

        return completed;
    }
}
}
";

        public const string SubscriberRegistry = @"{UsingNamespaces}
using System.CodeDom.Compiler;

namespace GenEvent
{
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
}
";
    }
}