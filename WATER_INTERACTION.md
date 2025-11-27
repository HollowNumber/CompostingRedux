# Water/Liquid Container Interaction Documentation

## Overview

The compost bin now supports manual watering using liquid containers from Vintage Story. This allows players to actively manage moisture levels in their compost piles, especially important in dry biomes or for indoor composting.

## Supported Containers

### 1. Water Portions
- **Item**: `game:waterportion`
- **Most common** liquid container in Vintage Story
- Created by filling a bowl with water
- **Effect**: Each portion adds **10% moisture** to the compost pile
- **Consumed**: Yes - water portion is consumed on use
- **Sound**: Large splash effect
- **Particles**: Water splash particles spawn on the pile

### 2. Water Buckets (Planned)
- **Item**: Wooden/metal buckets filled with water
- **Detection**: Checks for `LiquidCode == "water"` in block properties
- **Effect**: Each bucket adds **20% moisture** to the compost pile
- **Consumed**: Not yet - bucket is not consumed (requires more complex inventory interaction)
- **Sound**: Large splash effect
- **Particles**: Water splash particles

### 3. Future Containers
The system is designed to detect any liquid container with water:
- Watering cans
- Clay vessels with water
- Any item with `"water"` in the path or `LiquidCode`

## How It Works

### Player Interaction Flow

1. **Player holds water container** (e.g., water portion)
2. **Right-clicks on compost bin** with active composting
3. **System checks**:
   - Is the bin actively composting? (has items, not finished)
   - Is the container a water portion?
   - Or does the container have water (`LiquidCode == "water"`)?
4. **If valid**:
   - Moisture level increases by specified amount
   - Water container is consumed (portions only)
   - Splash sound plays
   - Water particles spawn on pile
   - Tooltip updates to show new moisture level

### Code Implementation

```csharp
private bool TryWaterCompost(IPlayer byPlayer, ItemSlot handSlot)
{
    if (handSlot?.Itemstack == null) return false;
    
    ItemStack heldStack = handSlot.Itemstack;
    string itemPath = heldStack.Collectible.Code.Path;
    
    // Check for water portion
    if (itemPath.StartsWith("waterportion"))
    {
        int waterPortionsUsed = GameMath.Min(1, heldStack.StackSize);
        processor.AddWater(0.1f * waterPortionsUsed); // 10% per portion
        
        handSlot.TakeOut(waterPortionsUsed);
        handSlot.MarkDirty();
        
        // Feedback
        Api.World.PlaySoundAt(new AssetLocation("sounds/environment/largesplash"), Pos, 0, byPlayer, false);
        SpawnWaterParticles();
        
        MarkDirty();
        return true;
    }
    
    // Check for buckets/other containers
    Block block = heldStack.Block;
    if (block != null)
    {
        string liquidCode = block.LiquidCode;
        bool hasWater = liquidCode == "water" || itemPath.Contains("water");
        
        if (hasWater)
        {
            processor.AddWater(0.2f); // 20% for buckets
            
            // Feedback
            Api.World.PlaySoundAt(new AssetLocation("sounds/environment/largesplash"), Pos, 0, byPlayer, false);
            SpawnWaterParticles();
            
            MarkDirty();
            return true;
        }
    }
    
    return false;
}
```

## Moisture Management

### CompostProcessor Methods

```csharp
/// <summary>
/// Manually adds water to the compost pile.
/// </summary>
public void AddWater(float amount = 0.2f)
{
    moistureLevel = GameMath.Clamp(moistureLevel + amount, 0f, 1f);
}

/// <summary>
/// Manually adds dry material to reduce moisture.
/// </summary>
public void AddDryMaterial(float amount = 0.15f)
{
    moistureLevel = GameMath.Clamp(moistureLevel - amount, 0f, 1f);
}
```

### Clamping
- Moisture is always clamped between **0.0** (bone dry) and **1.0** (waterlogged)
- Adding water when already at 100% has no effect
- Prevents overflow/underflow

## Visual & Audio Feedback

### Sound Effect
- **Asset**: `sounds/environment/largesplash`
- Plays at bin position when water is added
- Audible to nearby players

### Particle Effect
```csharp
private void SpawnWaterParticles()
{
    Vec3d pos = Pos.ToVec3d().Add(0.5, 0.5, 0.5);
    
    SimpleParticleProperties waterParticles = new SimpleParticleProperties(
        1, 3, 
        ColorUtil.ToRgba(180, 100, 150, 220), // Bluish-grey water
        pos.Add(-0.25, 0, -0.25),              // Min position
        pos.Add(0.25, 0.1, 0.25),              // Max position
        new Vec3f(-0.5f, -0.5f, -0.5f),        // Min velocity
        new Vec3f(0.5f, 0.5f, 0.5f),           // Max velocity
        0.5f, 0.5f,                            // Life span
        0.25f, 0.5f,                           // Size
        EnumParticleModel.Quad
    );
    
    waterParticles.MinQuantity = 5;
    waterParticles.AddQuantity = 10;
    waterParticles.GravityEffect = 0.8f;
    waterParticles.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.3f);
    
    Api.World.SpawnParticles(waterParticles);
}
```

**Effect**: 5-15 water droplet particles spawn and fall on the compost pile

## Player Experience

### Use Cases

1. **Desert Biome Composting**
   - Evaporation is very high (hot + dry air)
   - Player must manually water every few game days
   - Water portions are consumed regularly

2. **Indoor Composting**
   - No rain exposure → no automatic moisture gain
   - Relies entirely on manual watering
   - More controlled but requires attention

3. **Emergency Moisture Management**
   - Pile becomes too dry during summer heat
   - Player adds water to restore decomposition speed
   - Immediate feedback via tooltip

4. **Optimization Strategy**
   - Monitor tooltip: "Moisture: Too Dry (25%)"
   - Add 2-3 water portions to reach optimal range
   - Decomposition speed increases from 0.5x to 1.0x

### Interaction Hint

When hovering over an active compost bin, players see:

```
[Right-click with water portion icon] Water pile
```

This appears alongside:
- Add (with compostable items)
- Turn pile with shovel
- Harvest compost (when finished)

## Integration with Moisture System

### How They Work Together

1. **Automatic System** (environmental)
   - Rain adds moisture when exposed
   - Evaporation removes moisture over time
   - Climate affects rates (temperature, humidity)

2. **Manual System** (player-controlled)
   - Water containers add moisture instantly
   - Player decides when to water
   - Overrides environmental state

3. **Combined Effect**
   - Both systems update the same `moistureLevel` field
   - Manual watering can compensate for harsh climates
   - Rain can maintain bins in temperate regions

### Example Scenarios

**Scenario 1: Desert Bin**
- Evaporation: -5% per hour
- Rain: None (dry biome)
- Player action: Water with 2 portions every 4 hours
- Result: Moisture maintained at 40-60% (optimal)

**Scenario 2: Rainforest Bin**
- Evaporation: -1% per hour (humid)
- Rain: +3% per hour (frequent)
- Player action: None needed
- Result: Risk of waterlogging, player might need drainage

**Scenario 3: Indoor Bin**
- Evaporation: -2% per hour (normal temp)
- Rain: None (covered)
- Player action: Water with 1 portion every 2-3 hours
- Result: Controlled moisture, optimal composting

## Future Enhancements

### Planned Features

1. **Bucket Water Consumption**
   - Actually consume water from buckets when used
   - Requires interaction with bucket inventory system
   - Leave empty bucket in player's hand

2. **Watering Can Support**
   - Detect watering cans specifically
   - Use watering can's stored water
   - More efficient than portions (holds multiple uses)

3. **Container Fill Levels**
   - Check how full a container is
   - Add moisture proportional to fill level
   - Partially filled buckets add less moisture

4. **Visual Water Level**
   - Show moisture level in bin texture
   - Darker appearance when wet
   - Lighter/cracked when dry

5. **Moisture Tooltip Enhancement**
   - Show recommended action based on current level
   - "Add water" when too dry
   - "Wait for evaporation" when too wet

6. **Smart Watering**
   - Right-click with water adds optimal amount
   - Automatically calculates moisture needed
   - Won't overfill past 100%

### Advanced Ideas

1. **Irrigation System**
   - Pipe water from nearby source
   - Automatic watering at intervals
   - Requires mechanical power or redstone-like system

2. **Rain Collection**
   - Collect rainwater in containers
   - Use collected water for composting
   - Encourages water conservation gameplay

3. **Compost Tea**
   - Excess water drains from very wet piles
   - Collect in containers below
   - Nutrient-rich liquid fertilizer

## Technical Notes

### Performance
- Water interaction is **event-driven** (only on right-click)
- No continuous checking or ticking required
- Particles spawn server-side only (prevents client lag)
- Sound plays locally for smooth experience

### Networking
- `MarkDirty()` called after watering
- Syncs moisture level to all clients
- Tooltip updates immediately for all players
- Particles visible to all nearby players

### Save/Load
- Moisture level persists through saves
- `ToTreeAttributes` and `FromTreeAttributes` include moisture
- Old saves default to 0.5 (optimal) if missing

## Localization

### English Entries
```json
{
  "blockhelp-compostbin-water": "Water pile"
}
```

### Future Translations
For multi-language support, add to each `lang/*.json`:
- German: "Haufen bewässern"
- French: "Arroser le tas"
- Spanish: "Regar la pila"
- etc.

## Testing Checklist

- [ ] Right-click bin with water portion → moisture increases
- [ ] Water portion is consumed after use
- [ ] Splash sound plays when watering
- [ ] Water particles spawn on pile
- [ ] Tooltip shows updated moisture percentage
- [ ] Cannot water when bin is empty
- [ ] Cannot water when bin is finished
- [ ] Moisture clamps at 100% (can't overflow)
- [ ] Works with buckets (when implemented)
- [ ] Interaction hint appears when holding water
- [ ] Save/load preserves moisture level
- [ ] Multiplayer syncs moisture to all clients

## Conclusion

The water interaction system gives players direct control over compost moisture, enabling:
- Active management in challenging climates
- Indoor composting viability
- Strategic optimization of decomposition rates
- Satisfying tactile feedback (splash, particles)

It integrates seamlessly with the automatic moisture system while maintaining simplicity for casual players who rely on rain.