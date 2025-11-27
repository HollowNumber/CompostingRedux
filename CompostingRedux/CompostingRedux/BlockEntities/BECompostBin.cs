using System.Text;
using CompostingRedux.BlockEntities.Helpers;
using CompostingRedux.Configuration;
using Vintagestory.API.Common;
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

        private int itemAmount = 0;
        private double startTime = 0; // When composting started (in total hours)
        private bool isFinished = false;
        private double lastTurnTime = 0; // Last time the pile was turned

        private long? tickListenerId = null;
        private CompostBinFeedback feedback = null!;

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
        public int ItemAmount => itemAmount;

        /// <summary>
        /// Returns true if composting is complete.
        /// </summary>
        public bool IsFinished => isFinished;

        /// <summary>
        /// Calculate current progress based on elapsed time (0-100).
        /// </summary>
        public int CompostProgress
        {
            get
            {
                if (itemAmount == 0 || startTime == 0) return 0;

                double currentTime = Api.World.Calendar.TotalHours;
                double elapsedHours = currentTime - startTime;

                float progress = (float)(elapsedHours / HoursToComplete * 100f);

                return (int)GameMath.Min(100f, progress);
            }
        }

        /// <summary>
        /// Number of in-game hours remaining until composting is complete.
        /// </summary>
        public int RemainingHours
        {
            get
            {
                if (itemAmount == 0) return 0;
                return (int)GameMath.Max(0f, HoursToComplete - ElapsedHours);
            }
        }

        /// <summary>
        /// Returns true if enough time has passed since the last turn to allow turning again.
        /// </summary>
        public bool CanTurnPile
        {
            get
            {
                if (itemAmount == 0 || isFinished) return false;

                double currentTime = Api.World.Calendar.TotalHours;
                double timeSinceLastTurn = currentTime - lastTurnTime;

                return timeSinceLastTurn >= CompostingReduxModSystem.Config.ShovelSpeedupHours;
            }
        }

        /// <summary>
        /// Number of in-game hours that have elapsed since composting started.
        /// </summary>
        public int ElapsedHours
        {
            get
            {
                if (startTime == 0) return 0;
                return (int)(Api.World.Calendar.TotalHours - startTime);
            }
        }

        #endregion

        #region Initialization & Lifecycle

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            // Initialize feedback handler
            feedback = new CompostBinFeedback(api, Pos);

            // Only start ticking if we have active composting
            if (itemAmount > 0 && !isFinished)
            {
                StartCompostingTick();
            }
        }

        /// <summary>
        /// Called periodically to check if composting has completed.
        /// </summary>
        private void OnTick(float dt)
        {
            if (isFinished || itemAmount == 0 || startTime == 0) return;

            // Check if composting is complete
            if (CompostProgress >= 100f && !isFinished)
            {
                isFinished = true;
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
        /// Supports: adding items, turning pile with shovel, and harvesting finished compost.
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

            // Case 2: Adding Compostable Items
            if (config.IsCompostable(handSlot) && itemAmount < MaxCapacity)
            {
                return HandleAddItems(byPlayer, handSlot, config);
            }

            // Case 3: Finished and Empty Hand (Harvesting)
            if (isFinished && handSlot.Empty)
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
            if (itemAmount == 0 || isFinished) return false;

            // Check cooldown
            double currentTime = Api.World.Calendar.TotalHours;
            double timeSinceLastTurn = currentTime - lastTurnTime;

            if (timeSinceLastTurn < config.ShovelTurnCooldownHours)
            {
                feedback.ShowCooldownMessage(byPlayer, config.ShovelTurnCooldownHours - timeSinceLastTurn);
                return true;
            }

            // Advance composting time
            double hoursToAdd = config.ShovelSpeedupHours;
            startTime -= hoursToAdd;

            // Ensure we don't go back further than necessary to complete
            double maxBacktrack = Api.World.Calendar.TotalHours - HoursToComplete;
            if (startTime < maxBacktrack)
            {
                startTime = maxBacktrack;
            }

            lastTurnTime = currentTime;

            // Play feedback
            feedback.PlayDigSound(byPlayer);
            feedback.PlayShovelDigAnimation(byPlayer);
            feedback.SpawnDigParticlesBurst(isFinished);

            // Check if this turn completed the composting
            if (CompostProgress >= 100f)
            {
                isFinished = true;
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
                takeAmount = GameMath.Min(bulkAmount, GameMath.Min(handSlot.StackSize, MaxCapacity - itemAmount));
            }
            else
            {
                // Single add - add only 1 item
                takeAmount = GameMath.Min(1, MaxCapacity - itemAmount);
            }

            if (takeAmount == 0) return false;

            handSlot.TakeOut(takeAmount);
            itemAmount += takeAmount;

            // Start composting if this is the first addition
            if (startTime == 0)
            {
                startTime = Api.World.Calendar.TotalHours;
                StartCompostingTick();
            }

            feedback.PlayAddSound(byPlayer);
            UpdateBlockState();
            MarkDirty();

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

            int outputAmount = (int)(itemAmount * config.OutputPerItem);
            ItemStack outputStack = new ItemStack(compostItem, outputAmount);

            bool transferred = byPlayer.InventoryManager.TryGiveItemstack(outputStack, slotNotifyEffect: true);

            if (transferred)
            {
                // Only reset state if items were successfully transferred
                feedback.PlayHarvestSound(byPlayer);

                // Reset State
                itemAmount = 0;
                startTime = 0;
                isFinished = false;
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

        #endregion



        #region Block State Management

        /// <summary>
        /// Updates the visual block state based on fill level and doneness.
        /// </summary>
        private void UpdateBlockState()
        {
            if (Api?.World?.BlockAccessor == null) return;

            float fillRatio = (float)itemAmount / MaxCapacity;
            string newFillLevel = GetFillLevel(fillRatio);
            string newDonenessState = isFinished ? "done" : "raw";

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
                $"{itemAmount}/{MaxCapacity}"));

            if (itemAmount > 0 && !isFinished)
            {
                dsc.AppendLine(Lang.Get("compostingredux:compostbin-tooltip-composting-for",
                    (ElapsedHours / 24f).ToString("F1"),
                    (HoursToComplete / 24f).ToString("F1")));
            }
            else if (isFinished)
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
            tree.SetInt(nameof(itemAmount), itemAmount);
            tree.SetDouble(nameof(startTime), startTime);
            tree.SetBool(nameof(isFinished), isFinished);
            tree.SetDouble(nameof(lastTurnTime), lastTurnTime);
        }

        /// <summary>
        /// Loads the block entity state from the world save file.
        /// </summary>
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolve)
        {
            base.FromTreeAttributes(tree, worldForResolve);
            itemAmount = tree.GetInt(nameof(itemAmount));
            startTime = tree.GetDouble(nameof(startTime));
            isFinished = tree.GetBool(nameof(isFinished));
            lastTurnTime = tree.GetDouble(nameof(lastTurnTime));

            if (Api?.World != null)
            {
                UpdateBlockState();
            }
        }

        #endregion
    }
}
