using System;
using CompostingRedux.BlockEntities;
using CompostingRedux.Blocks;
using CompostingRedux.Configuration;
using Vintagestory.API.Common;

namespace CompostingRedux
{
    /// <summary>
    /// Main mod system for Composting Redux.
    /// Handles mod initialization, configuration loading, and class registration.
    /// </summary>
    public class CompostingReduxModSystem : ModSystem
    {
        /// <summary>
        /// Global configuration instance for the mod.
        /// Loaded from 'compostingreduxconfig.json' in the ModConfig folder.
        /// </summary>
        public static CompostConfig Config { get; private set; } = null!;

        /// <summary>
        /// Called when the mod starts. Loads configuration and registers block/entity classes.
        /// </summary>
        /// <param name="api">Core API instance (works on both client and server)</param>
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            LoadConfiguration(api);
            RegisterClasses(api);
        }

        /// <summary>
        /// Loads the mod configuration from disk or creates it with default values.
        /// </summary>
        private void LoadConfiguration(ICoreAPI api)
        {
            try
            {
                Config = api.LoadModConfig<CompostConfig>("compostingreduxconfig.json") ?? CompostConfig.GetDefault();
                api.Logger.Notification("CompostingRedux config loaded successfully.");
            }
            catch (Exception ex)
            {
                api.Logger.Error("CompostingRedux failed to load config: {0}", ex.Message);
                api.Logger.Notification("CompostingRedux using default config values.");
                Config = CompostConfig.GetDefault();
            }

            // Save the config to ensure it exists o6n disk with current schema
            api.StoreModConfig(Config, "compostingreduxconfig.json");
        }

        /// <summary>
        /// Registers custom block and block entity classes with the game.
        /// </summary>
        private void RegisterClasses(ICoreAPI api)
        {
            string modid = Mod.Info.ModID;

            api.RegisterBlockClass(modid + ".compostbinblock", typeof(BlockCompostBin));
            api.RegisterBlockEntityClass(modid + ".compostbinentity", typeof(BlockEntityCompostBin));
        }
    }
}