using GenEvent.Interface;

namespace CrossAssembly.Events
{
    public struct CrossAssemblyEvent : IGenEvent<CrossAssemblyEvent>
    {
        public int Value { get; set; }
    }

    public struct UnusedExternalEvent : IGenEvent<UnusedExternalEvent>
    {
        public int Value { get; set; }
    }
}

namespace CrossAssembly.Events.Orders
{
    public struct DuplicateNameEvent : IGenEvent<DuplicateNameEvent>
    {
        public int Value { get; set; }
    }
}

namespace CrossAssembly.Events.Billing
{
    public struct DuplicateNameEvent : IGenEvent<DuplicateNameEvent>
    {
        public int Value { get; set; }
    }
}
