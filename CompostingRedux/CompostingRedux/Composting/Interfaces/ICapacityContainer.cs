namespace CompostingRedux.Composting.Interfaces
{
    /// <summary>
    /// Interface for containers that have a maximum capacity limit.
    /// Used by composting bins, storage containers, and other inventory-based blocks.
    /// </summary>
    public interface ICapacityContainer
    {
        /// <summary>
        /// Gets the maximum capacity (number of items) this container can hold.
        /// </summary>
        int MaxCapacity { get; }

        /// <summary>
        /// Gets the current number of items in the container.
        /// </summary>
        int CurrentCount { get; }

        /// <summary>
        /// Gets the number of remaining slots available.
        /// </summary>
        int RemainingCapacity { get; }

        /// <summary>
        /// Gets whether the container is full.
        /// </summary>
        bool IsFull { get; }

        /// <summary>
        /// Gets whether the container is empty.
        /// </summary>
        bool IsEmpty { get; }

        /// <summary>
        /// Gets the fill ratio as a value between 0.0 (empty) and 1.0 (full).
        /// </summary>
        float FillRatio { get; }
    }
}