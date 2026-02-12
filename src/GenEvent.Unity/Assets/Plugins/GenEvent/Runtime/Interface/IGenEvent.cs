namespace GenEvent.Interface
{
    /// <summary>
    /// Interface for events. All events must be value types (struct).
    /// Using value type to avoid boxing and allocation.
    /// The generic type parameter is only used for type constraints.
    /// </summary>
    public interface IGenEvent<TGenEvent>
        where TGenEvent : struct{}
}