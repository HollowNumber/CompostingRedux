using Vintagestory.API.Common;

namespace CompostingRedux.Composting.Interfaces
{
    /// <summary>
    /// Interface for blocks that produce harvestable output items upon completion.
    /// Used by composting systems, fermentation vessels, and similar processing blocks.
    /// </summary>
    public interface IHarvestable
    {
        /// <summary>
        /// Gets whether the block is ready to harvest (process is complete).
        /// </summary>
        bool CanHarvest { get; }

        /// <summary>
        /// Gets the number of output items that will be produced when harvested.
        /// </summary>
        int OutputCount { get; }

        /// <summary>
        /// Harvests the finished product and returns the output item stack.
        /// </summary>
        /// <param name="byPlayer">The player performing the harvest</param>
        /// <returns>The harvested item stack, or null if harvest failed</returns>
        ItemStack Harvest(IPlayer byPlayer);

        /// <summary>
        /// Gets a preview of what will be harvested without actually harvesting.
        /// </summary>
        /// <returns>The item stack that would be produced, or null if not ready</returns>
        ItemStack GetHarvestPreview();
    }
}