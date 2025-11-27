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
        /// Array of item code path prefixes that are considered compostable.
        /// Items whose code path starts with any of these strings can be composted.
        /// Default: ["vegetable-", "grain-"]
        /// </summary>
        public string[] CompostablePathPrefixes { get; set; } = new[]
        {
            "vegetable-",
            "grain-"
        };

        /// <summary>
        /// Array of exact item code paths that are considered compostable.
        /// Items whose code path exactly matches any of these strings can be composted.
        /// Default: ["rot"]
        /// </summary>
        public string[] CompostableExactPaths { get; set; } = new[]
        {
            "rot"
        };

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

            return CompostableExactPaths.Contains(path) ||
                   CompostablePathPrefixes.Any(prefix => path.StartsWith(prefix));
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
}