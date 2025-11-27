namespace CompostingRedux.Composting.Interfaces
{
    /// <summary>
    /// Interface for composting systems that track carbon-to-nitrogen ratio (C:N ratio).
    /// The C:N ratio is critical for efficient composting - optimal range is typically 25-30:1.
    /// </summary>
    public interface ICarbonNitrogenRatio
    {
        /// <summary>
        /// Gets the number of green (nitrogen-rich) materials in the pile.
        /// </summary>
        int GreenMaterialCount { get; }

        /// <summary>
        /// Gets the number of brown (carbon-rich) materials in the pile.
        /// </summary>
        int BrownMaterialCount { get; }

        /// <summary>
        /// Gets the current carbon-to-nitrogen ratio.
        /// Calculated as weighted average of material C:N ratios.
        /// </summary>
        float CarbonNitrogenRatio { get; }

        /// <summary>
        /// Gets a text description of the ratio quality (e.g., "Excellent", "Good", "Poor").
        /// </summary>
        string RatioQualityText { get; }

        /// <summary>
        /// Gets the decomposition speed multiplier based on C:N ratio.
        /// Returns 1.0 for optimal ratio, less for poor ratios.
        /// </summary>
        float GetCNRatioModifier();
    }
}