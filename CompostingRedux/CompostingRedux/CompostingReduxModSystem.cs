using CompostingRedux.BlockEntities;
using CompostingRedux.Blocks;
using CompostingRedux.Config;
using Vintagestory.API.Common;

namespace CompostingRedux;

public class CompostingReduxModSystem : ModSystem
{
    
    public static CompostingReduxConfig Config { get; private set; }
        
    public override void Start(ICoreAPI api)
    {
        base.Start(api);


        try
        {
            Config = api.LoadModConfig<CompostingReduxConfig>("compostingreduxconfig.json") ?? CompostingReduxConfig.GetDefault();
            api.Logger.Notification("CompostingRedux started successfully.");
        }
        catch
        {
            api.Logger.Error("CompostingRedux couldn't start.");
            Config = CompostingReduxConfig.GetDefault();
        }
            
        api.StoreModConfig(Config, "compostingreduxconfig.json");
            
        string modid = Mod.Info.ModID;
            
        api.RegisterBlockClass(modid + ".compostbinblock", typeof(BlockCompostBin)); 
            
        api.RegisterBlockEntityClass(modid + ".compostbinentity", typeof(BlockEntityCompostBin));
            
    }
}
