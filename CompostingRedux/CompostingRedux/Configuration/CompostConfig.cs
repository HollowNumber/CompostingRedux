using System;
using System.Linq;
using Vintagestory.API.Common;

namespace CompostingRedux.Configuration
{
    /// <summary>
    /// Configuration settings for the Composting Redux mod.
    /// All values can be customized via the config file.
    /// </summary>
    public class CompostConfig
    {
        #region Capacity Settings

        /// <summary>
        /// Maximum number of items that can be stored in a compost bin.
        /// Default: 64
        /// </summary>
        public int MaxCapacity { get; set; } = 64;

        /// <summary>
        /// Number of items added when using Ctrl+Click for bulk adding.
        /// Default: 4
        /// </summary>
        public int BulkAddAmount { get; set; } = 4;

        #endregion

        #region Timing Settings

        /// <summary>
        /// Total in-game hours required for composting to complete naturally (without turning).
        /// Default: 240 hours (10 in-game days)
        /// </summary>
        public int HoursToComplete { get; set; } = 240;

        /// <summary>
        /// Number of hours the composting process is accelerated each time the pile is turned with a shovel.
        /// Default: 5 hours
        /// </summary>
        public int ShovelSpeedupHours { get; set; } = 5;

        /// <summary>
        /// Cooldown period in hours between shovel turns to prevent spam.
        /// Default: 5 hours
        /// </summary>
        public int ShovelTurnCooldownHours { get; set; } = 5;

        #endregion

        #region Fill Level Thresholds

        /// <summary>
        /// Fill ratio threshold (0.0 to 1.0) for displaying fill level 2.
        /// Default: 0.01 (1%)
        /// </summary>
        public float FillLevel2Threshold { get; set; } = 0.01f;

        /// <summary>
        /// Fill ratio threshold (0.0 to 1.0) for displaying fill level 4.
        /// Default: 0.3 (30%)
        /// </summary>
        public float FillLevel4Threshold { get; set; } = 0.3f;

        /// <summary>
        /// Fill ratio threshold (0.0 to 1.0) for displaying fill level 6.
        /// Default: 0.6 (60%)
        /// </summary>
        public float FillLevel6Threshold { get; set; } = 0.6f;

        /// <summary>
        /// Fill ratio threshold (0.0 to 1.0) for displaying fill level 8.
        /// Default: 0.95 (95%)
        /// </summary>
        public float FillLevel8Threshold { get; set; } = 0.95f;

        #endregion

        #region Output Settings

        /// <summary>
        /// Multiplier for how much compost is produced per input item.
        /// Default: 0.5 (each input item produces half a compost)
        /// </summary>
        public float OutputPerItem { get; set; } = 0.5f;

        #endregion

        #region Compostable Items

        /// <summary>
        /// Green materials (nitrogen-rich) - decompose faster, heat up pile.
        /// Format: "itemcode" or "prefix-*"
        /// Default: vegetables, fruits, grass, manure
        /// </summary>
        public string[] GreenMaterials { get; set; } = new[]
        {
            "vegetable-",
            "fruit-",
            "rot"
        };

        /// <summary>
        /// Brown materials (carbon-rich) - decompose slower, provide structure.
        /// Format: "itemcode" or "prefix-*"
        /// Default: grains, straw, dry materials
        /// </summary>
        public string[] BrownMaterials { get; set; } = new[]
        {
            "grain-",
        };

        #endregion

        #region Material Properties

        /// <summary>
        /// Carbon to Nitrogen ratio for green materials (nitrogen-rich).
        /// Greens have LOW C:N ratios. Default: 15 (typical for fresh grass/food scraps)
        /// </summary>
        public float GreenCNRatio { get; set; } = 15f;

        /// <summary>
        /// Carbon to Nitrogen ratio for brown materials (carbon-rich).
        /// Browns have HIGH C:N ratios. Default: 60 (typical for dry leaves/straw)
        /// </summary>
        public float BrownCNRatio { get; set; } = 60f;

        /// <summary>
        /// Optimal carbon to nitrogen ratio for fast composting.
        /// Default: 27.5 (realistic range is 25-30)
        /// </summary>
        public float OptimalCNRatio { get; set; } = 27.5f;

        /// <summary>
        /// Speed bonus when C:N ratio is optimal (multiplier).
        /// Default: 1.5 (50% faster)
        /// </summary>
        public float OptimalRatioBonus { get; set; } = 1.5f;

        /// <summary>
        /// Speed penalty when C:N ratio is very poor (multiplier).
        /// Default: 0.5 (50% slower)
        /// </summary>
        public float PoorRatioPenalty { get; set; } = 0.5f;

        #endregion

        #region Methods

        /// <summary>
        /// Determines if an item in the given slot is compostable based on the configured rules.
        /// </summary>
        /// <param name="handSlot">The item slot to check (typically the player's active hotbar slot)</param>
        /// <returns>True if the item is compostable, false otherwise</returns>
        public bool IsCompostable(ItemSlot? handSlot)
        {
            if (handSlot?.Empty != false) return false;

            string path = handSlot.Itemstack.Collectible.Code.Path;

            // Check green and brown materials
            return IsGreenMaterial(path) || IsBrownMaterial(path);
        }

        /// <summary>
        /// Checks if an item is a green (nitrogen-rich) material.
        /// </summary>
        public bool IsGreenMaterial(string itemPath)
        {
            return GreenMaterials.Any(mat => 
                mat.EndsWith("-") ? itemPath.StartsWith(mat) : itemPath == mat);
        }

        /// <summary>
        /// Checks if an item is a brown (carbon-rich) material.
        /// </summary>
        public bool IsBrownMaterial(string itemPath)
        {
            return BrownMaterials.Any(mat => 
                mat.EndsWith("-") ? itemPath.StartsWith(mat) : itemPath == mat);
        }

        /// <summary>
        /// Gets the material type for composting calculations.
        /// </summary>
        public MaterialType GetMaterialType(string itemPath)
        {
            if (IsGreenMaterial(itemPath)) return MaterialType.Green;
            return MaterialType.Brown;
        }

        /// <summary>
        /// Creates a new instance of CompostConfig with default values.
        /// </summary>
        /// <returns>A new CompostConfig instance with all default settings</returns>
        public static CompostConfig GetDefault()
        {
            return new CompostConfig();
        }

        #endregion
    }

    /// <summary>
    /// Material type for composting.
    /// </summary>
    public enum MaterialType
    {
        Green,  // Nitrogen-rich (vegetables, fruits, grass)
        Brown   // Carbon-rich (grains, straw, dry materials)
    }
}