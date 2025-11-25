using CompostingRedux.BlockEntities;
using CompostingRedux.Blocks;
using Vintagestory.API.Common;

namespace CompostingRedux;

public class CompostingReduxModSystem : ModSystem
{
    public static CompostConfig Config { get; private set; }
        
    public override void Start(ICoreAPI api)
    {
        base.Start(api);


        try
        {
            Config = api.LoadModConfig<CompostConfig>("compostingreduxconfig.json") ?? CompostConfig.GetDefault();
            api.Logger.Notification("CompostingRedux started successfully.");
        }
        catch
        {
            api.Logger.Error("CompostingRedux couldn't start.");
            Config = CompostConfig.GetDefault();
        }
            
        api.StoreModConfig(Config, "compostingreduxconfig.json");
            
        string modid = Mod.Info.ModID;
            
        api.RegisterBlockClass(modid + ".compostbinblock", typeof(BlockCompostBin)); 
            
        api.RegisterBlockEntityClass(modid + ".compostbinentity", typeof(BlockEntityCompostBin));
            
    }
}
