using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace CompostingRedux.BlockEntities.Helpers
{
    /// <summary>
    /// Handles audio and visual feedback for the compost bin.
    /// Provides methods for playing sounds, animations, and spawning particles.
    /// </summary>
    public class CompostBinFeedback
    {
        private readonly ICoreAPI api;
        private readonly BlockPos position;

        /// <summary>
        /// Creates a new feedback handler for a compost bin.
        /// </summary>
        /// <param name="api">Core API instance</param>
        /// <param name="position">Position of the compost bin in the world</param>
        public CompostBinFeedback(ICoreAPI api, BlockPos position)
        {
            this.api = api;
            this.position = position;
        }

        #region Sound

        /// <summary>
        /// Plays a sound at the compost bin position (client-side only).
        /// </summary>
        /// <param name="assetPath">Asset path of the sound to play</param>
        /// <param name="player">Player who triggered the sound (optional)</param>
        public void PlaySound(string assetPath, IPlayer? player = null)
        {
            if (!IsClient) return;

            api.World.PlaySoundAt(new AssetLocation(assetPath), position.X, position.Y, position.Z, player);
        }

        /// <summary>
        /// Plays the dirt digging sound.
        /// </summary>
        public void PlayDigSound(IPlayer? player = null)
        {
            PlaySound(CompostBinConstants.SoundDirtDig, player);
        }

        /// <summary>
        /// Plays the sand/adding sound.
        /// </summary>
        public void PlayAddSound(IPlayer? player = null)
        {
            PlaySound(CompostBinConstants.SoundSand, player);
        }

        /// <summary>
        /// Plays the collection/harvest sound.
        /// </summary>
        public void PlayHarvestSound(IPlayer? player = null)
        {
            PlaySound(CompostBinConstants.SoundCollect, player);
        }

        #endregion

        #region Animation

        /// <summary>
        /// Plays the shovel digging animation for the player (client-side only).
        /// </summary>
        /// <param name="player">The player to animate</param>
        public void PlayShovelDigAnimation(IPlayer player)
        {
            if (!IsClient || player?.Entity == null) return;

            player.Entity.AnimManager.StartAnimation(new AnimationMetaData()
            {
                Code = CompostBinConstants.AnimShovelDig,
                Animation = CompostBinConstants.AnimShovelDig,
                AnimationSpeed = 1.5f,
                Weight = 10,
                BlendMode = EnumAnimationBlendMode.Average,
                EaseInSpeed = 999f,      // Start immediately
                EaseOutSpeed = 999f,     // End immediately
                TriggeredBy = new AnimationTrigger()
                {
                    OnControls = new[] { EnumEntityActivity.Idle },
                    MatchExact = false
                }
            }.Init());

            // Stop the animation after it completes
            api.World.RegisterCallback((dt) =>
            {
                player.Entity?.AnimManager.StopAnimation(CompostBinConstants.AnimShovelDig);
            }, CompostBinConstants.AnimDurationMs);
        }

        #endregion

        #region Particles

        /// <summary>
        /// Spawns dirt/compost particles repeatedly during the digging animation (client-side only).
        /// </summary>
        /// <param name="isFinished">Whether the compost is finished (affects particle color)</param>
        public void SpawnDigParticlesBurst(bool isFinished)
        {
            if (!IsClient || api.World == null) return;

            var clientApi = api as ICoreClientAPI;
            if (clientApi == null) return;

            // Spawn particles multiple times during the animation to match digging motion
            int delayBetweenBursts = CompostBinConstants.AnimDurationMs / CompostBinConstants.ParticleBurstCount;

            for (int i = 0; i < CompostBinConstants.ParticleBurstCount; i++)
            {
                int burstIndex = i; // Capture for lambda
                api.World.RegisterCallback((dt) =>
                {
                    SpawnDigParticles(isFinished);
                }, delayBetweenBursts * burstIndex);
            }
        }

        /// <summary>
        /// Spawns a single burst of dirt/compost particles (client-side only).
        /// </summary>
        /// <param name="isFinished">Whether the compost is finished (affects particle color)</param>
        private void SpawnDigParticles(bool isFinished)
        {
            if (!IsClient || api.World == null) return;

            var clientApi = api as ICoreClientAPI;
            if (clientApi == null) return;

            // Determine particle color based on doneness
            int color = isFinished 
                ? ColorUtil.ToRgba(CompostBinConstants.ParticleColorAlpha, 
                    CompostBinConstants.ParticleColorFinishedR, 
                    CompostBinConstants.ParticleColorFinishedG, 
                    CompostBinConstants.ParticleColorFinishedB)
                : ColorUtil.ToRgba(CompostBinConstants.ParticleColorAlpha, 
                    CompostBinConstants.ParticleColorRawR, 
                    CompostBinConstants.ParticleColorRawG, 
                    CompostBinConstants.ParticleColorRawB);

            // Spawn particles from the center of the bin
            Vec3d centerPos = new Vec3d(position.X + 0.5, position.Y + 0.4, position.Z + 0.5);

            clientApi.World.SpawnParticles(new SimpleParticleProperties()
            {
                MinPos = centerPos,
                AddPos = new Vec3d(0.4, 0.2, 0.4),
                MinVelocity = new Vec3f(-0.5f, 0.8f, -0.5f),
                AddVelocity = new Vec3f(1.0f, 1.5f, 1.0f),
                MinQuantity = CompostBinConstants.ParticleMinQuantity,
                AddQuantity = CompostBinConstants.ParticleAddQuantity,
                Color = color,
                GravityEffect = CompostBinConstants.ParticleGravity,
                LifeLength = CompostBinConstants.ParticleLifeLength,
                MinSize = CompostBinConstants.ParticleMinSize,
                MaxSize = CompostBinConstants.ParticleMaxSize,
                ShouldDieInLiquid = true,
                ParticleModel = EnumParticleModel.Cube
            });
        }

        #endregion

        #region Messages

        /// <summary>
        /// Shows a cooldown error message to the player (client-side only).
        /// </summary>
        /// <param name="player">The player to show the message to</param>
        /// <param name="hoursRemaining">Number of hours remaining in cooldown</param>
        public void ShowCooldownMessage(IPlayer player, double hoursRemaining)
        {
            if (!IsClient) return;

            (api as ICoreClientAPI)?.TriggerIngameError(null, 
                CompostBinConstants.ErrorKeyCooldown,
                Lang.Get("compostingredux:pile-needs-settle", (int)hoursRemaining));
        }

        /// <summary>
        /// Shows an inventory full error message to the player (client-side only).
        /// </summary>
        /// <param name="player">The player to show the message to</param>
        public void ShowInventoryFullMessage(IPlayer player)
        {
            if (!IsClient) return;

            (api as ICoreClientAPI)?.TriggerIngameError(null, 
                CompostBinConstants.ErrorKeyInventoryFull,
                Lang.Get("game:ingameerror-inventoryfull"));
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Returns true if this is running on the client side.
        /// </summary>
        private bool IsClient => api.Side == EnumAppSide.Client;

        #endregion
    }
}