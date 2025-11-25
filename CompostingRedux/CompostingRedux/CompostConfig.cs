
namespace CompostingRedux
{
    public class CompostConfig
    {
        // Capacity
        public int MaxCapacity { get; set; } = 64;
        public int BulkAddAmount { get; set; } = 4; // How many items can be added at once
        
        // Timing
        public int HoursToComplete { get; set; } = 240;
        public int ShovelSpeedupHours { get; set; } = 5;
        
        public int ShovelTurnCooldownHours { get; set; } = 5; // Cooldown between shovel turns in seconds
        
        // Fill Level Thresholds (0.0 to 1.0)
        public float FillLevel2Threshold { get; set; } = 0.01f;
        public float FillLevel4Threshold { get; set; } = 0.3f;
        public float FillLevel6Threshold { get; set; } = 0.6f;
        public float FillLevel8Threshold { get; set; } = 0.95f;
        
        // Output
        public float OutputPerItem { get; set; } = 0.5f; // How many soil blocks per rot item
        

        
        public CompostConfig()
        {
        }
        
        // Make a copy with default values
        public static CompostConfig GetDefault()
        {
            return new CompostConfig();
        }
    }
}