using Vintagestory.API.Common;

namespace CompostingRedux.Composting.Temperature
{
    /// <summary>
    /// Interface for composting systems that track internal temperature and heat generation.
    /// Temperature is critical for decomposition rate - optimal thermophilic composting occurs at 40-65°C.
    /// </summary>
    public interface ITemperatureControllable
    {
        /// <summary>
        /// Gets the current internal temperature in Celsius.
        /// </summary>
        float Temperature { get; }

        /// <summary>
        /// Gets the ambient/external temperature in Celsius.
        /// </summary>
        float AmbientTemperature { get; }

        /// <summary>
        /// Gets the temperature above ambient (how much heat the pile is generating).
        /// </summary>
        float TemperatureAboveAmbient { get; }

        /// <summary>
        /// Gets whether the pile is in the optimal thermophilic range (40-65°C).
        /// </summary>
        bool IsThermophilic { get; }

        /// <summary>
        /// Gets whether the pile is too cold for efficient decomposition (< 20°C).
        /// </summary>
        bool IsTooCold { get; }

        /// <summary>
        /// Gets whether the pile is too hot (> 65°C, killing beneficial organisms).
        /// </summary>
        bool IsTooHot { get; }

        /// <summary>
        /// Gets a descriptive state of the temperature.
        /// </summary>
        string TemperatureState { get; }

        /// <summary>
        /// Updates temperature based on decomposition activity, ambient conditions, and pile characteristics.
        /// Should be called regularly.
        /// </summary>
        void UpdateTemperature();
    }
}