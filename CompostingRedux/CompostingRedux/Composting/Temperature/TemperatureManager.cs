using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace CompostingRedux.Composting.Temperature
{
    /// <summary>
    /// Manages internal temperature in composting systems.
    /// Tracks heat generation from decomposition, heat loss to environment,
    /// and interactions with moisture, aeration, and C:N ratio.
    /// </summary>
    public class TemperatureManager
    {
        #region Fields

        private ICoreAPI? api;
        private BlockPos? blockPos;

        private float internalTemperature; // Current pile temperature in Celsius
        private double lastTemperatureUpdateTime;

        // Configuration constants
        private const float THERMOPHILIC_MIN = 40f;
        private const float THERMOPHILIC_MAX = 65f;
        private const float TOO_COLD_THRESHOLD = 20f;
        private const float TOO_HOT_THRESHOLD = 65f;
        
        private const float BASE_HEAT_GENERATION = 2f; // °C per hour from active decomposition
        private const float MAX_HEAT_GENERATION = 10f; // Maximum heat generation per hour
        private const float HEAT_LOSS_COEFFICIENT = 0.5f; // How fast heat escapes to ambient
        private const float EVAPORATIVE_COOLING = 1.5f; // Cooling from moisture evaporation
        private const float TURNING_HEAT_LOSS_FRACTION = 0.4f; // Fraction of heat above ambient lost when turning

        #endregion

        #region Properties

        /// <summary>
        /// Current internal temperature in Celsius.
        /// </summary>
        public float Temperature => internalTemperature;

        /// <summary>
        /// Ambient temperature from world climate (Celsius).
        /// </summary>
        public float AmbientTemperature { get; private set; }

        /// <summary>
        /// Temperature above ambient (how much the pile is heating itself).
        /// </summary>
        public float TemperatureAboveAmbient => Math.Max(0, internalTemperature - AmbientTemperature);

        /// <summary>
        /// Returns true if pile is in optimal thermophilic range.
        /// </summary>
        public bool IsThermophilic => internalTemperature >= THERMOPHILIC_MIN && 
                                      internalTemperature <= THERMOPHILIC_MAX;

        /// <summary>
        /// Returns true if pile is too cold for efficient decomposition.
        /// </summary>
        public bool IsTooCold => internalTemperature < TOO_COLD_THRESHOLD;

        /// <summary>
        /// Returns true if pile is too hot (killing beneficial organisms).
        /// </summary>
        public bool IsTooHot => internalTemperature > TOO_HOT_THRESHOLD;

        /// <summary>
        /// Gets a descriptive state of the temperature.
        /// </summary>
        public string State
        {
            get
            {
                if (internalTemperature < 10f) return "Cold";
                if (internalTemperature < TOO_COLD_THRESHOLD) return "Cool";
                if (internalTemperature < 30f) return "Warm";
                if (internalTemperature < THERMOPHILIC_MIN) return "Getting Hot";
                if (internalTemperature <= THERMOPHILIC_MAX) return "Thermophilic";
                if (internalTemperature <= 70f) return "Too Hot";
                return "Critically Hot";
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Creates a new TemperatureManager starting at ambient temperature.
        /// </summary>
        public TemperatureManager()
        {
            internalTemperature = 20f; // Default ambient
            AmbientTemperature = 20f;
            lastTemperatureUpdateTime = 0;
        }

        /// <summary>
        /// Sets the API reference for world access.
        /// </summary>
        public void SetApi(ICoreAPI api)
        {
            this.api = api;
        }

        /// <summary>
        /// Sets the block position for climate checks.
        /// </summary>
        public void SetBlockPos(BlockPos pos)
        {
            this.blockPos = pos;
        }

        /// <summary>
        /// Resets temperature to ambient.
        /// </summary>
        public void Reset()
        {
            internalTemperature = AmbientTemperature;
            lastTemperatureUpdateTime = 0;
        }

        #endregion

        #region Temperature Updates

        /// <summary>
        /// Updates temperature based on decomposition activity, ambient conditions, and environmental factors.
        /// </summary>
        /// <param name="currentTime">Current game time in hours</param>
        /// <param name="activityLevel">Activity level (0.0 to 1.0) - use 1.0 for active decomposition</param>
        /// <param name="moistureLevel">Current moisture level (0.0 to 1.0)</param>
        /// <param name="aerationLevel">Current aeration level (0.0 to 1.0)</param>
        /// <param name="cnRatioModifier">C:N ratio modifier (0.0 to 1.5+)</param>
        /// <param name="pileSize">Pile size as fraction of capacity (0.0 to 1.0)</param>
        public void UpdateTemperature(
            double currentTime, 
            float activityLevel, 
            float moistureLevel, 
            float aerationLevel,
            float cnRatioModifier,
            float pileSize)
        {
            if (api?.World == null || blockPos == null) return;

            // Initialize on first update
            if (lastTemperatureUpdateTime == 0)
            {
                UpdateAmbientTemperature();
                internalTemperature = AmbientTemperature;
                lastTemperatureUpdateTime = currentTime;
                return;
            }

            // Only update once per hour
            double hoursSinceLastUpdate = currentTime - lastTemperatureUpdateTime;
            if (hoursSinceLastUpdate < 1.0) return;

            lastTemperatureUpdateTime = currentTime;

            // Update ambient temperature from climate
            UpdateAmbientTemperature();

            // Calculate heat generation from active decomposition
            float heatGeneration = CalculateHeatGeneration(
                activityLevel, 
                aerationLevel, 
                cnRatioModifier, 
                pileSize);

            // Calculate heat loss
            float heatLoss = CalculateHeatLoss(moistureLevel, pileSize);

            // Apply temperature change
            float temperatureChange = (heatGeneration - heatLoss) * (float)hoursSinceLastUpdate;
            internalTemperature = GameMath.Clamp(
                internalTemperature + temperatureChange, 
                AmbientTemperature - 5f, // Can't go much below ambient
                80f); // Maximum possible temperature
        }

        /// <summary>
        /// Updates ambient temperature from weather system (includes current weather effects).
        /// </summary>
        private void UpdateAmbientTemperature()
        {
            if (api?.World == null || blockPos == null) return;

            // Use NowValues mode which includes current climate conditions
            var climate = api.World.BlockAccessor.GetClimateAt(blockPos, EnumGetClimateMode.NowValues);
            if (climate != null)
            {
                AmbientTemperature = climate.Temperature;
            }
        }

        /// <summary>
        /// Calculates heat generation from decomposition activity.
        /// </summary>
        private float CalculateHeatGeneration(
            float activityLevel, 
            float aerationLevel, 
            float cnRatioModifier,
            float pileSize)
        {
            // Base heat from decomposition activity
            float baseHeat = BASE_HEAT_GENERATION * activityLevel;

            // Aerobic decomposition generates more heat than anaerobic
            float aerationBonus = 1.0f + (aerationLevel * 0.5f); // Up to 1.5x with good aeration

            // Optimal C:N ratio generates more heat (more efficient decomposition)
            float cnBonus = cnRatioModifier;

            // Larger piles retain heat better and can get hotter
            float sizeBonus = 0.5f + (pileSize * 0.5f); // 0.5x to 1.0x based on size

            float totalHeat = baseHeat * aerationBonus * cnBonus * sizeBonus;

            return GameMath.Clamp(totalHeat, 0f, MAX_HEAT_GENERATION);
        }

        /// <summary>
        /// Calculates heat loss to environment.
        /// </summary>
        private float CalculateHeatLoss(float moistureLevel, float pileSize)
        {
            // Heat escapes to ambient
            float temperatureDifference = internalTemperature - AmbientTemperature;
            float ambientLoss = temperatureDifference * HEAT_LOSS_COEFFICIENT;

            // Smaller piles lose heat faster (less insulation)
            float sizeFactor = 1.0f - (pileSize * 0.3f); // 0.7x to 1.0x loss
            ambientLoss *= sizeFactor;

            // Evaporative cooling from moisture
            float evaporativeLoss = 0f;
            if (moistureLevel > 0.5f && internalTemperature > AmbientTemperature)
            {
                // Wet piles cool through evaporation, especially when hot
                float excessMoisture = moistureLevel - 0.5f;
                float temperatureBonus = (internalTemperature - AmbientTemperature) / 50f;
                evaporativeLoss = excessMoisture * temperatureBonus * EVAPORATIVE_COOLING;
            }

            return ambientLoss + evaporativeLoss;
        }

        #endregion

        #region Manual Temperature Control

        /// <summary>
        /// Applies cooling effect from turning the pile (releases built-up heat).
        /// Turning releases a portion of the heat above ambient temperature.
        /// </summary>
        public void ApplyTurningCooling()
        {
            // Calculate heat above ambient
            float heatAboveAmbient = internalTemperature - AmbientTemperature;
            
            // Turning releases 40% of the heat above ambient
            float heatLoss = heatAboveAmbient * TURNING_HEAT_LOSS_FRACTION;
            
            // Apply cooling but never go below ambient
            internalTemperature = GameMath.Clamp(
                internalTemperature - heatLoss,
                AmbientTemperature,
                internalTemperature);
        }

        /// <summary>
        /// Sets the internal temperature directly (clamped to valid range).
        /// </summary>
        public void SetTemperature(float temperature)
        {
            internalTemperature = GameMath.Clamp(temperature, -10f, 80f);
        }

        #endregion

        #region Decomposition Modifiers

        /// <summary>
        /// Gets the decomposition rate modifier based on current temperature.
        /// Returns a multiplier where 1.0 = normal speed.
        /// </summary>
        public float GetDecompositionModifier()
        {
            if (internalTemperature < 5f)
            {
                // Near freezing: almost no activity
                return 0.1f;
            }
            else if (internalTemperature < 10f)
            {
                // Cold: very slow
                return 0.3f;
            }
            else if (internalTemperature < TOO_COLD_THRESHOLD)
            {
                // Cool: reduced activity
                return 0.6f;
            }
            else if (internalTemperature < 30f)
            {
                // Warm: good activity
                return 0.9f;
            }
            else if (internalTemperature < THERMOPHILIC_MIN)
            {
                // Getting hot: very good
                return 1.1f;
            }
            else if (internalTemperature <= 55f)
            {
                // Optimal thermophilic range: maximum activity
                return 1.5f;
            }
            else if (internalTemperature <= THERMOPHILIC_MAX)
            {
                // High thermophilic: still good but approaching limits
                return 1.3f;
            }
            else if (internalTemperature <= 70f)
            {
                // Too hot: killing beneficial organisms
                return 0.7f;
            }
            else
            {
                // Critically hot: most organisms dead
                return 0.3f;
            }
        }

        /// <summary>
        /// Gets the moisture evaporation rate based on temperature.
        /// Higher temperature = faster evaporation.
        /// </summary>
        public float GetEvaporationMultiplier()
        {
            if (internalTemperature <= AmbientTemperature)
            {
                return 1.0f; // Normal evaporation
            }

            // Temperature above ambient increases evaporation
            float tempAboveAmbient = internalTemperature - AmbientTemperature;
            return 1.0f + (tempAboveAmbient / 50f); // Up to 2x at 50°C above ambient
        }

        #endregion

        #region Serialization

        /// <summary>
        /// Saves temperature state to tree attributes.
        /// </summary>
        public void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetFloat("internalTemperature", internalTemperature);
            tree.SetFloat("ambientTemperature", AmbientTemperature);
            tree.SetDouble("lastTemperatureUpdateTime", lastTemperatureUpdateTime);
        }

        /// <summary>
        /// Loads temperature state from tree attributes.
        /// </summary>
        public void FromTreeAttributes(ITreeAttribute tree)
        {
            internalTemperature = tree.GetFloat("internalTemperature", 20f);
            AmbientTemperature = tree.GetFloat("ambientTemperature", 20f);
            lastTemperatureUpdateTime = tree.GetDouble("lastTemperatureUpdateTime", 0);
        }

        #endregion
    }
}