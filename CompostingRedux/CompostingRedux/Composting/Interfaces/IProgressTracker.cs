using System;

namespace CompostingRedux.Composting.Interfaces
{
    /// <summary>
    /// Interface for objects that track decomposition or processing progress over time.
    /// Provides standardized progress reporting for composting, fermenting, or other time-based processes.
    /// </summary>
    public interface IProgressTracker
    {
        /// <summary>
        /// Gets the current progress as a percentage (0-100).
        /// </summary>
        int ProgressPercent { get; }

        /// <summary>
        /// Gets whether the process is complete.
        /// </summary>
        bool IsFinished { get; }

        /// <summary>
        /// Gets the number of hours that have elapsed since the process started.
        /// </summary>
        int ElapsedHours { get; }

        /// <summary>
        /// Gets the estimated number of hours remaining until completion.
        /// </summary>
        int RemainingHours { get; }

        /// <summary>
        /// Updates the progress based on elapsed time and current conditions.
        /// </summary>
        void Update();

        /// <summary>
        /// Resets the progress tracker to its initial state.
        /// </summary>
        void Reset();
    }
}