using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace CompostingRedux.Composting.Moisture
{
    /// <summary>
    /// Manages moisture levels for composting systems, including atmospheric updates
    /// (rain, evaporation) and manual watering. This class is reusable across different
    /// composting block entities.
    /// </summary>
    public class MoistureManager
    {
        #region Fields

        private ICoreAPI? api;
        private BlockPos? blockPos;

        private float moistureLevel; // 0.0 (bone dry) to 1.0 (waterlogged)
        private double lastMoistureCheckTime;

        // Configuration constants
        private const float OPTIMAL_MIN = 0.4f;
        private const float OPTIMAL_MAX = 0.6f;
        private const float TOO_DRY_THRESHOLD = 0.3f;
        private const float TOO_WET_THRESHOLD = 0.7f;
        private const float BONE_DRY_THRESHOLD = 0.2f;
        private const float WATERLOGGED_THRESHOLD = 0.85f;

        private const float BASE_EVAPORATION_RATE = 0.02f; // Per hour
        private const float MAX_RAIN_GAIN_PER_HOUR = 0.1f;
        private const float TEMPERATURE_EVAPORATION_FACTOR = 0.001f;
        private const float RAIN_EVAPORATION_REDUCTION = 0.1f;

        #endregion

        #region Properties

        /// <summary>
        /// Current moisture level (0.0 to 1.0).
        /// </summary>
        public float Level => moistureLevel;

        /// <summary>
        /// Returns true if moisture is in the optimal range.
        /// </summary>
        public bool IsOptimal => moistureLevel >= OPTIMAL_MIN && moistureLevel <= OPTIMAL_MAX;

        /// <summary>
        /// Returns true if pile is too dry.
        /// </summary>
        public bool IsTooDry => moistureLevel < TOO_DRY_THRESHOLD;

        /// <summary>
        /// Returns true if pile is too wet.
        /// </summary>
        public bool IsTooWet => moistureLevel > TOO_WET_THRESHOLD;

        /// <summary>
        /// Returns true if pile is bone dry (critical).
        /// </summary>
        public bool IsBoneDry => moistureLevel < BONE_DRY_THRESHOLD;

        /// <summary>
        /// Returns true if pile is waterlogged (critical).
        /// </summary>
        public bool IsWaterlogged => moistureLevel > WATERLOGGED_THRESHOLD;

        /// <summary>
        /// Gets a descriptive moisture state for display.
        /// </summary>
        public string State
        {
            get
            {
                if (moistureLevel < BONE_DRY_THRESHOLD) return "Bone Dry";
                if (moistureLevel < TOO_DRY_THRESHOLD) return "Too Dry";
                if (moistureLevel < OPTIMAL_MIN) return "Slightly Dry";
                if (moistureLevel <= OPTIMAL_MAX) return "Optimal";
                if (moistureLevel <= TOO_WET_THRESHOLD) return "Slightly Wet";
                if (moistureLevel <= WATERLOGGED_THRESHOLD) return "Too Wet";
                return "Waterlogged";
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Creates a new MoistureManager with default optimal moisture.
        /// </summary>
        public MoistureManager()
        {
            moistureLevel = 0.5f; // Start at optimal
            lastMoistureCheckTime = 0;
        }

        /// <summary>
        /// Sets the API reference for world access.
        /// </summary>
        public void SetApi(ICoreAPI api)
        {
            this.api = api;
        }

        /// <summary>
        /// Sets the block position for rain detection and climate checks.
        /// </summary>
        public void SetBlockPos(BlockPos pos)
        {
            this.blockPos = pos;
        }

        /// <summary>
        /// Resets moisture to optimal level.
        /// </summary>
        public void Reset()
        {
            moistureLevel = 0.5f;
            lastMoistureCheckTime = 0;
        }

        #endregion

        #region Environmental Updates

        /// <summary>
        /// Updates moisture level based on environmental conditions (rain and evaporation).
        /// Should be called regularly (e.g., from Update() method).
        /// </summary>
        /// <param name="currentTime">Current game time in hours</param>
        /// <param name="temperatureEvaporationMultiplier">Optional multiplier from pile temperature (default 1.0)</param>
        public void UpdateEnvironmental(double currentTime, float temperatureEvaporationMultiplier = 1.0f)
        {
            if (blockPos == null || api?.World == null) return;

            // Only update moisture once per hour to reduce performance cost
            double hoursSinceLastCheck = currentTime - lastMoistureCheckTime;
            if (hoursSinceLastCheck < 1.0) return;

            lastMoistureCheckTime = currentTime;

            // Check if exposed to rain
            bool isRaining = api.World.BlockAccessor.GetRainMapHeightAt(blockPos.X, blockPos.Z) <= blockPos.Y;

            // Get climate data for temperature and rainfall
            var climate = api.World.BlockAccessor.GetClimateAt(blockPos, EnumGetClimateMode.NowValues);
            if (climate == null) return;

            float temperature = climate.Temperature;
            float rainfall = climate.Rainfall;

            // Apply rain gain
            if (isRaining && rainfall > 0)
            {
                float rainGain = rainfall * MAX_RAIN_GAIN_PER_HOUR;
                moistureLevel = GameMath.Clamp(moistureLevel + rainGain, 0f, 1f);
            }

            // Apply evaporation loss
            float evaporationRate = CalculateEvaporationRate(temperature, isRaining, temperatureEvaporationMultiplier);
            moistureLevel = GameMath.Clamp(moistureLevel - evaporationRate, 0f, 1f);
        }

        /// <summary>
        /// Calculates the evaporation rate based on temperature and weather.
        /// </summary>
        /// <param name="temperature">Ambient temperature</param>
        /// <param name="isRaining">Whether it's currently raining</param>
        /// <param name="temperatureMultiplier">Multiplier from pile internal temperature</param>
        private float CalculateEvaporationRate(float temperature, bool isRaining, float temperatureMultiplier)
        {
            float evaporationRate = BASE_EVAPORATION_RATE;

            // Increase evaporation in warm weather
            if (temperature > 0)
            {
                evaporationRate += temperature * TEMPERATURE_EVAPORATION_FACTOR;
            }

            // Apply pile temperature multiplier (hot piles evaporate faster)
            evaporationRate *= temperatureMultiplier;

            // Reduce evaporation during rain
            if (isRaining)
            {
                evaporationRate *= RAIN_EVAPORATION_REDUCTION;
            }

            return evaporationRate;
        }

        #endregion

        #region Manual Moisture Control

        /// <summary>
        /// Manually adds water to increase moisture level.
        /// </summary>
        /// <param name="amount">Amount to add (0.0 to 1.0)</param>
        public void AddWater(float amount)
        {
            moistureLevel = GameMath.Clamp(moistureLevel + amount, 0f, 1f);
        }

        /// <summary>
        /// Manually adds dry material to decrease moisture level.
        /// </summary>
        /// <param name="amount">Amount to remove (0.0 to 1.0)</param>
        public void AddDryMaterial(float amount)
        {
            moistureLevel = GameMath.Clamp(moistureLevel - amount, 0f, 1f);
        }

        /// <summary>
        /// Sets the moisture level directly (clamped to valid range).
        /// </summary>
        public void SetLevel(float level)
        {
            moistureLevel = GameMath.Clamp(level, 0f, 1f);
        }

        #endregion

        #region Decomposition Modifiers

        /// <summary>
        /// Gets the decomposition rate modifier based on current moisture level.
        /// Returns a multiplier where 1.0 = normal speed.
        /// </summary>
        public float GetDecompositionModifier()
        {
            if (moistureLevel < BONE_DRY_THRESHOLD)
            {
                // Bone dry: severe penalty (microbes can't function)
                return 0.1f;
            }
            else if (moistureLevel < TOO_DRY_THRESHOLD)
            {
                // Too dry: heavy penalty
                return 0.5f;
            }
            else if (moistureLevel < OPTIMAL_MIN)
            {
                // Slightly dry: minor penalty
                return 0.8f;
            }
            else if (moistureLevel <= OPTIMAL_MAX)
            {
                // Optimal: full speed
                return 1.0f;
            }
            else if (moistureLevel <= TOO_WET_THRESHOLD)
            {
                // Slightly wet: minor penalty
                return 0.8f;
            }
            else if (moistureLevel <= WATERLOGGED_THRESHOLD)
            {
                // Too wet: heavy penalty (anaerobic conditions)
                return 0.4f;
            }
            else
            {
                // Waterlogged: severe penalty (anaerobic, may putrefy)
                return 0.2f;
            }
        }

        #endregion

        #region Serialization

        /// <summary>
        /// Saves moisture state to tree attributes.
        /// </summary>
        public void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetFloat("moistureLevel", moistureLevel);
            tree.SetDouble("lastMoistureCheckTime", lastMoistureCheckTime);
        }

        /// <summary>
        /// Loads moisture state from tree attributes.
        /// </summary>
        public void FromTreeAttributes(ITreeAttribute tree)
        {
            moistureLevel = tree.GetFloat("moistureLevel", 0.5f); // Default to optimal
            lastMoistureCheckTime = tree.GetDouble("lastMoistureCheckTime", 0);
        }

        #endregion
    }
}