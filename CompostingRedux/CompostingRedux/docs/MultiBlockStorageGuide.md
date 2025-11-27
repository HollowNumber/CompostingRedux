# Multi-Block Storage Guide - Trunks and Large Containers

## Overview

Multi-block storage containers like trunks occupy multiple block spaces but function as a single inventory unit. The vanilla trunk is 2 blocks tall, with the bottom block containing the inventory logic and the top block being a visual extension.

## How Trunks Work

A trunk combines two systems:
1. **Multiblock Structure** - Occupies 2 vertical blocks
2. **Inventory System** - Stores items in slots

### Structure:
- **Bottom Block (Master)**: Contains BlockEntity with inventory, handles all logic
- **Top Block (Slave)**: Visual only, delegates all interactions to bottom block

## Implementation

### Step 1: Create the Inventory

```csharp
public class InventoryTrunk : InventoryBase, ISlotProvider
{
    private ItemSlot[] slots;
    
    public InventoryTrunk(string inventoryID, ICoreAPI api, int slotCount = 24) 
        : base(inventoryID, api)
    {
        slots = GenEmptySlots(slotCount);
    }
    
    public InventoryTrunk(string className, string instanceID, ICoreAPI api) 
        : base(className, instanceID, api)
    {
        slots = GenEmptySlots(24);
    }
    
    public override int Count => slots.Length;
    
    public override ItemSlot this[int slotId]
    {
        get
        {
            if (slotId < 0 || slotId >= Count) return null;
            return slots[slotId];
        }
        set
        {
            if (slotId < 0 || slotId >= Count) return;
            slots[slotId] = value ?? throw new ArgumentNullException(nameof(value));
        }
    }
    
    public override void FromTreeAttributes(ITreeAttribute tree)
    {
        slots = SlotsFromTreeAttributes(tree);
    }
    
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        SlotsToTreeAttributes(slots, tree);
    }
}
```

### Step 2: Create the Bottom Block Entity (Master)

```csharp
public class BlockEntityTrunkBottom : BlockEntityContainer
{
    private InventoryTrunk inventory;
    private BlockPos topBlockPos;
    
    public override InventoryBase Inventory => inventory;
    public override string InventoryClassName => "trunk";
    
    public BlockEntityTrunkBottom()
    {
        // Required parameterless constructor
    }
    
    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        
        // Calculate top block position
        topBlockPos = Pos.UpCopy();
        
        // Initialize inventory
        if (inventory == null)
        {
            inventory = new InventoryTrunk(null, api);
            inventory.Pos = Pos;
            inventory.LateInitialize($"{InventoryClassName}-{Pos.X}/{Pos.Y}/{Pos.Z}", api);
        }
    }
    
    public override void OnBlockPlaced(ItemStack byItemStack = null)
    {
        base.OnBlockPlaced(byItemStack);
        
        if (Api.Side == EnumAppSide.Server)
        {
            // Initialize inventory
            if (inventory == null)
            {
                inventory = new InventoryTrunk(null, Api);
                inventory.Pos = Pos;
                inventory.LateInitialize($"{InventoryClassName}-{Pos.X}/{Pos.Y}/{Pos.Z}", Api);
            }
        }
    }
    
    public void OnTopBlockBroken()
    {
        // Called when top block is broken
        // Break the bottom block too
        if (Api?.World != null)
        {
            Api.World.BlockAccessor.BreakBlock(Pos, null);
        }
    }
    
    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        
        if (inventory == null)
        {
            if (tree.HasAttribute("inventory"))
            {
                inventory = new InventoryTrunk(null, Api);
                inventory.Pos = Pos;
                inventory.LateInitialize($"{InventoryClassName}-{Pos.X}/{Pos.Y}/{Pos.Z}", Api);
                inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            }
        }
        else
        {
            inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
        }
        
        topBlockPos = Pos.UpCopy();
    }
    
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        
        if (inventory != null)
        {
            ITreeAttribute invTree = new TreeAttribute();
            inventory.ToTreeAttributes(invTree);
            tree["inventory"] = invTree;
        }
    }
}
```

### Step 3: Create the Top Block Entity (Slave)

```csharp
public class BlockEntityTrunkTop : BlockEntity
{
    private BlockPos bottomBlockPos;
    
    public BlockPos BottomBlockPos 
    { 
        get => bottomBlockPos; 
        set => bottomBlockPos = value; 
    }
    
    public BlockEntityTrunkBottom GetBottomBlockEntity()
    {
        if (bottomBlockPos == null) return null;
        return Api?.World?.BlockAccessor?.GetBlockEntity(bottomBlockPos) as BlockEntityTrunkBottom;
    }
    
    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        
        // Calculate bottom block position
        bottomBlockPos = Pos.DownCopy();
    }
    
    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        bottomBlockPos = Pos.DownCopy();
    }
    
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        // Top block doesn't need to save anything special
    }
}
```

### Step 4: Create the Bottom Block Class

```csharp
public class BlockTrunkBottom : Block
{
    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, 
        ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
    {
        // Check if there's space for top block
        BlockPos topPos = blockSel.Position.UpCopy();
        Block topBlock = world.BlockAccessor.GetBlock(topPos);
        
        if (!topBlock.IsReplacableBy(this))
        {
            failureCode = "requirespace";
            return false;
        }
        
        // Place bottom block
        if (!base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode))
        {
            return false;
        }
        
        // Place top block
        Block trunkTop = world.GetBlock(CodeWithParts("top"));
        if (trunkTop != null)
        {
            world.BlockAccessor.SetBlock(trunkTop.Id, topPos);
            
            // Initialize top block entity
            if (world.BlockAccessor.GetBlockEntity(topPos) is BlockEntityTrunkTop topBE)
            {
                topBE.BottomBlockPos = blockSel.Position.Copy();
                topBE.MarkDirty(true);
            }
        }
        
        return true;
    }
    
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityTrunkBottom beTrunk)
        {
            if (world.Side == EnumAppSide.Client)
            {
                var clientApi = world.Api as ICoreClientAPI;
                if (clientApi != null)
                {
                    var dialog = new GuiDialogTrunk("Trunk", beTrunk.Inventory, blockSel.Position, clientApi);
                    dialog.TryOpen();
                }
            }
            
            return true;
        }
        
        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }
    
    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, 
        float dropQuantityMultiplier = 1)
    {
        // Drop all items from inventory
        if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityTrunkBottom beTrunk)
        {
            if (beTrunk.Inventory != null)
            {
                for (int i = 0; i < beTrunk.Inventory.Count; i++)
                {
                    ItemSlot slot = beTrunk.Inventory[i];
                    if (!slot.Empty)
                    {
                        world.SpawnItemEntity(slot.Itemstack, pos.ToVec3d().Add(0.5, 0.5, 0.5));
                        slot.Itemstack = null;
                        slot.MarkDirty();
                    }
                }
            }
        }
        
        // Remove top block
        BlockPos topPos = pos.UpCopy();
        Block topBlock = world.BlockAccessor.GetBlock(topPos);
        if (topBlock is BlockTrunkTop)
        {
            world.BlockAccessor.SetBlock(0, topPos); // Set to air
        }
        
        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }
}
```

### Step 5: Create the Top Block Class

```csharp
public class BlockTrunkTop : Block
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        // Delegate to bottom block
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityTrunkTop topBE)
        {
            BlockEntityTrunkBottom bottomBE = topBE.GetBottomBlockEntity();
            if (bottomBE != null)
            {
                // Open the same GUI as bottom block
                if (world.Side == EnumAppSide.Client)
                {
                    var clientApi = world.Api as ICoreClientAPI;
                    if (clientApi != null)
                    {
                        var dialog = new GuiDialogTrunk("Trunk", bottomBE.Inventory, topBE.BottomBlockPos, clientApi);
                        dialog.TryOpen();
                    }
                }
                return true;
            }
        }
        
        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }
    
    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, 
        float dropQuantityMultiplier = 1)
    {
        // Break the bottom block, which handles everything
        if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityTrunkTop topBE)
        {
            if (topBE.BottomBlockPos != null)
            {
                Block bottomBlock = world.BlockAccessor.GetBlock(topBE.BottomBlockPos);
                bottomBlock.OnBlockBroken(world, topBE.BottomBlockPos, byPlayer, dropQuantityMultiplier);
            }
        }
        
        // Don't call base - bottom block handles everything including removing this block
    }
    
    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, 
        float dropQuantityMultiplier = 1)
    {
        // Top block doesn't drop anything - bottom block handles it
        return new ItemStack[0];
    }
    
    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
    {
        // Return the bottom block item
        return new ItemStack(world.GetBlock(CodeWithParts("bottom")));
    }
}
```

### Step 6: Create the GUI Dialog

```csharp
public class GuiDialogTrunk : GuiDialogBlockEntity
{
    public GuiDialogTrunk(string dialogTitle, InventoryBase inventory, BlockPos blockEntityPos, 
        ICoreClientAPI capi) : base(dialogTitle, inventory, blockEntityPos, capi)
    {
    }
    
    public override void OnGuiOpened()
    {
        base.OnGuiOpened();
        SetupDialog();
    }
    
    private void SetupDialog()
    {
        int rows = 6;    // 6 rows for trunk
        int cols = 4;    // 4 columns
        
        // Background
        ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;
        
        // Slot grid
        ElementBounds slotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 30, cols, rows);
        
        // Create slot IDs array (0-23 for 24 slots)
        int[] slotIds = Enumerable.Range(0, 24).ToArray();
        
        SingleComposer = capi.Gui
            .CreateCompo("trunkinventory" + BlockEntityPosition, ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar("Trunk", OnTitleBarClose)
            .BeginChildElements(bgBounds)
                .AddItemSlotGrid(Inventory, SendInvPacket, cols, slotIds, slotBounds, "trunkSlots")
            .EndChildElements()
            .Compose();
    }
    
    private void SendInvPacket(object packet)
    {
        capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, 
            BlockEntityPosition.Z, packet);
    }
    
    private void OnTitleBarClose()
    {
        TryClose();
    }
}
```

## JSON Configuration

### Bottom Block JSON

```json
{
  "code": "trunk",
  "class": "BlockTrunkBottom",
  "entityClass": "BlockEntityTrunkBottom",
  "variantgroups": [
    {
      "code": "type",
      "states": ["bottom"]
    },
    {
      "code": "wood",
      "states": ["oak", "birch", "pine"]
    }
  ],
  "shapeByType": {
    "*-bottom-*": { "base": "block/wood/trunk/{wood}/bottom" }
  },
  "blockmaterial": "Wood",
  "resistance": 2.0,
  "maxStackSize": 4,
  "creativeinventory": {
    "general": ["*-bottom-oak"],
    "decorative": ["*-bottom-*"]
  },
  "sounds": {
    "place": "game:block/planks",
    "break": "game:block/planks",
    "hit": "game:block/planks"
  }
}
```

### Top Block JSON

```json
{
  "code": "trunk",
  "class": "BlockTrunkTop",
  "entityClass": "BlockEntityTrunkTop",
  "variantgroups": [
    {
      "code": "type",
      "states": ["top"]
    },
    {
      "code": "wood",
      "states": ["oak", "birch", "pine"]
    }
  ],
  "shapeByType": {
    "*-top-*": { "base": "block/wood/trunk/{wood}/top" }
  },
  "blockmaterial": "Wood",
  "resistance": 2.0,
  "creativeinventory": {},
  "sounds": {
    "place": "game:block/planks",
    "break": "game:block/planks",
    "hit": "game:block/planks"
  }
}
```

## Advanced Features

### Horizontal Trunks (3 blocks wide)

```csharp
public class BlockEntityHorizontalTrunk : BlockEntityContainer
{
    private BlockPos[] slavePositions;
    
    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        
        // Define slave positions based on orientation
        BlockFacing facing = BlockFacing.FromCode(Block.Variant["side"]);
        slavePositions = new[]
        {
            Pos.AddCopy(facing.Normali),           // Middle block
            Pos.AddCopy(facing.Normali.X * 2, 0, facing.Normali.Z * 2)  // Far block
        };
    }
}
```

### Different Sizes

```csharp
public class BlockTrunkVariable : BlockTrunkBottom
{
    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, 
        ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
    {
        // Read size from variant
        int height = Variant["size"] switch
        {
            "small" => 1,   // 1 block (no top)
            "medium" => 2,  // 2 blocks
            "large" => 3,   // 3 blocks
            _ => 2
        };
        
        // Check space for all blocks
        for (int i = 1; i < height; i++)
        {
            BlockPos checkPos = blockSel.Position.UpCopy(i);
            Block block = world.BlockAccessor.GetBlock(checkPos);
            
            if (!block.IsReplacableBy(this))
            {
                failureCode = "requirespace";
                return false;
            }
        }
        
        // Place bottom
        if (!base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode))
        {
            return false;
        }
        
        // Place additional blocks
        Block topBlock = world.GetBlock(CodeWithParts("top"));
        for (int i = 1; i < height; i++)
        {
            BlockPos topPos = blockSel.Position.UpCopy(i);
            world.BlockAccessor.SetBlock(topBlock.Id, topPos);
        }
        
        return true;
    }
}
```

### Animated Opening

```csharp
public class BlockEntityAnimatedTrunk : BlockEntityTrunkBottom
{
    private float lidAngle = 0f;
    private bool isOpen = false;
    
    public float LidAngle => lidAngle;
    
    public void SetOpen(bool open)
    {
        isOpen = open;
        RegisterGameTickListener(AnimateLid, 50);
    }
    
    private void AnimateLid(float dt)
    {
        float targetAngle = isOpen ? 90f : 0f;
        
        if (Math.Abs(lidAngle - targetAngle) < 1f)
        {
            lidAngle = targetAngle;
            UnregisterGameTickListener(AnimateLid);
            MarkDirty(true);
            return;
        }
        
        float direction = isOpen ? 1f : -1f;
        lidAngle += direction * 180f * dt; // 0.5 seconds to open/close
        lidAngle = GameMath.Clamp(lidAngle, 0f, 90f);
        
        MarkDirty(true);
    }
}
```

## Selection and Collision Boxes

For multi-block containers, you typically want:

**Bottom Block:**
```json
"collisionbox": { "x1": 0, "y1": 0, "z1": 0, "x2": 1, "y2": 2, "z2": 1 },
"selectionbox": { "x1": 0, "y1": 0, "z1": 0, "x2": 1, "y2": 2, "z2": 1 }
```

**Top Block:**
```json
"collisionbox": null,
"selectionbox": null
```

This makes the entire 2-block structure selectable from the bottom block, and prevents double-selection.

## Best Practices

1. **Always check space** before placing multi-block structures
2. **Store master position** in slave blocks
3. **Delegate all interactions** to the master block
4. **Remove all blocks** when any part is broken
5. **Drop items from master** block only
6. **Use appropriate hitboxes** - make it feel like one unit
7. **Initialize inventory** in the master block only
8. **Test chunk boundaries** - multi-blocks spanning chunks need special handling
9. **Validate on load** - ensure structure integrity
10. **Provide visual feedback** - particles/sounds on placement

## Common Pitfalls

- Forgetting to remove slave blocks when master is broken
- Not checking if top block space is available
- Placing slave blocks in creative inventory
- Duplicating drops from both blocks
- Not handling rotation properly
- Slave blocks saving inventory data (only master should)
- Not delegating interactions from slaves to master
- Missing null checks for block entity references

## Summary

Multi-block storage combines:
- **Multiblock Structure**: Bottom (master) + Top (slave) blocks
- **Inventory System**: Full item storage with slots
- **Interaction Delegation**: Click anywhere → opens same inventory
- **Unified Breaking**: Break any part → entire structure removed

The trunk is essentially a 2-block tall chest where both blocks act as entry points to the same inventory!