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
public static class GenEventBootstrap
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
using System.Threading.Tasks;
using System.CodeDom.Compiler;

namespace GenEvent
{
[GeneratedCode(""GenEvent"",""V0.5"")]
public class {EventClassName} : BaseEventPublisher
{
    public override bool Publish<TGenEvent>(TGenEvent @event, PublishConfig<TGenEvent> config)
    {
        bool completed = true;

{SubscriberSnapshots}
        try
        {
{SubscriberInvocations}

            return completed;
        }
        finally
        {
{SubscriberSnapshotReturns}
        }
    }

    public override async Task<bool> PublishAsync<TGenEvent>(TGenEvent @event, PublishConfig<TGenEvent> config)
    {
        bool completed = true;

{SubscriberSnapshotsAsync}
        try
        {
{SubscriberInvocationsAsync}

            return completed;
        }
        finally
        {
{SubscriberSnapshotReturnsAsync}
        }
    }
}
}
";

        public const string SubscriberRegistry = @"{UsingNamespaces}
using System.Threading.Tasks;
using System.CodeDom.Compiler;

namespace GenEvent
{
[GeneratedCode(""GenEvent"",""V0.5"")]
public class {SubscriberRegistryClassName} : BaseSubscriberRegistry
{
    static {SubscriberRegistryClassName}()
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
