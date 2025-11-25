using System.Collections.Generic;
using CompostingRedux.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace CompostingRedux.Blocks
{
    public class BlockCompostBin : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityCompostBin be)
            {
                return be.OnInteract(byPlayer);
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection,
            IPlayer forPlayer)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(selection.Position);
            if (!(be is BlockEntityCompostBin composter))
                return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);

            List<WorldInteraction> interactions = new List<WorldInteraction>();

            // Can add rot?
            if (composter.ItemAmount < CompostingReduxModSystem.Config.MaxCapacity && !composter.IsFinished)
            {
                List<ItemStack> compostableItems = new List<ItemStack>();
    
                // Add rot
                Item rotItem = world.GetItem(new AssetLocation("game:rot"));
                if (rotItem != null)
                {
                    compostableItems.Add(new ItemStack(rotItem));
                }
    
                // Add some example vegetables
                string[] vegetables = new[] { "cabbage", "carrot", "onion", "turnip", "parsnip" };
                foreach (string veg in vegetables)
                {
                    Item vegItem = world.GetItem(new AssetLocation($"game:vegetable-{veg}"));
                    if (vegItem != null)
                    {
                        compostableItems.Add(new ItemStack(vegItem));
                        break; // Just show one vegetable as example
                    }
                }
    
                // Add some example grains
                string[] grains = new[] { "flax", "rice", "rye", "spelt" };
                foreach (string grain in grains)
                {
                    Item grainItem = world.GetItem(new AssetLocation($"game:grain-{grain}"));
                    if (grainItem != null)
                    {
                        compostableItems.Add(new ItemStack(grainItem));
                        break; // Just show one grain as example
                    }
                }
    
                if (compostableItems.Count > 0)
                {
                    // Single add
                    interactions.Add(new WorldInteraction()
                    {
                        ActionLangCode = "compostingredux:blockhelp-compostbin-add",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = compostableItems.ToArray()
                    });
        
                    // Bulk add
                    interactions.Add(new WorldInteraction()
                    {
                        ActionLangCode = "compostingredux:blockhelp-compostbin-bulkadd",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "ctrl",
                        Itemstacks = compostableItems.ToArray()
                    });
                }
            }

            // Can turn pile with shovel?
            if (composter.ItemAmount > 0 && !composter.IsFinished)
            {
                List<ItemStack> shovels = new List<ItemStack>();

                // Safely add shovels if they exist
                string[] shovelTypes = new[] { "copper", "bronze", "iron", "steel" };
                foreach (string type in shovelTypes)
                {
                    Item shovel = world.GetItem(new AssetLocation($"game:shovel-{type}"));
                    if (shovel != null)
                    {
                        shovels.Add(new ItemStack(shovel));
                    }
                }

                if (shovels.Count > 0)
                {
                    interactions.Add(new WorldInteraction()
                    {
                        ActionLangCode = "compostingredux:blockhelp-compostbin-turn",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = shovels.ToArray()
                    });
                }
            }

            // Can harvest?
            if (composter.IsFinished)
            {
                interactions.Add(new WorldInteraction()
                {
                    ActionLangCode = "compostingredux:blockhelp-compostbin-harvest",
                    MouseButton = EnumMouseButton.Right
                });
            }

            return interactions.ToArray();
        }
    }
}