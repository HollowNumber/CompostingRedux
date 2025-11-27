using System;
using System.Text;
using CompostingRedux.Composting;
using CompostingRedux.Composting.Moisture;
using CompostingRedux.Configuration;
using CompostingRedux.Helpers;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace CompostingRedux.BlockEntities
{
    /// <summary>
    /// Block entity for the compost bin. Handles composting organic materials over time.
    /// Players can add compostable items, turn the pile with a shovel to speed up decomposition,
    /// and harvest finished compost.
    /// </summary>
    public class BlockEntityCompostBin : BlockEntity
    {
        #region Fields

        private CompostBinInventory inventory = null!;
        private CompostProcessor processor = null!;
        private long? tickListenerId = null;
        private CompostBinFeedback feedback = null!;
        private ITreeAttribute? savedInventoryTree = null; // Store inventory tree for reload after API is set

        #endregion

        #region Properties

        /// <summary>
        /// Maximum number of items that can be added to the compost bin.
        /// </summary>
        private int MaxCapacity => CompostingReduxModSystem.Config.MaxCapacity;

        /// <summary>
        /// Number of in-game hours required for composting to complete.
        /// </summary>
        private int HoursToComplete => CompostingReduxModSystem.Config.HoursToComplete;

        /// <summary>
        /// Returns true if this is running on the client side.
        /// </summary>
        private bool IsClient => Api.Side == EnumAppSide.Client;

        /// <summary>
        /// Current number of items in the compost bin.
        /// </summary>
        public int ItemAmount => processor.TotalItemCount;

        /// <summary>
        /// Returns true if composting is complete.
        /// </summary>
        public bool IsFinished => processor.IsFinished;

        /// <summary>
        /// Calculate current progress based on elapsed time (0-100).
        /// </summary>
        public int CompostProgress => processor.DecompositionPercent;

        /// <summary>
        /// Number of in-game hours remaining until composting is complete.
        /// </summary>
        public int RemainingHours => processor.RemainingHours;

        /// <summary>
        /// Returns true if enough time has passed since the last turn to allow turning again.
        /// </summary>
        public bool CanTurnPile => processor.CanTurn;

        /// <summary>
        /// Number of in-game hours that have elapsed since composting started.
        /// </summary>
        public int ElapsedHours => processor.ElapsedHours;

        #endregion

        #region Initialization & Lifecycle

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            // Initialize inventory
            if (inventory == null)
            {
                inventory = new CompostBinInventory("compostbin", $"{Pos.X}/{Pos.Y}/{Pos.Z}", api);
            }
            else
            {
                // Inventory was loaded from save - need to set API reference
                inventory.Api = api;
                
                // Reload inventory data now that API is available for proper ItemStack deserialization
                if (savedInventoryTree != null)
                {
                    inventory.FromTreeAttributes(savedInventoryTree);
                    savedInventoryTree = null; // Clear after use
                }
            }

            // Initialize processor
            if (processor == null)
            {
                processor = new CompostProcessor(api, inventory);
            }
            else
            {
                // Ensure API is set (in case processor was created during deserialization)
                processor.SetApi(api);
                processor.SetInventory(inventory);
            }

            // Set block position for environmental checks (rain, climate, temperature)
            processor.SetBlockPos(Pos);

            // Initialize feedback handler
            feedback = new CompostBinFeedback(api, Pos);

            // Only start ticking if we have active composting
            if (processor.TotalItemCount > 0 && !processor.IsFinished)
            {
                StartCompostingTick();
            }
        }

        /// <summary>
        /// Called periodically to update composting progress.
        /// </summary>
        private void OnTick(float dt)
        {
            if (processor.IsFinished || processor.TotalItemCount == 0) return;

            // Update decomposition
            processor.Update();

            // Check if composting is complete
            if (processor.IsFinished)
            {
                StopCompostingTick();
                UpdateBlockState();
                MarkDirty();
            }
        }

        /// <summary>
        /// Registers the tick listener to monitor composting progress.
        /// </summary>
        private void StartCompostingTick()
        {
            if (tickListenerId == null)
            {
                tickListenerId = RegisterGameTickListener(OnTick, CompostBinConstants.TickIntervalMs);
            }
        }

        /// <summary>
        /// Unregisters the tick listener when composting is complete or cancelled.
        /// </summary>
        private void StopCompostingTick()
        {
            if (tickListenerId.HasValue)
            {
                UnregisterGameTickListener(tickListenerId.Value);
                tickListenerId = null;
            }
        }

        #endregion

        #region Player Interaction

        /// <summary>
        /// Handles player interaction with the compost bin.
        /// Supports: adding items, turning pile with shovel, watering with liquid containers, and harvesting finished compost.
        /// </summary>
        public bool OnInteract(IPlayer byPlayer)
        {
            CompostConfig config = CompostingReduxModSystem.Config;
            ItemSlot handSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            // Case 1: Player is holding a Shovel (Turning the pile)
            if (handSlot.Itemstack?.Collectible.Tool == EnumTool.Shovel)
            {
                return HandleShovelInteraction(byPlayer, config);
            }

            // Case 2: Watering with liquid container (water bucket, water portion, etc.)
            if (processor.TotalItemCount > 0 && !processor.IsFinished && TryWaterCompost(byPlayer, handSlot))
            {
                return true;
            }

            // Case 3: Adding Compostable Items
            if (config.IsCompostable(handSlot) && processor.TotalItemCount < MaxCapacity)
            {
                return HandleAddItems(byPlayer, handSlot, config);
            }



            // Case 4: Finished and Empty Hand (Harvesting)
            if (processor.IsFinished && handSlot.Empty)
            {
                return HandleHarvest(byPlayer, config);
            }

            return false;
        }

        /// <summary>
        /// Handles interaction when player uses a shovel to turn the compost pile.
        /// </summary>
        private bool HandleShovelInteraction(IPlayer byPlayer, CompostConfig config)
        {
            if (processor.TotalItemCount == 0 || processor.IsFinished) return false;

            // Check cooldown
            if (!processor.CanTurn)
            {
                double cooldownRemaining = processor.TurnCooldownRemaining;
                feedback.ShowCooldownMessage(byPlayer, cooldownRemaining);
                return true;
            }

            // Turn the pile (speeds up decomposition)
            processor.TurnPile(config.ShovelSpeedupHours);

            // Play feedback
            feedback.PlayShovelDigAnimation(byPlayer);
            feedback.PlayDigSoundBurst(processor.IsFinished, byPlayer);

            // Check if this turn completed the composting
            if (processor.IsFinished)
            {
                StopCompostingTick();
                UpdateBlockState();
            }

            MarkDirty(true);
            return true;
        }

        /// <summary>
        /// Handles adding compostable items to the bin.
        /// </summary>
        private bool HandleAddItems(IPlayer byPlayer, ItemSlot handSlot, CompostConfig config)
        {
            bool isBulkAdd = byPlayer.Entity.Controls.CtrlKey;

            int takeAmount;
            if (isBulkAdd)
            {
                int bulkAmount = config.BulkAddAmount;
                takeAmount = GameMath.Min(bulkAmount, GameMath.Min(handSlot.StackSize, MaxCapacity - processor.TotalItemCount));
            }
            else
            {
                // Single add - add only 1 item
                takeAmount = GameMath.Min(1, MaxCapacity - processor.TotalItemCount);
            }

            if (takeAmount == 0) return false;

            // Create a stack to add
            ItemStack stackToAdd = handSlot.Itemstack.Clone();
            stackToAdd.StackSize = takeAmount;

            // Check if this is the first addition
            bool wasEmpty = processor.TotalItemCount == 0;

            // Add to inventory
            int actuallyAdded = inventory.TryAddItems(stackToAdd, MaxCapacity);
            if (actuallyAdded == 0) return false;

            // Take items from player's hand
            handSlot.TakeOut(actuallyAdded);

            // Start composting timer if this is the first addition
            if (wasEmpty)
            {
                processor.StartComposting();
                StartCompostingTick();
            }

            feedback.PlayAddSound(byPlayer);
            UpdateBlockState();
            MarkDirty(true);

            return true;
        }

        /// <summary>
        /// Handles harvesting finished compost from the bin.
        /// </summary>
        private bool HandleHarvest(IPlayer byPlayer, CompostConfig config)
        {
            Item compostItem = Api.World.GetItem(new AssetLocation(CompostBinConstants.ItemCompost));

            if (compostItem == null)
            {
                Api.Logger.Error($"Compost item '{CompostBinConstants.ItemCompost}' not found in asset database! Cannot harvest.");
                return true;
            }

            int outputAmount = (int)(processor.TotalItemCount * config.OutputPerItem);
            ItemStack outputStack = new ItemStack(compostItem, outputAmount);

            bool transferred = byPlayer.InventoryManager.TryGiveItemstack(outputStack, slotNotifyEffect: true);

            if (transferred)
            {
                // Only reset state if items were successfully transferred
                feedback.PlayHarvestSound(byPlayer);

                // Reset State
                processor.Harvest();
                StopCompostingTick();
                UpdateBlockState();
                MarkDirty();
            }
            else
            {
                // Notify player their inventory is full
                feedback.ShowInventoryFullMessage(byPlayer);
            }

            return transferred;
        }

        /// <summary>
        /// Attempts to water the compost pile using a liquid container (water portion, bucket, etc.).
        /// Returns true if watering was successful.
        /// </summary>
        private bool TryWaterCompost(IPlayer byPlayer, ItemSlot handSlot)
        {
            if (handSlot?.Itemstack == null) return false;

            ItemStack heldStack = handSlot.Itemstack;
            string itemPath = heldStack.Collectible.Code.Path;

            // Check if holding a water portion (direct water items)
            if (itemPath.StartsWith("waterportion"))
            {
                // Water portions are 10ml each
                // Calculate how much moisture to add based on compost dryness
                float currentMoisture = processor.MoistureLevel;
                float moistureNeeded = GameMath.Max(0.1f, 0.6f - currentMoisture); // At least 10%, target optimal

                // Each water portion (10ml) adds about 0.1% moisture (small amount)
                // Minimum 100 portions (1000ml / 1L) to match game standards
                int portionsToUse = GameMath.Clamp((int)Math.Ceiling(moistureNeeded / 0.001f), 100, heldStack.StackSize);
                float moistureToAdd = portionsToUse * 0.001f;

                // Add moisture
                processor.AddWater(moistureToAdd);

                // Consume the water portions
                handSlot.TakeOut(portionsToUse);
                handSlot.MarkDirty();

                // Play feedback
                Api.World.PlaySoundAt(new AssetLocation("sounds/environment/largesplash"), Pos, 0, byPlayer, false);
                if (Api.Side == EnumAppSide.Server)
                {
                    SpawnWaterParticles();
                }

                MarkDirty();
                return true;
            }

            // Check for any liquid container (buckets, bowls, etc.) using VS's BlockLiquidContainerBase
            if (heldStack.Block != null)
            {
                var containerBlock = heldStack.Block;

                // Check if it's a liquid container by looking for GetContent method
                try
                {
                    var getContentMethod = containerBlock.GetType().GetMethod("GetContent", new[] { typeof(ItemStack) });
                    var setContentMethod = containerBlock.GetType().GetMethod("SetContent", new[] { typeof(ItemStack), typeof(ItemStack) });

                    if (getContentMethod != null && setContentMethod != null)
                    {
                        ItemStack contents = getContentMethod.Invoke(containerBlock, new object[] { heldStack }) as ItemStack;
                        if (contents != null && contents.Collectible != null && contents.Collectible.Code.Path.Contains("waterportion"))
                        {
                            // Calculate how much moisture to add based on how dry the compost is
                            float currentMoisture = processor.MoistureLevel;
                            float moistureNeeded = GameMath.Max(0.05f, 0.6f - currentMoisture); // At least 5%, target optimal (60%)

                            // Container capacity varies (bowls ~1L, buckets ~10L)
                            // Each portion (10ml) adds ~0.1% moisture
                            // Minimum 100 portions (1000ml / 1L) to match game standards
                            int portionsToUse = GameMath.Clamp((int)Math.Ceiling(moistureNeeded / 0.001f), 100, contents.StackSize);
                            float moistureToAdd = portionsToUse * 0.001f;

                            // Add moisture to compost
                            processor.AddWater(moistureToAdd);

                            // Remove water portions from bucket
                            contents.StackSize -= portionsToUse;
                            if (contents.StackSize <= 0)
                            {
                                contents = null; // Empty bucket
                            }

                            // Update container contents
                            setContentMethod.Invoke(containerBlock, new object[] { heldStack, contents });
                            handSlot.MarkDirty();

                            // Play feedback
                            Api.World.PlaySoundAt(new AssetLocation("sounds/environment/largesplash"), Pos, 0, byPlayer, false);
                            if (Api.Side == EnumAppSide.Server)
                            {
                                SpawnWaterParticles();
                            }

                            MarkDirty();
                            return true;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Api.Logger.Warning($"CompostBin: Error handling liquid container water: {ex.Message}");
                }
            }



            return false;
        }



        /// <summary>
        /// Spawns water splash particles on the compost pile.
        /// </summary>
        private void SpawnWaterParticles()
        {
            if (Api.Side != EnumAppSide.Server) return;

            Vec3d pos = Pos.ToVec3d().Add(0.5, 0.5, 0.5);

            SimpleParticleProperties waterParticles = new SimpleParticleProperties(
                1, 3,
                ColorUtil.ToRgba(180, 100, 150, 220),
                pos.Add(-0.25, 0, -0.25),
                pos.Add(0.25, 0.1, 0.25),
                new Vec3f(-0.5f, -0.5f, -0.5f),
                new Vec3f(0.5f, 0.5f, 0.5f),
                0.5f, 0.5f,
                0.25f, 0.5f,
                EnumParticleModel.Quad
            );

            waterParticles.MinQuantity = 5;
            waterParticles.AddQuantity = 10;
            waterParticles.GravityEffect = 0.8f;
            waterParticles.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.3f);

            Api.World.SpawnParticles(waterParticles);
        }




        #endregion



        #region Block State Management

        /// <summary>
        /// Updates the visual block state based on fill level and doneness.
        /// </summary>
        private void UpdateBlockState()
        {
            if (Api?.World?.BlockAccessor == null) return;

            float fillRatio = (float)processor.TotalItemCount / MaxCapacity;
            string newFillLevel = GetFillLevel(fillRatio);
            string newDonenessState = processor.IsFinished ? "done" : "raw";

            Block currentBlock = Api.World.BlockAccessor.GetBlock(Pos);

            // Check if we're already in the correct state
            if (currentBlock.Code.Path.EndsWith($"{newFillLevel}-{newDonenessState}"))
            {
                return;
            }

            // Exchange to new block state
            AssetLocation newBlockCode = currentBlock.CodeWithParts(newFillLevel, newDonenessState);
            Block newBlock = Api.World.GetBlock(newBlockCode);

            if (newBlock != null)
            {
                Api.World.BlockAccessor.ExchangeBlock(newBlock.Id, Pos);
                Api.Logger.Debug($"Compost Bin at {Pos} updated to Fill Level: {newFillLevel}, Doneness: {newDonenessState}");
            }
            else
            {
                Api.Logger.Warning($"Failed to find block variant: {newBlockCode}");
            }
        }

        /// <summary>
        /// Determines the fill level string based on the fill ratio.
        /// </summary>
        private string GetFillLevel(float fillRatio)
        {
            CompostConfig config = CompostingReduxModSystem.Config;

            if (fillRatio >= config.FillLevel8Threshold) return "8";
            if (fillRatio > config.FillLevel6Threshold) return "6";
            if (fillRatio > config.FillLevel4Threshold) return "4";
            if (fillRatio > config.FillLevel2Threshold) return "2";
            return "0";
        }

        #endregion

        #region UI & Tooltips

        /// <summary>
        /// Displays hover text with composting progress information.
        /// </summary>
        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            dsc.AppendLine(Lang.Get("compostingredux:compostbin-tooltip-composting-contents",
                $"{processor.TotalItemCount}/{MaxCapacity}"));

            if (processor.TotalItemCount > 0)
            {
                // Show actual items in the pile, grouped by type
                var itemSummary = inventory.GetItemSummary();
                var config = CompostingReduxModSystem.Config;

                if (itemSummary.Count > 0)
                {
                    var greens = new System.Collections.Generic.List<string>();
                    var browns = new System.Collections.Generic.List<string>();

                    foreach (var kvp in itemSummary)
                    {
                        // Get the item to show its proper name
                        AssetLocation itemLocation = new AssetLocation(kvp.Key);
                        CollectibleObject collectible = Api.World.GetItem(itemLocation) ?? (CollectibleObject)Api.World.GetBlock(itemLocation);

                        if (collectible != null)
                        {
                            string itemName = collectible.GetHeldItemName(new ItemStack(collectible));
                            string displayText = $"{kvp.Value}× {itemName}";

                            // Determine if green or brown
                            string itemPath = collectible.Code.Path;
                            if (config.GetMaterialType(itemPath) == MaterialType.Green)
                            {
                                greens.Add(displayText);
                            }
                            else
                            {
                                browns.Add(displayText);
                            }
                        }
                    }

                    // Display greens
                    if (greens.Count > 0)
                    {
                        dsc.AppendLine($"\n{Lang.Get("compostingredux:compostbin-tooltip-greens-header")}");
                        foreach (var item in greens)
                        {
                            dsc.AppendLine($"  {item}");
                        }
                    }

                    // Display browns
                    if (browns.Count > 0)
                    {
                        dsc.AppendLine($"\n{Lang.Get("compostingredux:compostbin-tooltip-browns-header")}");
                        foreach (var item in browns)
                        {
                            dsc.AppendLine($"  {item}");
                        }
                    }

                    dsc.AppendLine("");
                }

                // Show material composition summary
                dsc.AppendLine(Lang.Get("compostingredux:compostbin-tooltip-materials",
                    processor.GreenMaterialCount, processor.BrownMaterialCount));

                // Show C:N ratio with quality indicator
                float ratio = processor.CarbonNitrogenRatio;
                if (ratio > 0 && ratio < 100)
                {
                    string ratioQuality = processor.RatioQualityText;
                    dsc.AppendLine(Lang.Get("compostingredux:compostbin-tooltip-cn-ratio",
                        ratio.ToString("F1"), ratioQuality));
                }

                // Show speed modifier
                float speedMult = processor.GetSpeedMultiplier();
                if (speedMult != 1.0f)
                {
                    dsc.AppendLine(Lang.Get("compostingredux:compostbin-tooltip-speed",
                        speedMult.ToString("F1")));
                }

                // Show moisture status
                string moistureState = processor.MoistureState;
                float moisturePercent = processor.MoistureLevel * 100f;
                dsc.AppendLine(Lang.Get("compostingredux:compostbin-tooltip-moisture",
                    moistureState, moisturePercent.ToString("F0")));

                // Add moisture warning if not optimal
                if (processor.IsTooDry)
                {
                    dsc.AppendLine(Lang.Get("compostingredux:compostbin-tooltip-moisture-too-dry"));
                }
                else if (processor.IsTooWet)
                {
                    dsc.AppendLine(Lang.Get("compostingredux:compostbin-tooltip-moisture-too-wet"));
                }

                // Show aeration status
                string aerationState = processor.AerationState;
                float aerationPercent = processor.AerationLevel * 100f;
                dsc.AppendLine(Lang.Get("compostingredux:compostbin-tooltip-aeration",
                    aerationState, aerationPercent.ToString("F0")));

                // Add aeration warning if not optimal
                if (processor.IsAnaerobic)
                {
                    dsc.AppendLine(Lang.Get("compostingredux:compostbin-tooltip-aeration-anaerobic"));
                }
                else if (!processor.HasOptimalAeration)
                {
                    dsc.AppendLine(Lang.Get("compostingredux:compostbin-tooltip-aeration-low"));
                }

                // Show temperature status
                string temperatureState = processor.TemperatureState;
                float temperature = processor.Temperature;
                dsc.AppendLine(Lang.Get("compostingredux:compostbin-tooltip-temperature",
                    temperatureState, temperature.ToString("F0")));

                // Add temperature warnings or indicators
                if (processor.IsThermophilic)
                {
                    dsc.AppendLine(Lang.Get("compostingredux:compostbin-tooltip-temperature-thermophilic"));
                }
                else if (processor.IsTooCold)
                {
                    dsc.AppendLine(Lang.Get("compostingredux:compostbin-tooltip-temperature-too-cold"));
                }
                else if (processor.IsTooHot)
                {
                    dsc.AppendLine(Lang.Get("compostingredux:compostbin-tooltip-temperature-too-hot"));
                }
            }

            if (processor.TotalItemCount > 0 && !processor.IsFinished)
            {
                float elapsedDays = processor.ElapsedHours / 24f;
                float expectedTotalDays = (processor.ElapsedHours + processor.RemainingHours) / 24f;
                dsc.AppendLine(Lang.Get("compostingredux:compostbin-tooltip-composting-for",
                    elapsedDays.ToString("F1"),
                    expectedTotalDays.ToString("F1")));
            }
            else if (processor.IsFinished)
            {
                dsc.AppendLine(Lang.Get("compostingredux:compostbin-tooltip-composting-ready"));
            }
        }

        #endregion

        #region Serialization

        /// <summary>
        /// Saves the block entity state to the world save file.
        /// </summary>
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            // Save inventory
            var inventoryTree = new TreeAttribute();
            inventory.ToTreeAttributes(inventoryTree);
            tree["inventory"] = inventoryTree;

            // Save processor
            var processorTree = new TreeAttribute();
            processor.ToTreeAttributes(processorTree);
            tree["processor"] = processorTree;
        }

        /// <summary>
        /// Loads the block entity state from the world save file.
        /// </summary>
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolve)
        {
            base.FromTreeAttributes(tree, worldForResolve);

            // Create inventory if it doesn't exist
            if (inventory == null)
            {
                inventory = new CompostBinInventory("compostbin", $"{Pos.X}/{Pos.Y}/{Pos.Z}", Api);
            }

            // Check for old save format (had counts but no inventory)
            bool hasOldFormat = tree.HasAttribute("processor") && !tree.HasAttribute("inventory");

            // Store inventory data for reload after API is set
            if (tree.HasAttribute("inventory"))
            {
                savedInventoryTree = tree.GetTreeAttribute("inventory");
                // Also do initial load attempt (will be reloaded in Initialize with proper API)
                inventory.FromTreeAttributes(savedInventoryTree);
            }

            // Create processor if it doesn't exist (API might not be available yet)
            if (processor == null)
            {
                processor = new CompostProcessor(null, inventory);
            }

            // Load processor data
            if (tree.HasAttribute("processor"))
            {
                var processorTree = tree.GetTreeAttribute("processor");

                // If old format, check if it had items and reset to prevent errors
                if (hasOldFormat)
                {
                    // Old format detected - reset the compost bin
                    // (We can't migrate because we don't know which items were in there)
                    processor.Reset();

                    if (Api?.World != null && Api.Side == EnumAppSide.Server)
                    {
                        Api.Logger.Notification($"CompostingRedux: Reset compost bin at {Pos} due to mod update. Please re-add materials.");
                    }
                }
                else
                {
                    processor.FromTreeAttributes(processorTree);
                }
            }

            // Update block state if API is available
            if (Api?.World != null)
            {
                processor.SetApi(Api);
                processor.SetInventory(inventory);
                UpdateBlockState();
            }
        }

        #endregion
    }
}
