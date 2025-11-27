using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace CompostingRedux.Composting.Moisture
{
    /// <summary>
    /// Base class for block entities that need moisture management.
    /// Inherit from this class to automatically get moisture tracking,
    /// environmental updates, and serialization support.
    /// </summary>
    public abstract class MoistureBlockEntity : BlockEntity, IMoistureControllable
    {
        protected MoistureManager moistureManager;

        /// <summary>
        /// Gets the moisture manager instance.
        /// </summary>
        public MoistureManager MoistureManager => moistureManager;

        /// <summary>
        /// Gets the current moisture level (0.0 to 1.0).
        /// </summary>
        public float MoistureLevel => moistureManager.Level;

        /// <summary>
        /// Gets whether the moisture is in optimal range.
        /// </summary>
        public bool HasOptimalMoisture => moistureManager.IsOptimal;

        /// <summary>
        /// Gets whether the block is too dry.
        /// </summary>
        public bool IsTooDry => moistureManager.IsTooDry;

        /// <summary>
        /// Gets whether the block is too wet.
        /// </summary>
        public bool IsTooWet => moistureManager.IsTooWet;

        /// <summary>
        /// Gets a descriptive state of the moisture.
        /// </summary>
        public string MoistureState => moistureManager.State;

        public MoistureBlockEntity()
        {
            moistureManager = new MoistureManager();
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            moistureManager.SetApi(api);
            moistureManager.SetBlockPos(Pos);
        }

        /// <summary>
        /// Adds water to increase moisture level.
        /// </summary>
        public virtual void AddWater(float amount)
        {
            moistureManager.AddWater(amount);
            MarkDirty();
        }

        /// <summary>
        /// Adds dry material to decrease moisture level.
        /// </summary>
        public virtual void AddDryMaterial(float amount)
        {
            moistureManager.AddDryMaterial(amount);
            MarkDirty();
        }

        /// <summary>
        /// Updates moisture based on environmental conditions.
        /// Call this from your Update/OnTick method.
        /// </summary>
        public virtual void UpdateMoisture()
        {
            if (Api?.World == null) return;
            double currentTime = Api.World.Calendar.TotalHours;
            moistureManager.UpdateEnvironmental(currentTime);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            moistureManager.ToTreeAttributes(tree);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            moistureManager.FromTreeAttributes(tree);
        }
    }
}