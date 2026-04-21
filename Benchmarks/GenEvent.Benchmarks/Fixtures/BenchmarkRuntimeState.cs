using GenEvent;
using System.Reflection;
using GenEvent.Interface;

namespace GenEvent.Benchmarks.Fixtures;

internal static class BenchmarkRuntimeState
{
    public static void Reset()
    {
        BaseEventPublisher.Publishers.Clear();
        BaseSubscriberRegistry.Subscribers.Clear();
        ResetBootstrapInitializationFlags();
    }

    public static void ResetAndInit()
    {
        Reset();
        GenEventBootstrap.Init();
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
