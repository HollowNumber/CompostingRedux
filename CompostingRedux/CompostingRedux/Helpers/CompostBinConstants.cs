namespace CompostingRedux.Helpers
{
    /// <summary>
    /// Constants used by the compost bin system.
    /// Centralizes all magic strings and numbers for easier maintenance.  
    /// </summary>
    public static class CompostBinConstants
    {
        #region Sound Asset Paths

        /// <summary>  
        /// Sound played when turning the pile with a shovel.  
        /// </summary> 
        public const string SoundDirtDig = "game:sounds/block/dirt1";

        /// <summary> 
        /// Sound played when adding items to the compost bin.
        /// </summary>
        public const string SoundSand = "game:sounds/block/sand";

        /// <summary>
        /// Sound played when harvesting finished compost.
        /// </summary>
        public const string SoundCollect = "game:sounds/player/collect2";

        #endregion

        #region Item Asset Paths

        /// <summary>
        /// Asset location for the compost item produced by the bin.
        /// </summary>
        public const string ItemCompost = "game:compost";

        #endregion

        #region Animation Identifiers

        /// <summary>
        /// Animation code for the shovel digging action.
        /// </summary>
        public const string AnimShovelDig = "shoveldig-fp";

        /// <summary>
        /// Duration of the shovel dig animation in milliseconds.
        /// </summary>
        public const int AnimDurationMs = 3000;

        #endregion

        #region Timing

        /// <summary>
        /// Interval between tick updates in milliseconds.
        /// </summary>
        public const int TickIntervalMs = 10000; // 10 seconds

        #endregion

        #region Particle Settings

        /// <summary>
        /// Number of particle bursts to spawn during the dig animation.
        /// </summary>
        public const int ParticleBurstCount = 6;

        /// <summary>
        /// Number of particle bursts for a single sound burst effect.
        /// </summary>
        public const int SoundBurstParticleCount = 2;

        /// <summary>
        /// Delay between particle bursts in a sound burst effect (in milliseconds).
        /// </summary>
        public const int SoundBurstDelayMs = 150;

        /// <summary>
        /// Minimum number of particles per burst.
        /// </summary>
        public const int ParticleMinQuantity = 8;

        /// <summary>
        /// Additional random particles per burst.
        /// </summary>
        public const int ParticleAddQuantity = 5;

        /// <summary>
        /// How long each particle lives in seconds.
        /// </summary>
        public const float ParticleLifeLength = 1.5f;

        /// <summary>
        /// Minimum size of particles.
        /// </summary>
        public const float ParticleMinSize = 0.2f;

        /// <summary>
        /// Maximum size of particles.
        /// </summary>
        public const float ParticleMaxSize = 0.5f;

        /// <summary>
        /// Gravity effect on particles (1.0 = normal gravity).
        /// </summary>
        public const float ParticleGravity = 1.0f;

        #endregion

        #region Particle Colors

        /// <summary>
        /// RGB values for finished compost particle color (rich brown).
        /// </summary>
        public const int ParticleColorFinishedR = 139;
        public const int ParticleColorFinishedG = 69;
        public const int ParticleColorFinishedB = 20;

        /// <summary>
        /// RGB values for raw compost particle color (darker brown).
        /// </summary>
        public const int ParticleColorRawR = 101;
        public const int ParticleColorRawG = 67;
        public const int ParticleColorRawB = 45;

        /// <summary>
        /// Alpha value for particle colors.
        /// </summary>
        public const int ParticleColorAlpha = 200;

        #endregion

        #region Error Keys

        /// <summary>
        /// Error key for cooldown messages.
        /// </summary>
        public const string ErrorKeyCooldown = "compostcooldown";

        /// <summary>
        /// Error key for inventory full messages.
        /// </summary>
        public const string ErrorKeyInventoryFull = "inventoryfull";

        #endregion
    }
}
