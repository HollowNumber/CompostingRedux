using Vintagestory.API.Common;

namespace CompostingRedux.Composting.Moisture
{
    /// <summary>
    /// Interface for block entities that can track and manage moisture levels.
    /// Implement this interface to add moisture management to any block entity.
    /// </summary>
    public interface IMoistureControllable
    {
        /// <summary>
        /// Gets the moisture manager for this block entity.
        /// </summary>
        MoistureManager MoistureManager { get; }

        /// <summary>
        /// Gets the current moisture level (0.0 to 1.0).
        /// </summary>
        float MoistureLevel { get; }

        /// <summary>
        /// Gets whether the moisture is in optimal range.
        /// </summary>
        bool HasOptimalMoisture { get; }

        /// <summary>
        /// Gets whether the block is too dry.
        /// </summary>
        bool IsTooDry { get; }

        /// <summary>
        /// Gets whether the block is too wet.
        /// </summary>
        bool IsTooWet { get; }

        /// <summary>
        /// Gets a descriptive state of the moisture ("Optimal", "Too Dry", etc.).
        /// </summary>
        string MoistureState { get; }

        /// <summary>
        /// Adds water to increase moisture level.
        /// </summary>
        /// <param name="amount">Amount to add (0.0 to 1.0)</param>
        void AddWater(float amount);

        /// <summary>
        /// Adds dry material to decrease moisture level.
        /// </summary>
        /// <param name="amount">Amount to remove (0.0 to 1.0)</param>
        void AddDryMaterial(float amount);

        /// <summary>
        /// Updates moisture based on environmental conditions.
        /// Should be called regularly (e.g., hourly).
        /// </summary>
        void UpdateMoisture();
    }
}