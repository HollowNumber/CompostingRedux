using System;
using System.Linq;
using CompostingRedux.Composting.Aeration;
using CompostingRedux.Composting.Interfaces;
using CompostingRedux.Composting.Moisture;
using CompostingRedux.Composting.Temperature;
using CompostingRedux.Configuration;
using CompostingRedux.Helpers;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace CompostingRedux.Composting
{
    /// <summary>
    /// Handles all composting calculations including decomposition rates, C:N ratios,
    /// and environmental factors. This class is updated on tick and provides
    /// all the logic for realistic composting simulation.
    /// </summary>
    public class CompostProcessor : IProgressTracker, ITurnable, ICarbonNitrogenRatio, ICapacityContainer, IAerationControllable, ITemperatureControllable
    {
        #region Fields

        private ICoreAPI api;
        private CompostConfig config;
        private CompostBinInventory inventory;
        private MoistureManager moistureManager;
        private AerationManager aerationManager;
        private TemperatureManager temperatureManager;

        // Time tracking
        private double startTime;
        private double lastUpdateTime;
        private double lastTurnTime;

        // State tracking
        private float decompositionProgress; // 0.0 - 1.0
        private bool isFinished;

        #endregion

        #region Properties

        /// <summary>
        /// Current number of green (nitrogen-rich) materials in the pile.
        /// Calculated dynamically from inventory contents.
        /// </summary>
        public int GreenMaterialCount
        {
            get
            {
                if (inventory == null) return 0;
                int count = 0;
                foreach (var slot in inventory.GetFilledSlots())
                {
                    if (slot?.Itemstack?.Collectible == null) continue;
                    string itemPath = slot.Itemstack.Collectible.Code.Path;
                    if (config.GetMaterialType(itemPath) == MaterialType.Green)
                    {
                        count += slot.StackSize;
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// Current number of brown (carbon-rich) materials in the pile.
        /// Calculated dynamically from inventory contents.
        /// </summary>
        public int BrownMaterialCount
        {
            get
            {
                if (inventory == null) return 0;
                int count = 0;
                foreach (var slot in inventory.GetFilledSlots())
                {
                    if (slot?.Itemstack?.Collectible == null) continue;
                    string itemPath = slot.Itemstack.Collectible.Code.Path;
                    if (config.GetMaterialType(itemPath) == MaterialType.Brown)
                    {
                        count += slot.StackSize;
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// Total number of items in the compost pile.
        /// Calculated dynamically from inventory contents.
        /// </summary>
        public int TotalItemCount => inventory?.TotalItemCount ?? 0;

        /// <summary>
        /// ICapacityContainer implementation - current count.
        /// </summary>
        public int CurrentCount => TotalItemCount;

        /// <summary>
        /// ICapacityContainer implementation - maximum capacity.
        /// </summary>
        public int MaxCapacity => config.MaxCapacity;

        /// <summary>
        /// ICapacityContainer implementation - remaining capacity.
        /// </summary>
        public int RemainingCapacity => MaxCapacity - CurrentCount;

        /// <summary>
        /// ICapacityContainer implementation - is full.
        /// </summary>
        public bool IsFull => CurrentCount >= MaxCapacity;

        /// <summary>
        /// ICapacityContainer implementation - is empty.
        /// </summary>
        public bool IsEmpty => CurrentCount == 0;

        /// <summary>
        /// ICapacityContainer implementation - fill ratio.
        /// </summary>
        public float FillRatio => MaxCapacity > 0 ? (float)CurrentCount / MaxCapacity : 0f;

        /// <summary>
        /// Current carbon to nitrogen ratio of the compost pile.
        /// Optimal is around 25-30:1
        /// Calculated as weighted average of material C:N ratios.
        /// </summary>
        public float CarbonNitrogenRatio
        {
            get
            {
                int greens = GreenMaterialCount;
                int browns = BrownMaterialCount;
                int total = greens + browns;

                if (total == 0) return 0f;
                if (greens == 0) return config.BrownCNRatio; // All browns
                if (browns == 0) return config.GreenCNRatio; // All greens

                // Calculate weighted average C:N ratio
                // Each material contributes to the overall ratio based on its proportion
                float weightedSum = (greens * config.GreenCNRatio) + (browns * config.BrownCNRatio);
                return weightedSum / total;
            }
        }

        /// <summary>
        /// Current decomposition progress as a percentage (0-100).
        /// </summary>
        public int DecompositionPercent => (int)GameMath.Clamp(decompositionProgress * 100f, 0f, 100f);

        /// <summary>
        /// IProgressTracker implementation - delegates to DecompositionPercent.
        /// </summary>
        public int ProgressPercent => DecompositionPercent;



        /// <summary>
        /// Returns true if composting is complete.
        /// </summary>
        public bool IsFinished => isFinished;

        /// <summary>
        /// Number of in-game hours that have elapsed since composting started.
        /// </summary>
        public int ElapsedHours
        {
            get
            {
                if (startTime == 0) return 0;
                return (int)(api.World.Calendar.TotalHours - startTime);
            }
        }

        /// <summary>
        /// Estimated number of in-game hours remaining until completion.
        /// Takes into account current decomposition rate modifiers.
        /// </summary>
        public int RemainingHours
        {
            get
            {
                if (TotalItemCount == 0) return 0;
                if (isFinished) return 0;

                float remainingProgress = 1.0f - decompositionProgress;
                float currentRate = CalculateDecompositionRate();

                if (currentRate <= 0) return 9999; // Effectively never

                float hoursRemaining = remainingProgress / currentRate;
                return (int)GameMath.Max(0f, hoursRemaining);
            }
        }

        /// <summary>
        /// Current moisture level of the compost pile (0.0 to 1.0).
        /// 0.0 = bone dry, 0.5 = optimal, 1.0 = waterlogged
        /// </summary>
        public float MoistureLevel => moistureManager.Level;

        /// <summary>
        /// Returns true if moisture is in the optimal range (0.4 - 0.6).
        /// </summary>
        public bool HasOptimalMoisture => moistureManager.IsOptimal;

        /// <summary>
        /// Returns true if pile is too dry (< 0.3).
        /// </summary>
        public bool IsTooDry => moistureManager.IsTooDry;

        /// <summary>
        /// Returns true if pile is too wet (> 0.7).
        /// </summary>
        public bool IsTooWet => moistureManager.IsTooWet;

        /// <summary>
        /// Gets a descriptive moisture state for display.
        /// </summary>
        public string MoistureState => moistureManager.State;

        /// <summary>
        /// Gets the aeration manager instance.
        /// </summary>
        public AerationManager AerationManager => aerationManager;

        /// <summary>
        /// Current aeration level of the compost pile (0.0 to 1.0).
        /// </summary>
        public float AerationLevel => aerationManager.Level;

        /// <summary>
        /// Returns true if aeration is optimal.
        /// </summary>
        public bool HasOptimalAeration => aerationManager.IsOptimal;

        /// <summary>
        /// Returns true if pile is anaerobic (no oxygen).
        /// </summary>
        public bool IsAnaerobic => aerationManager.IsAnaerobic;

        /// <summary>
        /// Returns true if pile is over-aerated.
        /// </summary>
        public bool IsOverAerated => aerationManager.IsOverAerated;

        /// <summary>
        /// Gets a descriptive aeration state for display.
        /// </summary>
        public string AerationState => aerationManager.State;

        /// <summary>
        /// Gets the temperature manager instance.
        /// </summary>
        public TemperatureManager TemperatureManager => temperatureManager;

        /// <summary>
        /// Current internal temperature (Celsius).
        /// </summary>
        public float Temperature => temperatureManager.Temperature;

        /// <summary>
        /// Ambient/external temperature (Celsius).
        /// </summary>
        public float AmbientTemperature => temperatureManager.AmbientTemperature;

        /// <summary>
        /// Temperature above ambient (heat generation).
        /// </summary>
        public float TemperatureAboveAmbient => temperatureManager.TemperatureAboveAmbient;

        /// <summary>
        /// Returns true if pile is in thermophilic range.
        /// </summary>
        public bool IsThermophilic => temperatureManager.IsThermophilic;

        /// <summary>
        /// Returns true if pile is too cold.
        /// </summary>
        public bool IsTooCold => temperatureManager.IsTooCold;

        /// <summary>
        /// Returns true if pile is too hot.
        /// </summary>
        public bool IsTooHot => temperatureManager.IsTooHot;

        /// <summary>
        /// Gets a descriptive temperature state for display.
        /// </summary>
        public string TemperatureState => temperatureManager.State;

        #endregion

        #region Initialization

        /// <summary>
        /// Creates a new CompostProcessor instance.
        /// </summary>
        public CompostProcessor(ICoreAPI api = null, CompostBinInventory inventory = null)
        {
            this.api = api;
            this.inventory = inventory;
            this.config = CompostingReduxModSystem.Config;
            this.moistureManager = new MoistureManager();
            this.aerationManager = new AerationManager();
            this.temperatureManager = new TemperatureManager();

            Reset();
        }

        /// <summary>
        /// Sets the API reference. Used when the processor is created before the API is available.
        /// </summary>
        public void SetApi(ICoreAPI api)
        {
            this.api = api;
            moistureManager.SetApi(api);
            aerationManager.SetApi(api);
            temperatureManager.SetApi(api);
        }

        /// <summary>
        /// Sets the inventory reference. Used when the inventory is created after the processor.
        /// </summary>
        public void SetInventory(CompostBinInventory inventory)
        {
            this.inventory = inventory;
        }

        /// <summary>
        /// Sets the block position. Used for environmental checks (rain, climate, temperature).
        /// </summary>
        public void SetBlockPos(BlockPos pos)
        {
            moistureManager.SetBlockPos(pos);
            temperatureManager.SetBlockPos(pos);
        }

        /// <summary>
        /// Resets the processor to an empty state.
        /// </summary>
        public void Reset()
        {
            startTime = 0;
            lastUpdateTime = 0;
            lastTurnTime = 0;
            decompositionProgress = 0f;
            isFinished = false;
            moistureManager.Reset();
            aerationManager.Reset();
            temperatureManager.Reset();
        }

        #endregion

        #region Material Management

        /// <summary>
        /// Called when the first material is added to start the composting timer.
        /// </summary>
        public void StartComposting()
        {
            if (api != null && TotalItemCount > 0 && startTime == 0)
            {
                startTime = api.World.Calendar.TotalHours;
                lastUpdateTime = startTime;
                lastTurnTime = startTime;
            }
        }

        /// <summary>
        /// Removes all materials from the compost pile. Called when harvesting.
        /// </summary>
        public void Harvest()
        {
            inventory?.ClearAll();
            Reset();
        }

        #endregion

        #region Decomposition Calculation

        /// <summary>
        /// Updates the decomposition progress based on elapsed time.
        /// Should be called periodically (e.g., every tick).
        /// </summary>
        public void Update()
        {
            if (api == null || TotalItemCount == 0 || isFinished) return;

            double currentTime = api.World.Calendar.TotalHours;
            double hoursSinceLastUpdate = currentTime - lastUpdateTime;

            if (hoursSinceLastUpdate <= 0) return;

            // Update environmental factors
            // Note: We use the current temperature's evaporation multiplier (from last update)
            // to affect moisture, creating a feedback loop where hot piles dry faster
            float tempEvaporationMultiplier = temperatureManager.GetEvaporationMultiplier();
            moistureManager.UpdateEnvironmental(currentTime, tempEvaporationMultiplier);
            aerationManager.UpdateAeration(currentTime, moistureManager.Level);

            // Update temperature with activity level (not decomposition rate which is too small)
            // Activity level = 1.0 when pile is active, scaled down for small piles
            float pileSize = (float)TotalItemCount / MaxCapacity;
            float activityLevel = 1.0f;
            if (pileSize < 0.3f)
            {
                // Small piles are less active (need critical mass for heat buildup)
                activityLevel = pileSize / 0.3f;
            }
            
            temperatureManager.UpdateTemperature(
                currentTime,
                activityLevel,
                moistureManager.Level,
                aerationManager.Level,
                CalculateCNRatioModifier(),
                pileSize);

            // Calculate decomposition rate per hour
            float ratePerHour = CalculateDecompositionRate();

            // Update progress
            float progressGain = ratePerHour * (float)hoursSinceLastUpdate;
            decompositionProgress += progressGain;

            // Check if finished
            if (decompositionProgress >= 1.0f)
            {
                decompositionProgress = 1.0f;
                isFinished = true;
            }

            lastUpdateTime = currentTime;
        }

        /// <summary>
        /// Calculates the current decomposition rate per hour.
        /// Returns a value where 1.0 = complete in HoursToComplete hours.
        /// </summary>
        private float CalculateDecompositionRate()
        {
            // Base rate: progress of 1.0 over HoursToComplete hours
            float baseRate = 1.0f / config.HoursToComplete;

            // Apply C:N ratio modifier
            float cnModifier = CalculateCNRatioModifier();

            // Apply moisture modifier
            float moistureModifier = moistureManager.GetDecompositionModifier();

            // Apply aeration modifier
            float aerationModifier = aerationManager.GetDecompositionModifier();

            // Apply temperature modifier
            float temperatureModifier = temperatureManager.GetDecompositionModifier();

            return baseRate * cnModifier * moistureModifier * aerationModifier * temperatureModifier;
        }

        /// <summary>
        /// Gets the decomposition rate modifier based on C:N ratio.
        /// Optimal ratio gives bonus, poor ratio gives penalty.
        /// </summary>
        private float CalculateCNRatioModifier()
        {
            float ratio = CarbonNitrogenRatio;

            // If no materials or invalid ratio, return neutral
            if (ratio <= 0 || ratio > 100) return 1.0f;

            // Calculate distance from optimal ratio
            float optimalRatio = config.OptimalCNRatio;
            float distance = Math.Abs(ratio - optimalRatio);

            // Within 5 of optimal: excellent bonus
            if (distance <= 5f)
            {
                return config.OptimalRatioBonus;
            }
            // Within 10 of optimal: good bonus
            else if (distance <= 10f)
            {
                return 1.2f;
            }
            // Within 15 of optimal: neutral
            else if (distance <= 15f)
            {
                return 1.0f;
            }
            // Within 25 of optimal: slight penalty
            else if (distance <= 25f)
            {
                return 0.8f;
            }
            // Poor ratio: heavy penalty
            else
            {
                return config.PoorRatioPenalty;
            }
        }

        #endregion

        #region Pile Turning

        /// <summary>
        /// Turns the compost pile, speeding up decomposition. (ITurnable implementation)
        /// </summary>
        /// <param name="speedupHours">Number of hours to speed up the process</param>
        public void Turn(int speedupHours)
        {
            TurnPile(speedupHours);

            // Turning also increases aeration
            aerationManager.Turn();

            // Turning helps reduce excess moisture through increased evaporation
            if (moistureManager.IsTooWet)
            {
                // If too wet, turning helps dry it out more
                moistureManager.AddDryMaterial(0.1f);
            }
            else if (moistureManager.Level > 0.5f)
            {
                // Even if not too wet, turning increases evaporation slightly
                moistureManager.AddDryMaterial(0.05f);
            }

            // Turning also releases built-up heat
            temperatureManager.ApplyTurningCooling();
        }

        /// <summary>
        /// Turns the compost pile, speeding up decomposition.
        /// </summary>
        /// <param name="speedupHours">Number of hours to speed up the process</param>
        public void TurnPile(int speedupHours)
        {
            if (TotalItemCount == 0 || isFinished) return;

            // Calculate how much progress the speedup represents
            float progressBoost = speedupHours / (float)config.HoursToComplete;
            decompositionProgress += progressBoost;

            // Check if this turn completed the composting
            if (decompositionProgress >= 1.0f)
            {
                decompositionProgress = 1.0f;
                isFinished = true;
            }

            lastTurnTime = api.World.Calendar.TotalHours;

            // Turning increases aeration
            aerationManager.Turn();

            // Turning helps reduce excess moisture through increased evaporation
            if (moistureManager.IsTooWet)
            {
                // If too wet, turning helps dry it out more
                moistureManager.AddDryMaterial(0.1f);
            }
            else if (moistureManager.Level > 0.5f)
            {
                // Even if not too wet, turning increases evaporation slightly
                moistureManager.AddDryMaterial(0.05f);
            }

            // Turning also releases built-up heat
            temperatureManager.ApplyTurningCooling();
        }

        /// <summary>
        /// ITurnable implementation - returns whether pile can be turned.
        /// </summary>
        public bool CanTurn => CanTurnInternal(config.ShovelTurnCooldownHours);

        /// <summary>
        /// ITurnable implementation - returns hours until can turn again.
        /// </summary>
        public double TurnCooldownRemaining => GetTurnCooldownRemaining(config.ShovelTurnCooldownHours);

        /// <summary>
        /// Returns true if enough time has passed since the last turn.
        /// </summary>
        public bool CanTurnInternal(int cooldownHours)
        {
            if (TotalItemCount == 0 || isFinished) return false;

            double currentTime = api.World.Calendar.TotalHours;
            double timeSinceLastTurn = currentTime - lastTurnTime;

            return timeSinceLastTurn >= cooldownHours;
        }

        /// <summary>
        /// Gets the hours remaining until the pile can be turned again.
        /// </summary>
        public double GetTurnCooldownRemaining(int cooldownHours)
        {
            double currentTime = api.World.Calendar.TotalHours;
            double timeSinceLastTurn = currentTime - lastTurnTime;
            return GameMath.Max(0, cooldownHours - timeSinceLastTurn);
        }

        #endregion

        #region Quality Assessment

        /// <summary>
        /// Gets the C:N ratio modifier. (ICarbonNitrogenRatio implementation)
        /// </summary>
        /// <returns>Decomposition speed multiplier based on C:N ratio</returns>
        public float GetCNRatioModifier()
        {
            return CalculateCNRatioModifier();
        }

        /// <summary>
        /// ICarbonNitrogenRatio implementation - gets ratio quality text.
        /// </summary>
        public string RatioQualityText => GetRatioQualityDescription();

        /// <summary>
        /// Gets a text description of the C:N ratio quality.
        /// </summary>
        public string GetRatioQualityDescription()
        {
            float ratio = CarbonNitrogenRatio;
            if (ratio <= 0 || ratio > 100) return "";

            float distance = Math.Abs(ratio - config.OptimalCNRatio);

            if (distance <= 5f) return "(Excellent!)";
            if (distance <= 10f) return "(Good)";
            if (distance <= 15f) return "(Ok)";
            if (distance <= 25f) return "(Poor)";
            return "(Very Poor)";
        }

        /// <summary>
        /// Gets the current decomposition speed multiplier as a displayable value.
        /// </summary>
        public float GetSpeedMultiplier()
        {
            return GetCNRatioModifier();
        }

        #endregion

        #region Serialization

        /// <summary>
        /// Saves the processor state to tree attributes.
        /// </summary>
        public void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetDouble("startTime", startTime);
            tree.SetDouble("lastUpdateTime", lastUpdateTime);
            tree.SetDouble("lastTurnTime", lastTurnTime);
            tree.SetFloat("decompositionProgress", decompositionProgress);
            tree.SetBool("isFinished", isFinished);

            // Save manager states
            moistureManager.ToTreeAttributes(tree);
            aerationManager.ToTreeAttributes(tree);
            temperatureManager.ToTreeAttributes(tree);
        }

        /// <summary>
        /// Loads the processor state from tree attributes.
        /// </summary>
        public void FromTreeAttributes(ITreeAttribute tree)
        {
            startTime = tree.GetDouble("startTime");
            lastUpdateTime = tree.GetDouble("lastUpdateTime");
            lastTurnTime = tree.GetDouble("lastTurnTime");
            decompositionProgress = tree.GetFloat("decompositionProgress");
            isFinished = tree.GetBool("isFinished");

            // Load manager states
            moistureManager.FromTreeAttributes(tree);
            aerationManager.FromTreeAttributes(tree);
            temperatureManager.FromTreeAttributes(tree);
        }

        #endregion

        #region Moisture Management

        /// <summary>
        /// Manually adds water to the compost pile (e.g., from a bucket).
        /// </summary>
        /// <param name="amount">Amount of moisture to add (0.0 to 1.0)</param>
        public void AddWater(float amount = 0.2f)
        {
            moistureManager.AddWater(amount);
        }

        /// <summary>
        /// Manually adds dry material to reduce moisture (e.g., adding browns when too wet).
        /// </summary>
        /// <param name="amount">Amount of moisture to remove (0.0 to 1.0)</param>
        public void AddDryMaterial(float amount = 0.15f)
        {
            moistureManager.AddDryMaterial(amount);
        }

        /// <summary>
        /// IAerationControllable implementation - increases aeration.
        /// </summary>
        public void Aerate(float amount)
        {
            aerationManager.Aerate(amount);
        }

        /// <summary>
        /// IAerationControllable implementation - updates aeration.
        /// </summary>
        public void UpdateAeration()
        {
            if (api?.World == null) return;
            double currentTime = api.World.Calendar.TotalHours;
            aerationManager.UpdateAeration(currentTime, moistureManager.Level);
        }

        /// <summary>
        /// ITemperatureControllable implementation - updates temperature.
        /// </summary>
        public void UpdateTemperature()
        {
            if (api?.World == null) return;
            double currentTime = api.World.Calendar.TotalHours;
            float currentDecompRate = CalculateDecompositionRate();
            float pileSize = (float)TotalItemCount / MaxCapacity;
            temperatureManager.UpdateTemperature(
                currentTime,
                currentDecompRate,
                moistureManager.Level,
                aerationManager.Level,
                CalculateCNRatioModifier(),
                pileSize);
        }

        #endregion
    }
}
