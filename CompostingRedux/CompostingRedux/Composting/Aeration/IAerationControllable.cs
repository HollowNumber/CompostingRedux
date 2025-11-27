using Vintagestory.API.Common;

namespace CompostingRedux.Composting.Aeration
{
    /// <summary>
    /// Interface for composting systems that track oxygen/aeration levels.
    /// Proper aeration is critical for aerobic decomposition - low oxygen leads to anaerobic conditions.
    /// </summary>
    public interface IAerationControllable
    {
        /// <summary>
        /// Gets the current aeration/oxygen level (0.0 to 1.0).
        /// 0.0 = completely anaerobic (no oxygen), 1.0 = fully aerated
        /// </summary>
        float AerationLevel { get; }

        /// <summary>
        /// Gets whether aeration is in the optimal range for aerobic decomposition.
        /// </summary>
        bool HasOptimalAeration { get; }

        /// <summary>
        /// Gets whether the pile has become anaerobic (too little oxygen).
        /// </summary>
        bool IsAnaerobic { get; }

        /// <summary>
        /// Gets whether the pile is over-aerated (typically not a problem, but possible with active aeration).
        /// </summary>
        bool IsOverAerated { get; }

        /// <summary>
        /// Gets a descriptive state of the aeration level.
        /// </summary>
        string AerationState { get; }

        /// <summary>
        /// Increases aeration by turning/mixing the pile.
        /// </summary>
        /// <param name="amount">Amount to increase (0.0 to 1.0)</param>
        void Aerate(float amount);

        /// <summary>
        /// Updates aeration based on environmental factors (compaction, moisture).
        /// Should be called regularly.
        /// </summary>
        void UpdateAeration();
    }
}