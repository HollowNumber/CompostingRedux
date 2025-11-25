using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;



using CompostingRedux.Utils;
using MeshUtil = CompostingRedux.Utils.MeshUtil;

namespace CompostingRedux.BlockEntities
{
    public class BlockEntityCompostBin : BlockEntity, IBlockEntityContainer, IRenderer
    {
        // --- Inventory Implementation ---
        internal InventoryGeneric inventory;
        public IInventory Inventory => inventory;
        public string InventoryClassName => "compostbin";

        // Required by IBlockEntityContainer
        public void DropContents(Vec3d atPos)
        {
            inventory.DropAll(atPos);
        }

        // --- Configuration ---
        private int MaxCapacity => CompostingReduxModSystem.Config.MaxCapacity;
        private int HoursToComplete => CompostingReduxModSystem.Config.HoursToComplete;

        // --- State ---
        private double startTime;
        private bool isComposting;
        private bool isFinished;
        private double lastTurnTime;
        private int lastItemAmount = -1; // Cache
        private bool lastIsFinished = false;

        // --- Rendering ---
        private MeshRef? contentMeshRef; // CHANGED: MeshData -> MeshRef

        private static readonly AssetLocation
            TextureRaw = new AssetLocation("game:block/soil/flooring/drypackeddirt1a");

        private static readonly AssetLocation TextureDone = new AssetLocation("game:block/soil/fertcompost");

        public double RenderOrder => 0.5;
        public int RenderRange => 24;

        public int ItemAmount
        {
            get
            {
                int count = 0;
                foreach (var slot in inventory)
                    if (!slot.Empty)
                        count += slot.StackSize;
                return count;
            }
        }

        public bool IsFinished => isFinished;

        // --- Assets ---
        private static readonly AssetLocation SoundSand = new AssetLocation("game:sounds/block/sand");
        private static readonly AssetLocation SoundCollect = new AssetLocation("game:sounds/player/collect2");
        private static readonly AssetLocation SoundDirtDig = new AssetLocation("game:sounds/block/dirt-dig");

        private static readonly AssetLocation SoundCompostFinished =
            new AssetLocation("game:sounds/effect/compost_finished");

        private static readonly AssetLocation ItemCompost = new AssetLocation("game:compost");

        public BlockEntityCompostBin()
        {
            inventory = new InventoryGeneric(4, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            inventory.LateInitialize("compostbin-" + Pos.ToString(), api);

            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnServerTick, 2000);
            }
            else
            {
                (api as ICoreClientAPI).Event.RegisterRenderer(this, EnumRenderStage.Opaque, "compostbin");
                UpdateMesh();
            }
        }


        private void OnServerTick(float dt)
        {
            if (!isComposting || isFinished) return;
            if (Api.World.Calendar.TotalHours - startTime >= HoursToComplete) FinishComposting();
        }

        private void FinishComposting()
        {
            isFinished = true;
            isComposting = false;
            int totalItems = ItemAmount;
            inventory.Clear();
            Item compostItem = Api.World.GetItem(ItemCompost);
            if (compostItem != null)
            {
                int outputCount = (int)(totalItems * CompostingReduxModSystem.Config.OutputPerItem);
                if (outputCount > 0) inventory[0].Itemstack = new ItemStack(compostItem, outputCount);
            }

            Api.World.PlaySoundAt(SoundCompostFinished, Pos.X, Pos.Y, Pos.Z);
            MarkDirty(true);
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            ItemSlot handSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (handSlot.Itemstack?.Collectible.Tool == EnumTool.Shovel)
                return HandleShovelInteract(byPlayer);

            
            if (handSlot.Empty) return TryTakeItem(byPlayer);
            if (IsCompostable(handSlot)) return TryPutItem(byPlayer, handSlot);

            return false;
        }

        // ... [Rest of interact logic matches previous snippet] ...
        private bool TryPutItem(IPlayer byPlayer, ItemSlot handSlot)
        {
            // 1. Capture state BEFORE adding
            int oldAmount = ItemAmount;
            double currentProgressHours = Api.World.Calendar.TotalHours - startTime;
            if (currentProgressHours < 0) currentProgressHours = 0;

            int quantityToMove = 1;
            if (byPlayer.Entity.Controls.CtrlKey)
            {
                quantityToMove = GameMath.Min(handSlot.StackSize, 8);
            }
            
            int movedTotal = 0;


            // 2. Add items
            foreach (var slot in inventory)
            {
                if (quantityToMove <= 0) break;

                if (slot.CanTakeFrom(handSlot))
                {
                    int moved = handSlot.TryPutInto(Api.World, slot, quantityToMove);
                    if (moved > 0)
                    {
                        movedTotal += moved;
                        quantityToMove -= moved;
                        slot.MarkDirty();
                    }
                }
            }

            if (movedTotal > 0)
            {
                handSlot.MarkDirty();
                Api.World.PlaySoundAt(SoundSand, Pos.X, Pos.Y, Pos.Z, byPlayer);

                int newAmount = oldAmount + movedTotal;

                if (!isComposting)
                {
                    // Start fresh
                    isComposting = true;
                    startTime = Api.World.Calendar.TotalHours;
                }
                else if (oldAmount > 0)
                {
                    // DILUTION LOGIC:
                    // Retain the "completed work" proportional to the old mass.
                    // EffectiveHours = OldHours * (OldMass / NewMass)

                    double ratio = (double)oldAmount / newAmount;
                    double effectiveHours = currentProgressHours * ratio;

                    // Reset start time so that (Now - Start) equals our new EffectiveHours
                    startTime = Api.World.Calendar.TotalHours - effectiveHours;
                }

                MarkDirty(true);
                return true;
            }

            return false;
        }

        private bool TryTakeItem(IPlayer byPlayer)
        {
            foreach (var slot in inventory)
            {
                if (!slot.Empty)
                {
                    ItemStack stack = slot.TakeOut(slot.StackSize);
                    if (byPlayer.InventoryManager.TryGiveItemstack(stack))
                    {
                        Api.World.PlaySoundAt(SoundCollect, Pos.X, Pos.Y, Pos.Z, byPlayer);
                        slot.MarkDirty();
                        if (inventory.Empty)
                        {
                            isFinished = false;
                            isComposting = false;
                        }

                        MarkDirty(true);
                        return true;
                    }
                    else
                    {
                        slot.Itemstack = stack;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HandleShovelInteract(IPlayer byPlayer)
        {
            if (!isComposting || isFinished) return false;
            if (Api.World.Calendar.TotalHours - lastTurnTime <
                CompostingReduxModSystem.Config.ShovelTurnCooldownHours) return true;
            if (Api.Side == EnumAppSide.Server)
            {
                startTime -= CompostingReduxModSystem.Config.ShovelSpeedupHours;
                lastTurnTime = Api.World.Calendar.TotalHours;
                if (Api.World.Calendar.TotalHours - startTime >= HoursToComplete) FinishComposting();
                else MarkDirty(true);
            }

            Api.World.PlaySoundAt(SoundDirtDig, Pos.X, Pos.Y, Pos.Z, byPlayer);
            return true;
        }


        private void UpdateMesh()
        {
            if (Api?.Side != EnumAppSide.Client) return;
            contentMeshRef?.Dispose(); contentMeshRef = null;
            if (inventory.Empty) return;

            ICoreClientAPI capi = Api as ICoreClientAPI;
            
            // 1. Try to find a valid source BLOCK
            Block sourceBlock;
            
            if (isFinished) sourceBlock = capi.World.GetBlock(new AssetLocation("game:soil-high-none"));
            else sourceBlock = capi.World.GetBlock(new AssetLocation("game:soil-verylow-none"));
            
            // Fallback 1: Normal Soil
            if (sourceBlock == null) sourceBlock = capi.World.GetBlock(new AssetLocation("game:soil-medium-none"));
            
            // Fallback 2: Cobblestone (Guaranteed to exist)
            if (sourceBlock == null) sourceBlock = capi.World.GetBlock(new AssetLocation("game:cobblestone-granite"));

            if (sourceBlock == null) return; // Should never happen

            // 2. Get texture from the block
            TextureAtlasPosition tex = capi.BlockTextureAtlas.GetPosition(sourceBlock, "up");
            
            // 3. Calculate Height
            float fillPercent = (float)ItemAmount / MaxCapacity;
            float height = fillPercent * 0.65f; 
            if (height < 0.05f) height = 0.05f;

            // 4. Generate Mesh
            MeshData mesh = MeshUtil.GetCubeMesh(0.15f, 0.063f, 0.15f, 0.85f, 0.063f + height, 0.85f, tex);
            
            contentMeshRef = capi.Render.UploadMesh(mesh);
        }
        
        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            
            if (ItemAmount != lastItemAmount || isFinished != lastIsFinished || contentMeshRef == null)
            {
                UpdateMesh();
                lastItemAmount = ItemAmount;
                lastIsFinished = isFinished;
            }
            
            if (contentMeshRef == null) return;

            ICoreClientAPI capi = Api as ICoreClientAPI;
            IRenderAPI rpi = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            rpi.GlToggleBlend(true);
            IStandardShaderProgram prog = rpi.PreparedStandardShader(Pos.X, Pos.Y, Pos.Z);

            float fillPercent = (float)ItemAmount / MaxCapacity;
            
            // VISUAL HEIGHT CALCULATION
            // 0.0625 is the bin floor thickness.
            // We want to start SLIGHTLY above it to prevent Z-fighting at 0 items (if we rendered it).
            float yStart = 0.063f; 
            
            // Max height is ~0.7 blocks.
            float visibleHeight = fillPercent * 0.7f;
            if (visibleHeight < 0.01f) visibleHeight = 0.01f;

            float[] modelMatrix = Matrixf.Create()
                .Translate(Pos.X - camPos.X, Pos.Y - camPos.Y, Pos.Z - camPos.Z)
                .Translate(0, yStart, 0)     // Move to floor
                .Values;

            prog.ModelMatrix = modelMatrix;
            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            rpi.RenderMesh(contentMeshRef);
            prog.Stop();
        }

        public void Dispose()
        {
            contentMeshRef?.Dispose();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if (Api?.Side == EnumAppSide.Client)
                (Api as ICoreClientAPI)?.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            contentMeshRef?.Dispose();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            if (Api?.Side == EnumAppSide.Client)
                (Api as ICoreClientAPI)?.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            contentMeshRef?.Dispose();
        }

        // --- Sync ---
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            inventory.ToTreeAttributes(tree);
            tree.SetDouble("startTime", startTime);
            tree.SetBool("isComposting", isComposting);
            tree.SetBool("isFinished", isFinished);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolve)
        {
            base.FromTreeAttributes(tree, worldForResolve);
            inventory.FromTreeAttributes(tree);
            startTime = tree.GetDouble("startTime");
            isComposting = tree.GetBool("isComposting");
            isFinished = tree.GetBool("isFinished");
            if (Api?.Side == EnumAppSide.Client) UpdateMesh();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
            if (Api.Side == EnumAppSide.Server) inventory.DropAll(Pos.ToVec3d().Add(0.5, 0.5, 0.5));
        }

        public bool IsCompostable(ItemSlot handSlot)
        {
            if (handSlot?.Itemstack == null) return false;
            string path = handSlot.Itemstack.Collectible.Code.Path;
            return path == "rot" || path.StartsWith("vegetable-") || path.StartsWith("grain-");
        }

        public class ContainerTextureSource : ITexPositionSource
        {
            private ICoreClientAPI capi;
            private AssetLocation texturePath;

            public ContainerTextureSource(ICoreClientAPI capi, AssetLocation texturePath)
            {
                this.capi = capi;
                this.texturePath = texturePath;
            }

            public TextureAtlasPosition this[string textureCode]
            {
                get { return capi.BlockTextureAtlas[texturePath]; }
            }

            public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            dsc.AppendLine($"Contents: {ItemAmount}/{MaxCapacity}");

            if (isFinished)
            {
                dsc.AppendLine("Compost Ready!");
            }
            else if (isComposting && ItemAmount > 0)
            {
                int elapsed = (int)(Api.World.Calendar.TotalHours - startTime);

                dsc.AppendLine($"Composting for: {elapsed}/{HoursToComplete}");
            }
            else if (ItemAmount > 0)
            {
                dsc.AppendLine("Waiting for more materials...");
            }
            else
            {
                dsc.AppendLine("Empty.");
            }
        }
    }
}