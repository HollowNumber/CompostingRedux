namespace CompostingRedux.Composting.Interfaces
{
    /// <summary>
    /// Interface for composting systems that support manual turning/mixing to accelerate decomposition.
    /// Turning introduces oxygen and redistributes materials for more efficient composting.
    /// </summary>
    public interface ITurnable
    {
        /// <summary>
        /// Gets whether the pile can currently be turned (cooldown period has elapsed).
        /// </summary>
        bool CanTurn { get; }

        /// <summary>
        /// Gets the hours remaining until the pile can be turned again.
        /// </summary>
        double TurnCooldownRemaining { get; }

        /// <summary>
        /// Turns the pile, speeding up decomposition by the specified number of hours.
        /// </summary>
        /// <param name="speedupHours">Number of hours to advance the decomposition process</param>
        void Turn(int speedupHours);
    }
}