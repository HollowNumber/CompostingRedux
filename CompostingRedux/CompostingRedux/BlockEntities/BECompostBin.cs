using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace CompostingRedux.BlockEntities
{
    public class BlockEntityCompostBin : BlockEntity
    {
        private int MaxCapacity => CompostingReduxModSystem.Config.MaxCapacity;
        private int HoursToComplete = CompostingReduxModSystem.Config.HoursToComplete; // Total hours to finish composting

        private int itemAmount = 0;
        private double startTime = 0; // Store when composting started (in total hours)
        private bool isFinished = false;
        private double lastTurnTime = 0; // Last time the pile was turned

        // Public accessors
        public int ItemAmount => itemAmount;
        public bool IsFinished => isFinished;

        // Calculate current progress based on elapsed time
        public int CompostProgress
        {
            get
            {
                if (itemAmount == 0 || startTime == 0) return 0;

                double currentTime = Api.World.Calendar.TotalHours;
                double elapsedHours = currentTime - startTime;

                // Constant time - always 100 hours
                float progress = (float)(elapsedHours / HoursToComplete * 100f);

                return (int)GameMath.Min(100f, progress);
            }
        }

        public int RemainingHours
        {
            get
            {
                if (itemAmount == 0) return 0;
                return (int)GameMath.Max(0f, HoursToComplete - ElapsedHours);
            }
        }

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

        public int ElapsedHours
        {
            get
            {
                if (startTime == 0) return 0;
                return (int)(Api.World.Calendar.TotalHours - startTime);
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            RegisterGameTickListener(OnTick, 1000); // Check every second
        }

        private void OnTick(float dt)
        {
            if (isFinished || itemAmount == 0 || startTime == 0) return;

            // Check if composting is complete
            if (CompostProgress >= 100f && !isFinished)
            {
                isFinished = true;
                UpdateBlockState();
                Api.World.PlaySoundAt(new AssetLocation("game:sounds/effect/compost_finished"), Pos.X, Pos.Y, Pos.Z);
                MarkDirty();
            }
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            ItemSlot handSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            // Case 1: Player is holding a Shovel (Turning the pile)
            if (handSlot.Itemstack?.Collectible.Tool == EnumTool.Shovel)
            {
                if (itemAmount == 0 || isFinished) return false;

                // Check cooldown
                double currentTime = Api.World.Calendar.TotalHours;
                double timeSinceLastTurn = currentTime - lastTurnTime;

                if (timeSinceLastTurn < CompostingReduxModSystem.Config.ShovelTurnCooldownHours)
                {
                    // Still on cooldown - show message
                    double hoursRemaining = CompostingReduxModSystem.Config.ShovelTurnCooldownHours - timeSinceLastTurn;
                    (Api as ICoreClientAPI)?.ShowChatMessage(
                        $"The pile needs to settle. Wait {(int)hoursRemaining} more hours."
                    );
                    return true;
                }

                // Advance time by config amount
                double hoursToAdd = CompostingReduxModSystem.Config.ShovelSpeedupHours;
                startTime -= hoursToAdd;

                if (startTime < 0) startTime = 0;

                lastTurnTime = currentTime; // Track when pile was turned

                Api.World.PlaySoundAt(new AssetLocation("game:sounds/block/dirt-dig"), Pos.X, Pos.Y, Pos.Z, byPlayer);
                // Play animation
                if (byPlayer?.Entity != null)
                {
                    byPlayer.Entity.AnimManager.StartAnimation(new AnimationMetaData()
                    {
                        Code = "shoveldig-fp",
                        Animation = "shoveldig-fp",
                        AnimationSpeed = 1.5f,
                        Weight = 10,
                        BlendMode = EnumAnimationBlendMode.Average,
                        EaseInSpeed = 999f,      // Start immediately
                        EaseOutSpeed = 999f,     // End immediately
                        TriggeredBy = new AnimationTrigger()
                        {
                            OnControls = [EnumEntityActivity.Idle],
                            MatchExact = false
                        }
                    }.Init());
    
                    // Stop the animation after it completes (optional, for extra control)
                    Api.World.RegisterCallback((dt) => 
                    {
                        byPlayer.Entity?.AnimManager.StopAnimation("shoveldig-fp");
                    }, 3000); // Adjust time in ms based on animation length
                }
                
                
                if (CompostProgress >= 100f)
                {
                    isFinished = true;
                    UpdateBlockState();
                }

                MarkDirty(true);
                return true;
            }

            // Case 2: Adding Compostable Items
            if (IsCompostable(handSlot) && itemAmount < MaxCapacity)
            {
                bool isSneaking = byPlayer.Entity.Controls.CtrlKey;

                int takeAmount;
                if (isSneaking)
                {
                    int bulkAmount = CompostingReduxModSystem.Config.BulkAddAmount;
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
                }

                Api.World.PlaySoundAt(new AssetLocation("game:sounds/block/sand"), Pos.X, Pos.Y, Pos.Z, byPlayer);

                UpdateBlockState();
                MarkDirty();
                return true;
            }

            // Case 3: Finished and Empty Hand (Harvesting)
            if (isFinished && handSlot.Empty)
            {
                Item compostItem = Api.World.GetItem(new AssetLocation("game", "compost"));

                if (compostItem == null)
                {
                    Api.Logger.Error("Compost item 'game:compost' not found in asset database! Cannot harvest.");
                    return true;
                }

                int outputAmount = (int)(itemAmount * CompostingReduxModSystem.Config.OutputPerItem);
                ItemStack outputStack = new ItemStack(compostItem, outputAmount);

                bool transferred = byPlayer.InventoryManager.TryGiveItemstack(outputStack, slotNotifyEffect: true);
                Api.World.PlaySoundAt(new AssetLocation("game:sounds/player/collect2"), Pos.X, Pos.Y, Pos.Z, byPlayer);

                // Reset State
                itemAmount = 0;
                startTime = 0;
                isFinished = false;
                UpdateBlockState();
                MarkDirty();
                
                return transferred; 
            }

            return false;
        }

        private void UpdateBlockState()
        {
            // Calculate new Fill Level
            float fillRatio = (float)itemAmount / MaxCapacity;
            string newFillLevel = "0";

            if (fillRatio > CompostingReduxModSystem.Config.FillLevel2Threshold) newFillLevel = "2";
            if (fillRatio > CompostingReduxModSystem.Config.FillLevel4Threshold) newFillLevel = "4";
            if (fillRatio > CompostingReduxModSystem.Config.FillLevel6Threshold) newFillLevel = "6";
            if (fillRatio >= CompostingReduxModSystem.Config.FillLevel8Threshold) newFillLevel = "8";

            // Determine Doneness State
            string newDonenessState = isFinished ? "done" : "raw";

            Block block = Api.World.BlockAccessor.GetBlock(Pos);

            if (block.Code.Path.EndsWith($"{newFillLevel}-{newDonenessState}"))
            {
                return;
            }

            AssetLocation ctx = block.CodeWithParts(newFillLevel, newDonenessState);

            Api.Logger.Debug($"Compost Bin at {Pos} changing state to {ctx}");

            block = Api.World.GetBlock(ctx);

            Api.World.BlockAccessor.ExchangeBlock(block.Id, Pos);

            Api.Logger.Debug(
                $"Compost Bin at {Pos} updated to Fill Level: {newFillLevel}, Doneness State: {newDonenessState}");
        }

        // Display hover text (timer and progress)
        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            dsc.AppendLine($"Contents: \n {itemAmount}/{MaxCapacity}");

            if (itemAmount > 0 && !isFinished)
            {
                // Show constant time regardless of fill
                dsc.AppendLine($"Composting for {ElapsedHours}/{HoursToComplete} hours");
            }
            else if (isFinished)
            {
                dsc.AppendLine("Compost ready!");
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("itemAmount", itemAmount);
            tree.SetDouble("startTime", startTime);
            tree.SetBool("isFinished", isFinished);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolve)
        {
            base.FromTreeAttributes(tree, worldForResolve);
            itemAmount = tree.GetInt("itemAmount");
            startTime = tree.GetDouble("startTime");
            isFinished = tree.GetBool("isFinished");

            if (Api?.World != null)
            {
                UpdateBlockState();
            }
        }

        public bool IsCompostable(ItemSlot? handSlot)
        {
            if (handSlot == null || handSlot.Empty)
            {
                return false;
            }

            CollectibleObject collectibles = handSlot.Itemstack.Collectible;

            string path = collectibles.Code.Path;

            return path == "rot" 
                   || path.StartsWith("vegetable-") 
                   || path.StartsWith("grain-");
        }
    }
}