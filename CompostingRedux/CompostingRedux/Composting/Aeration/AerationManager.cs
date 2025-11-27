using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace CompostingRedux.Composting.Aeration
{
    /// <summary>
    /// Manages aeration/oxygen levels in composting systems.
    /// Tracks oxygen availability, compaction, and the effects of turning.
    /// Proper aeration is essential for efficient aerobic decomposition.
    /// </summary>
    public class AerationManager
    {
        #region Fields

        private ICoreAPI? api;
        private float aerationLevel; // 0.0 (anaerobic) to 1.0 (fully aerated)
        private double lastAerationUpdateTime;
        private double lastTurnTime;

        // Configuration constants
        private const float OPTIMAL_MIN = 0.5f;
        private const float OPTIMAL_MAX = 0.9f;
        private const float ANAEROBIC_THRESHOLD = 0.3f;
        private const float OVER_AERATED_THRESHOLD = 0.95f;
        
        private const float BASE_COMPACTION_RATE = 0.01f; // Aeration loss per hour from settling
        private const float TURN_AERATION_BOOST = 0.4f; // How much turning increases aeration
        private const float MOISTURE_AERATION_FACTOR = 0.5f; // How much moisture reduces aeration

        #endregion

        #region Properties

        /// <summary>
        /// Current aeration/oxygen level (0.0 to 1.0).
        /// </summary>
        public float Level => aerationLevel;

        /// <summary>
        /// Returns true if aeration is in the optimal range for aerobic decomposition.
        /// </summary>
        public bool IsOptimal => aerationLevel >= OPTIMAL_MIN && aerationLevel <= OPTIMAL_MAX;

        /// <summary>
        /// Returns true if the pile has become anaerobic (insufficient oxygen).
        /// </summary>
        public bool IsAnaerobic => aerationLevel < ANAEROBIC_THRESHOLD;

        /// <summary>
        /// Returns true if the pile is over-aerated (rare, usually from active forced air).
        /// </summary>
        public bool IsOverAerated => aerationLevel > OVER_AERATED_THRESHOLD;

        /// <summary>
        /// Gets a descriptive state of the aeration level.
        /// </summary>
        public string State
        {
            get
            {
                if (aerationLevel < 0.2f) return "Completely Anaerobic";
                if (aerationLevel < ANAEROBIC_THRESHOLD) return "Anaerobic";
                if (aerationLevel < OPTIMAL_MIN) return "Low Oxygen";
                if (aerationLevel <= OPTIMAL_MAX) return "Well Aerated";
                if (aerationLevel <= OVER_AERATED_THRESHOLD) return "Highly Aerated";
                return "Over Aerated";
            }
        }

        /// <summary>
        /// Gets the hours since the pile was last turned.
        /// </summary>
        public double HoursSinceLastTurn
        {
            get
            {
                if (api?.World == null || lastTurnTime == 0) return 0;
                return api.World.Calendar.TotalHours - lastTurnTime;
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Creates a new AerationManager with default well-aerated state.
        /// </summary>
        public AerationManager()
        {
            aerationLevel = 0.7f; // Start well-aerated (freshly added material)
            lastAerationUpdateTime = 0;
            lastTurnTime = 0;
        }

        /// <summary>
        /// Sets the API reference for world access.
        /// </summary>
        public void SetApi(ICoreAPI api)
        {
            this.api = api;
        }

        /// <summary>
        /// Resets aeration to default well-aerated state.
        /// </summary>
        public void Reset()
        {
            aerationLevel = 0.7f;
            lastAerationUpdateTime = 0;
            lastTurnTime = 0;
        }

        #endregion

        #region Aeration Updates

        /// <summary>
        /// Updates aeration based on compaction, moisture, and time since last turn.
        /// Should be called regularly (e.g., hourly).
        /// </summary>
        /// <param name="currentTime">Current game time in hours</param>
        /// <param name="moistureLevel">Current moisture level (0.0 to 1.0)</param>
        public void UpdateAeration(double currentTime, float moistureLevel)
        {
            if (api?.World == null) return;

            // Initialize on first update
            if (lastAerationUpdateTime == 0)
            {
                lastAerationUpdateTime = currentTime;
                lastTurnTime = currentTime;
                return;
            }

            // Only update once per hour
            double hoursSinceLastUpdate = currentTime - lastAerationUpdateTime;
            if (hoursSinceLastUpdate < 1.0) return;

            lastAerationUpdateTime = currentTime;

            // Calculate compaction (pile naturally settles and loses air over time)
            float compactionLoss = CalculateCompactionLoss(hoursSinceLastUpdate);
            
            // Calculate moisture effect (water fills air pockets)
            float moistureLoss = CalculateMoistureLoss(moistureLevel);

            // Apply losses
            aerationLevel = GameMath.Clamp(aerationLevel - compactionLoss - moistureLoss, 0f, 1f);
        }

        /// <summary>
        /// Calculates aeration loss from natural compaction/settling.
        /// </summary>
        private float CalculateCompactionLoss(double hours)
        {
            // Pile compacts faster when first turned, slower as it settles
            double hoursSinceTurn = HoursSinceLastTurn;
            
            if (hoursSinceTurn < 24) // First day: rapid settling
            {
                return BASE_COMPACTION_RATE * (float)hours * 1.5f;
            }
            else if (hoursSinceTurn < 72) // Days 2-3: moderate settling
            {
                return BASE_COMPACTION_RATE * (float)hours;
            }
            else // After 3 days: slow settling (already compacted)
            {
                return BASE_COMPACTION_RATE * (float)hours * 0.5f;
            }
        }

        /// <summary>
        /// Calculates aeration loss from moisture filling air pockets.
        /// </summary>
        private float CalculateMoistureLoss(float moistureLevel)
        {
            // Wet compost loses aeration as water fills pore spaces
            if (moistureLevel > 0.6f) // Above optimal moisture
            {
                float excessMoisture = moistureLevel - 0.6f;
                return excessMoisture * MOISTURE_AERATION_FACTOR * 0.01f; // Small per-update loss
            }
            
            return 0f;
        }

        #endregion

        #region Manual Aeration Control

        /// <summary>
        /// Increases aeration by turning/mixing the pile.
        /// </summary>
        /// <param name="amount">Amount to increase (0.0 to 1.0), defaults to standard turn boost</param>
        public void Aerate(float amount)
        {
            aerationLevel = GameMath.Clamp(aerationLevel + amount, 0f, 1f);
            
            if (api?.World != null)
            {
                lastTurnTime = api.World.Calendar.TotalHours;
            }
        }

        /// <summary>
        /// Performs a standard turning operation, restoring good aeration.
        /// Note: Turning also helps reduce excess moisture through increased evaporation.
        /// The moisture reduction is handled by the caller (CompostProcessor).
        /// </summary>
        public void Turn()
        {
            Aerate(TURN_AERATION_BOOST);
        }

        /// <summary>
        /// Sets the aeration level directly (clamped to valid range).
        /// </summary>
        public void SetLevel(float level)
        {
            aerationLevel = GameMath.Clamp(level, 0f, 1f);
        }

        #endregion

        #region Decomposition Modifiers

        /// <summary>
        /// Gets the decomposition rate modifier based on current aeration level.
        /// Returns a multiplier where 1.0 = normal speed.
        /// </summary>
        public float GetDecompositionModifier()
        {
            if (aerationLevel < 0.2f)
            {
                // Completely anaerobic: extremely slow, may putrefy instead
                return 0.1f;
            }
            else if (aerationLevel < ANAEROBIC_THRESHOLD)
            {
                // Anaerobic: very slow decomposition
                return 0.3f;
            }
            else if (aerationLevel < OPTIMAL_MIN)
            {
                // Low oxygen: reduced decomposition
                return 0.7f;
            }
            else if (aerationLevel <= OPTIMAL_MAX)
            {
                // Optimal: full aerobic decomposition speed
                return 1.0f;
            }
            else
            {
                // Over-aerated: slightly reduced (material dries out, heat loss)
                return 0.9f;
            }
        }

        #endregion

        #region Serialization

        /// <summary>
        /// Saves aeration state to tree attributes.
        /// </summary>
        public void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetFloat("aerationLevel", aerationLevel);
            tree.SetDouble("lastAerationUpdateTime", lastAerationUpdateTime);
            tree.SetDouble("lastTurnTime", lastTurnTime);
        }

        /// <summary>
        /// Loads aeration state from tree attributes.
        /// </summary>
        public void FromTreeAttributes(ITreeAttribute tree)
        {
            aerationLevel = tree.GetFloat("aerationLevel", 0.7f); // Default to well-aerated
            lastAerationUpdateTime = tree.GetDouble("lastAerationUpdateTime", 0);
            lastTurnTime = tree.GetDouble("lastTurnTime", 0);
        }

        #endregion
    }
}