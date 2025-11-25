using System.Collections.Generic;
using CompostingRedux.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace CompostingRedux.Blocks
{
    public class BlockCompostBin : Block
    {
        private WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            // Define interactions once on load to save performance
            if (api.Side != EnumAppSide.Client) return;

            var compostableStackList = new List<ItemStack>();
            var shovelStackList = new List<ItemStack>();

            // Helper to safely add itemstacks if they exist
            void AddItemIfExists(string path)
            {
                var item = api.World.GetItem(new AssetLocation(path));
                if (item != null) compostableStackList.Add(new ItemStack(item));
            }

            // Add representative items for the handbook tooltip
            AddItemIfExists("game:rot");
            AddItemIfExists("game:vegetable-carrot");
            AddItemIfExists("game:grain-spelt");

            // Find all shovels dynamically
            foreach (var item in api.World.Items)
            {
                if (item.Code?.Path.StartsWith("shovel-") == true)
                {
                    shovelStackList.Add(new ItemStack(item));
                }
            }

            interactions = new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "compostingredux:blockhelp-compostbin-add",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = compostableStackList.ToArray()
                },
                new WorldInteraction()
                {
                    ActionLangCode = "compostingredux:blockhelp-compostbin-bulkadd",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "ctrl",
                    Itemstacks = compostableStackList.ToArray()
                },
                new WorldInteraction()
                {
                    ActionLangCode = "compostingredux:blockhelp-compostbin-turn",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = shovelStackList.ToArray()
                },
                new WorldInteraction()
                {
                    ActionLangCode = "compostingredux:blockhelp-compostbin-harvest",
                    MouseButton = EnumMouseButton.Right,
                }
            };
        }

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
            if (interactions == null) return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);

            // We still need the BE to know WHICH interactions to show
            if (!(world.BlockAccessor.GetBlockEntity(selection.Position) is BlockEntityCompostBin composter))
                return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);

            // Filter the cached list based on current state
            var activeInteractions = new List<WorldInteraction>();

            bool canAdd = composter.ItemAmount < CompostingReduxModSystem.Config.MaxCapacity && !composter.IsFinished;
            bool canTurn = composter.ItemAmount > 0 && !composter.IsFinished;
            bool canHarvest = composter.IsFinished;

            if (canAdd)
            {
                activeInteractions.Add(interactions[0]); // Single Add
                activeInteractions.Add(interactions[1]); // Bulk Add
            }

            if (canTurn)
            {
                activeInteractions.Add(interactions[2]); // Turn
            }

            if (canHarvest)
            {
                activeInteractions.Add(interactions[3]); // Harvest
            }

            return activeInteractions.ToArray();
        }
    }
}