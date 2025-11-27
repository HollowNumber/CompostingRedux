using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace CompostingRedux.Composting.Interfaces
{
    /// <summary>
    /// Interface for providing standardized audio and visual feedback for block entities.
    /// Handles particles, sounds, and other sensory feedback to improve player experience.
    /// </summary>
    public interface IFeedbackProvider
    {
        /// <summary>
        /// Plays a sound effect at the block's position.
        /// </summary>
        /// <param name="soundPath">Asset path to the sound (e.g., "game:sounds/effect")</param>
        /// <param name="randomizePitch">Whether to randomize pitch for variety</param>
        void PlaySound(string soundPath, bool randomizePitch = true);

        /// <summary>
        /// Spawns particle effects at the specified position.
        /// </summary>
        /// <param name="position">World position to spawn particles</param>
        /// <param name="particleType">Type of particle effect to spawn</param>
        /// <param name="count">Number of particles to spawn</param>
        void SpawnParticles(Vec3d position, string particleType, int count = 10);

        /// <summary>
        /// Provides feedback when an item is successfully added.
        /// </summary>
        /// <param name="player">The player who added the item</param>
        void OnItemAdded(IPlayer player);

        /// <summary>
        /// Provides feedback when an item is successfully harvested.
        /// </summary>
        /// <param name="player">The player who harvested</param>
        void OnItemHarvested(IPlayer player);

        /// <summary>
        /// Provides feedback when the pile is turned.
        /// </summary>
        /// <param name="player">The player who turned the pile</param>
        void OnPileTurned(IPlayer player);

        /// <summary>
        /// Provides feedback when an action fails (e.g., bin is full, not ready).
        /// </summary>
        /// <param name="player">The player who attempted the action</param>
        /// <param name="message">The failure message to display</param>
        void OnActionFailed(IPlayer player, string message);
    }
}