using GenEvent;
using GenEvent.Interface;
using System.Reflection;

namespace Tests;

internal static class TestRuntimeState
{
    public static void Reset()
    {
        BaseEventPublisher.Publishers.Clear();
        BaseSubscriberRegistry.Subscribers.Clear();
        ResetBootstrapInitializationFlags();
    }

    private static void ResetBootstrapInitializationFlags()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var bootstrapType = assembly.GetType("GenEvent.GenEventBootstrap");
            if (bootstrapType == null)
                continue;

            var backingField = bootstrapType.GetField("<IsInitialized>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            backingField?.SetValue(null, false);
        }
    }
}
