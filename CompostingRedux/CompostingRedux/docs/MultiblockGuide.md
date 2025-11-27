# Multiblock Guide for Vintage Story

## Overview

Multiblocks are structures composed of multiple blocks that function as a single unit. Examples in vanilla Vintage Story include the helve hammer, pulverizer, and windmill.

## Core Concepts

### 1. Master Block
- The "main" block that contains all the logic
- Usually has a BlockEntity that manages the multiblock state
- Handles player interactions
- Coordinates with slave blocks

### 2. Slave Blocks
- Auxiliary blocks that are part of the structure
- Typically transparent/invisible or decorative
- Delegate functionality to the master block
- Store a reference to the master block position

### 3. Structure Definition
- Defines the shape and layout of the multiblock
- Specifies which blocks go where relative to the master
- Can include rotation/orientation support

## Implementation Steps

### Step 1: Define the Structure

```csharp
public class MultiblockStructure
{
    // Relative positions from master block
    public BlockPos[] SlavePositions { get; set; }
    
    // Optional: Block codes for each position
    public AssetLocation[] BlockCodes { get; set; }
    
    public MultiblockStructure()
    {
        // Example: 2x2x2 structure
        SlavePositions = new[]
        {
            new BlockPos(1, 0, 0),  // East
            new BlockPos(0, 1, 0),  // Up
            new BlockPos(1, 1, 0),  // East + Up
            new BlockPos(0, 0, 1),  // North
            new BlockPos(1, 0, 1),  // East + North
            new BlockPos(0, 1, 1),  // Up + North
            new BlockPos(1, 1, 1),  // East + Up + North
        };
    }
}
```

### Step 2: Create the Master Block

```csharp
public class BlockMultiblockMaster : Block
{
    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, 
        ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
    {
        // Check if all positions are available
        if (!CanPlaceMultiblock(world, blockSel.Position, ref failureCode))
        {
            return false;
        }
        
        // Place master block
        if (!base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode))
        {
            return false;
        }
        
        // Place slave blocks
        PlaceSlaveBlocks(world, blockSel.Position);
        
        return true;
    }
    
    private bool CanPlaceMultiblock(IWorldAccessor world, BlockPos masterPos, 
        ref string failureCode)
    {
        MultiblockStructure structure = new MultiblockStructure();
        
        foreach (BlockPos offset in structure.SlavePositions)
        {
            BlockPos checkPos = masterPos.AddCopy(offset);
            Block block = world.BlockAccessor.GetBlock(checkPos);
            
            if (!block.IsReplacableBy(this))
            {
                failureCode = "requirespace";
                return false;
            }
        }
        
        return true;
    }
    
    private void PlaceSlaveBlocks(IWorldAccessor world, BlockPos masterPos)
    {
        MultiblockStructure structure = new MultiblockStructure();
        Block slaveBlock = world.GetBlock(new AssetLocation("yourmod:multiblock-slave"));
        
        foreach (BlockPos offset in structure.SlavePositions)
        {
            BlockPos slavePos = masterPos.AddCopy(offset);
            world.BlockAccessor.SetBlock(slaveBlock.Id, slavePos);
            
            // Store master position in slave block entity
            if (world.BlockAccessor.GetBlockEntity(slavePos) is BlockEntityMultiblockSlave slaveBE)
            {
                slaveBE.MasterPos = masterPos.Copy();
                slaveBE.MarkDirty(true);
            }
        }
    }
    
    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, 
        IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        // Remove all slave blocks when master is broken
        RemoveSlaveBlocks(world, pos);
        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }
    
    private void RemoveSlaveBlocks(IWorldAccessor world, BlockPos masterPos)
    {
        MultiblockStructure structure = new MultiblockStructure();
        
        foreach (BlockPos offset in structure.SlavePositions)
        {
            BlockPos slavePos = masterPos.AddCopy(offset);
            world.BlockAccessor.SetBlock(0, slavePos); // Air
        }
    }
}
```

### Step 3: Create the Slave Block

```csharp
public class BlockMultiblockSlave : Block
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, 
        BlockSelection blockSel)
    {
        // Delegate to master block
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityMultiblockSlave slaveBE)
        {
            if (slaveBE.MasterPos != null)
            {
                Block masterBlock = world.BlockAccessor.GetBlock(slaveBE.MasterPos);
                return masterBlock.OnBlockInteractStart(world, byPlayer, 
                    new BlockSelection { Position = slaveBE.MasterPos });
            }
        }
        
        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }
    
    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, 
        IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        // When slave is broken, break the master
        if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityMultiblockSlave slaveBE)
        {
            if (slaveBE.MasterPos != null)
            {
                Block masterBlock = world.BlockAccessor.GetBlock(slaveBE.MasterPos);
                masterBlock.OnBlockBroken(world, slaveBE.MasterPos, byPlayer, dropQuantityMultiplier);
            }
        }
        
        // Don't call base - master handles everything
    }
    
    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, 
        IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        // Slaves don't drop anything - master handles drops
        return new ItemStack[0];
    }
}
```

### Step 4: Create Block Entities

**Master Block Entity:**
```csharp
public class BlockEntityMultiblockMaster : BlockEntity
{
    // Your multiblock logic here
    private bool isActive = false;
    
    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        // Initialize multiblock
    }
    
    public void OnPlayerInteract(IPlayer byPlayer)
    {
        // Handle player interaction
        isActive = !isActive;
        MarkDirty(true);
    }
    
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetBool("isActive", isActive);
    }
    
    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        isActive = tree.GetBool("isActive");
    }
}
```

**Slave Block Entity:**
```csharp
public class BlockEntityMultiblockSlave : BlockEntity
{
    public BlockPos MasterPos { get; set; }
    
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        
        if (MasterPos != null)
        {
            tree.SetInt("masterX", MasterPos.X);
            tree.SetInt("masterY", MasterPos.Y);
            tree.SetInt("masterZ", MasterPos.Z);
        }
    }
    
    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        
        if (tree.HasAttribute("masterX"))
        {
            MasterPos = new BlockPos(
                tree.GetInt("masterX"),
                tree.GetInt("masterY"),
                tree.GetInt("masterZ")
            );
        }
    }
}
```

## Advanced Features

### Rotation Support

```csharp
public BlockPos[] GetRotatedOffsets(BlockFacing facing)
{
    // Rotate structure based on placement direction
    MultiblockStructure structure = new MultiblockStructure();
    BlockPos[] rotated = new BlockPos[structure.SlavePositions.Length];
    
    for (int i = 0; i < structure.SlavePositions.Length; i++)
    {
        rotated[i] = RotatePosition(structure.SlavePositions[i], facing);
    }
    
    return rotated;
}

private BlockPos RotatePosition(BlockPos pos, BlockFacing facing)
{
    // Implement rotation logic based on facing direction
    // This is a simplified example
    switch (facing.Code)
    {
        case "north":
            return pos.Copy();
        case "east":
            return new BlockPos(-pos.Z, pos.Y, pos.X);
        case "south":
            return new BlockPos(-pos.X, pos.Y, -pos.Z);
        case "west":
            return new BlockPos(pos.Z, pos.Y, -pos.X);
        default:
            return pos.Copy();
    }
}
```

### Validation on Load

```csharp
public override void Initialize(ICoreAPI api)
{
    base.Initialize(api);
    
    if (api.Side == EnumAppSide.Server)
    {
        // Validate multiblock structure on load
        RegisterDelayedCallback((dt) => ValidateStructure(), 1000);
    }
}

private void ValidateStructure()
{
    // Check if all slave blocks are still in place
    MultiblockStructure structure = new MultiblockStructure();
    
    foreach (BlockPos offset in structure.SlavePositions)
    {
        BlockPos slavePos = Pos.AddCopy(offset);
        Block block = Api.World.BlockAccessor.GetBlock(slavePos);
        
        if (!(block is BlockMultiblockSlave))
        {
            // Structure is broken, disable functionality
            Api.Logger.Warning($"Multiblock structure broken at {Pos}");
            // Optionally remove the entire structure
            break;
        }
    }
}
```

### Visual Feedback

```csharp
public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, 
    ItemStack byItemStack = null)
{
    base.OnBlockPlaced(world, blockPos, byItemStack);
    
    // Show particle effects when multiblock is formed
    if (world.Side == EnumAppSide.Client)
    {
        MultiblockStructure structure = new MultiblockStructure();
        var clientApi = world.Api as ICoreClientAPI;
        
        foreach (BlockPos offset in structure.SlavePositions)
        {
            BlockPos effectPos = blockPos.AddCopy(offset);
            
            clientApi?.World.SpawnParticles(new SimpleParticleProperties()
            {
                MinPos = effectPos.ToVec3d().Add(0.5, 0.5, 0.5),
                MinVelocity = new Vec3f(-0.2f, 0.2f, -0.2f),
                AddVelocity = new Vec3f(0.4f, 0.4f, 0.4f),
                Color = ColorUtil.ToRgba(255, 100, 255, 100),
                MinQuantity = 5,
                AddQuantity = 5,
                MinSize = 0.1f,
                MaxSize = 0.3f,
                LifeLength = 1.0f,
                ParticleModel = EnumParticleModel.Quad
            });
        }
    }
}
```

## JSON Configuration

**Master Block:**
```json
{
  "code": "multiblock-master",
  "class": "BlockMultiblockMaster",
  "entityClass": "BlockEntityMultiblockMaster",
  "variantgroups": [
    {
      "code": "state",
      "states": ["inactive", "active"]
    }
  ],
  "behaviors": [
    { "name": "HorizontalOrientable" }
  ],
  "shape": { "base": "block/multiblock/master" },
  "collisionbox": { "x1": 0, "y1": 0, "z1": 0, "x2": 2, "y2": 2, "z2": 2 },
  "selectionbox": { "x1": 0, "y1": 0, "z1": 0, "x2": 2, "y2": 2, "z2": 2 }
}
```

**Slave Block:**
```json
{
  "code": "multiblock-slave",
  "class": "BlockMultiblockSlave",
  "entityClass": "BlockEntityMultiblockSlave",
  "drawtype": "empty",
  "collisionbox": null,
  "selectionbox": null,
  "replaceable": 500
}
```

## Best Practices

1. **Always validate structure** before placing
2. **Store master position** in slave block entities
3. **Handle all interactions** through the master block
4. **Remove all blocks** when any part is broken
5. **Use appropriate collision/selection boxes** to make the multiblock feel like one unit
6. **Save structure state** properly in block entities
7. **Test rotation** in all 4 directions
8. **Provide clear feedback** when placement fails
9. **Consider chunk boundaries** - multiblocks spanning chunks can cause issues
10. **Validate on world load** to handle missing/corrupted slaves

## Common Pitfalls

- **Forgetting to remove slaves** when master is broken
- **Not handling chunk unloading** properly
- **Circular references** in block entity serialization
- **Missing null checks** for master position
- **Not testing all rotation angles**
- **Ignoring world boundaries** (y < 0 or y > max)

## Example Use Cases

- **Large machinery**: Mills, crushers, forges
- **Multi-tile storage**: Large chests, silos
- **Decorative structures**: Statues, gates, archways
- **Processing stations**: Assembly lines, workbenches
- **Agricultural equipment**: Automated farms, irrigation systems

---

This guide provides the foundation for creating multiblock structures in Vintage Story. Adapt these patterns to fit your specific needs!