using System.Collections.Generic;
using CompostingRedux.BlockEntities;
using CompostingRedux.Configuration;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace CompostingRedux.Blocks
{
    /// <summary>
    /// Block class for the compost bin. Handles player interaction and provides UI hints.
    /// </summary>
    public class BlockCompostBin : Block
    {
        #region Interaction

        /// <summary>
        /// Handles player interaction with the compost bin block.
        /// Delegates to the block entity for actual logic.
        /// </summary>
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityCompostBin be)
            {
                return be.OnInteract(byPlayer);
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        #endregion

        #region UI Hints

        /// <summary>
        /// Provides contextual interaction hints to the player based on the current state of the compost bin.
        /// </summary>
        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection,
            IPlayer forPlayer)
        {
            if (!(world.BlockAccessor.GetBlockEntity(selection.Position) is BlockEntityCompostBin be))
            {
                return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
            }

            var interactions = new List<WorldInteraction>();
            var config = CompostingReduxModSystem.Config;

            // 1. Add Items Interaction
            if (be.ItemAmount < config.MaxCapacity && !be.IsFinished)
            {
                var compostableItems = GetCompostableExamples(world);

                if (compostableItems.Count > 0)
                {
                    interactions.Add(new WorldInteraction
                    {
                        ActionLangCode = "compostingredux:blockhelp-compostbin-add",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = compostableItems.ToArray()
                    });

                    interactions.Add(new WorldInteraction
                    {
                        ActionLangCode = "compostingredux:blockhelp-compostbin-bulkadd",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "ctrl",
                        Itemstacks = compostableItems.ToArray()
                    });
                }
            }

            // 2. Turn Pile Interaction
            if (be is { ItemAmount: > 0, IsFinished: false })
            {
                var shovels = GetShovelExamples(world);
                if (shovels.Count > 0)
                {
                    interactions.Add(new WorldInteraction
                    {
                        ActionLangCode = "compostingredux:blockhelp-compostbin-turn",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = shovels.ToArray()
                    });
                }
            }

            // 3. Harvest Interaction
            if (be.IsFinished)
            {
                interactions.Add(new WorldInteraction
                {
                    ActionLangCode = "compostingredux:blockhelp-compostbin-harvest",
                    MouseButton = EnumMouseButton.Right
                });
            }

            return interactions.ToArray();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets example compostable items to display in interaction hints.
        /// </summary>
        private List<ItemStack> GetCompostableExamples(IWorldAccessor world)
        {
            var list = new List<ItemStack>();

            TryAdd(world, list, "game:rot");

            // Add one example of each category
            string[] vegExamples = { "cabbage", "carrot", "onion", "turnip", "parsnip" };
            foreach (var veg in vegExamples)
            {
                if (TryAdd(world, list, $"game:vegetable-{veg}")) break;
            }

            string[] grainExamples = { "flax", "rice", "rye", "spelt" };
            foreach (var grain in grainExamples)
            {
                if (TryAdd(world, list, $"game:grain-{grain}")) break;
            }

            return list;
        }

        /// <summary>
        /// Gets example shovel items to display in interaction hints.
        /// </summary>
        private List<ItemStack> GetShovelExamples(IWorldAccessor world)
        {
            var list = new List<ItemStack>();
            string[] materials = { "copper", "bronze", "iron", "steel" };

            foreach (var mat in materials)
            {
                TryAdd(world, list, $"game:shovel-{mat}");
            }

            return list;
        }

        /// <summary>
        /// Attempts to add an item to the list by its code. Returns true if successful.
        /// </summary>
        private bool TryAdd(IWorldAccessor world, List<ItemStack> list, string itemCode)
        {
            var item = world.GetItem(new AssetLocation(itemCode));
            if (item != null)
            {
                list.Add(new ItemStack(item));
                return true;
            }

            return false;
        }

        #endregion
    }
}