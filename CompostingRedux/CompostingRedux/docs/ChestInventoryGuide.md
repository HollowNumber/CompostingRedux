# Chest/Trunk Inventory System Guide for Vintage Story

## Overview

Chests and trunks in Vintage Story are container blocks that store items in an inventory system. They use a combination of BlockEntities, InventoryBase, and GUI dialogs to provide a storage interface.

## Core Components

### 1. InventoryBase
- Manages item storage
- Handles slot operations (insert, remove, swap)
- Supports serialization for world saving
- Can be shared between client and server

### 2. Block Entity
- Contains the inventory instance
- Handles synchronization between client/server
- Manages inventory state persistence

### 3. GUI Dialog
- Client-side interface for viewing/interacting with inventory
- Shows item slots in a grid layout
- Handles mouse interactions (click, drag, shift-click)

## Implementation

### Step 1: Create the Inventory Class

```csharp
public class InventoryChest : InventoryBase, ISlotProvider
{
    private ItemSlot[] slots;
    
    public InventoryChest(string inventoryID, ICoreAPI api) : base(inventoryID, api)
    {
        // Create slots (e.g., 16 for a small chest)
        slots = GenEmptySlots(16);
    }
    
    public InventoryChest(string className, string instanceID, ICoreAPI api) 
        : base(className, instanceID, api)
    {
        slots = GenEmptySlots(16);
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

### Step 2: Create the Block Entity

```csharp
public class BlockEntityChest : BlockEntityContainer
{
    private InventoryChest inventory;
    
    public override InventoryBase Inventory => inventory;
    
    public override string InventoryClassName => "chest";
    
    public BlockEntityChest()
    {
        // Parameterless constructor required
    }
    
    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        
        // Create inventory if it doesn't exist
        if (inventory == null)
        {
            inventory = new InventoryChest(null, api);
            inventory.Pos = Pos;
            inventory.LateInitialize($"{InventoryClassName}-{Pos.X}/{Pos.Y}/{Pos.Z}", api);
        }
    }
    
    public override void OnBlockPlaced(ItemStack byItemStack = null)
    {
        base.OnBlockPlaced(byItemStack);
        
        if (Api.Side == EnumAppSide.Server)
        {
            // Initialize inventory on server
            if (inventory == null)
            {
                inventory = new InventoryChest(null, Api);
                inventory.Pos = Pos;
                inventory.LateInitialize($"{InventoryClassName}-{Pos.X}/{Pos.Y}/{Pos.Z}", Api);
            }
        }
    }
    
    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        
        if (inventory == null)
        {
            if (tree.HasAttribute("inventory"))
            {
                inventory = new InventoryChest(null, Api);
                inventory.Pos = Pos;
                inventory.LateInitialize($"{InventoryClassName}-{Pos.X}/{Pos.Y}/{Pos.Z}", Api);
                inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            }
        }
        else
        {
            inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
        }
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

### Step 3: Create the Block Class

```csharp
public class BlockChest : Block
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityChest beChest)
        {
            if (world.Side == EnumAppSide.Client)
            {
                // Open GUI on client
                var clientApi = world.Api as ICoreClientAPI;
                if (clientApi != null)
                {
                    var dialog = new GuiDialogChest("Chest", beChest.Inventory, blockSel.Position, clientApi);
                    dialog.TryOpen();
                }
            }
            
            return true;
        }
        
        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }
    
    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
    {
        // Return a stack of the chest item
        return new ItemStack(world.GetBlock(CodeWithVariant("type", "normal")));
    }
    
    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        // Drop all items from the chest
        if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityChest beChest)
        {
            if (beChest.Inventory != null)
            {
                for (int i = 0; i < beChest.Inventory.Count; i++)
                {
                    ItemSlot slot = beChest.Inventory[i];
                    if (!slot.Empty)
                    {
                        world.SpawnItemEntity(slot.Itemstack, pos.ToVec3d().Add(0.5, 0.5, 0.5));
                        slot.Itemstack = null;
                        slot.MarkDirty();
                    }
                }
            }
        }
        
        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }
}
```

### Step 4: Create the GUI Dialog

```csharp
public class GuiDialogChest : GuiDialogBlockEntity
{
    public GuiDialogChest(string dialogTitle, InventoryBase inventory, BlockPos blockEntityPos, ICoreClientAPI capi)
        : base(dialogTitle, inventory, blockEntityPos, capi)
    {
        // Composer will be set up in SetupDialog
    }
    
    public override void OnGuiOpened()
    {
        base.OnGuiOpened();
        SetupDialog();
    }
    
    private void SetupDialog()
    {
        // Calculate dimensions
        double slotSize = GuiElementPassiveItemSlot.unscaledSlotSize;
        double slotPadding = GuiElementPassiveItemSlot.unscaledSlotPadding;
        
        int rows = 4;    // 4 rows
        int cols = 4;    // 4 columns
        
        double inventoryWidth = cols * (slotSize + slotPadding);
        double inventoryHeight = rows * (slotSize + slotPadding);
        
        // Background + 10 pixels padding on each side
        ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;
        
        // Slot grid bounds
        ElementBounds slotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 30, cols, rows);
        
        // Player inventory at bottom
        ElementBounds playerInvBounds = ElementStdBounds.PlacedAfter(slotBounds, 0);
        
        SingleComposer = capi.Gui
            .CreateCompo("chestinventory" + BlockEntityPosition, ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar("Chest", OnTitleBarClose)
            .BeginChildElements(bgBounds)
                .AddItemSlotGrid(Inventory, SendInvPacket, cols, new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 }, slotBounds, "chestSlots")
                .AddDynamicText("", CairoFont.WhiteDetailText(), playerInvBounds.FlatCopy().WithFixedOffset(0, -20))
            .EndChildElements()
            .Compose();
    }
    
    private void SendInvPacket(object packet)
    {
        capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, packet);
    }
    
    private void OnTitleBarClose()
    {
        TryClose();
    }
}
```

## Advanced Features

### Different Sized Chests

```csharp
public class BlockEntityGenericChest : BlockEntityContainer
{
    private int slotCount = 16;
    
    public override void Initialize(ICoreAPI api)
    {
        // Read slot count from block variant or attributes
        if (Block.Variant.ContainsKey("size"))
        {
            string size = Block.Variant["size"];
            slotCount = size switch
            {
                "small" => 8,
                "medium" => 16,
                "large" => 32,
                _ => 16
            };
        }
        
        inventory = new InventoryChest(null, api, slotCount);
        base.Initialize(api);
    }
}
```

### Labeled Chests

```csharp
public class BlockEntityLabeledChest : BlockEntityChest
{
    private string label = "";
    
    public string Label
    {
        get => label;
        set
        {
            label = value;
            MarkDirty(true);
        }
    }
    
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetString("label", label);
    }
    
    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        label = tree.GetString("label", "");
    }
}
```

**GUI with Label Input:**
```csharp
SingleComposer = capi.Gui
    .CreateCompo("labeledchest" + BlockEntityPosition, ElementStdBounds.AutosizedMainDialog)
    .AddShadedDialogBG(bgBounds)
    .AddDialogTitleBar("Chest", OnTitleBarClose)
    .BeginChildElements(bgBounds)
        .AddTextInput(labelBounds, OnLabelChanged, CairoFont.WhiteDetailText(), "labelInput")
        .AddItemSlotGrid(Inventory, SendInvPacket, cols, slotIds, slotBounds, "chestSlots")
    .EndChildElements()
    .Compose();
```

### Lockable Chests

```csharp
public class BlockEntityLockableChest : BlockEntityChest
{
    private string ownerUID = "";
    private bool isLocked = false;
    
    public bool CanAccess(IPlayer player)
    {
        if (!isLocked) return true;
        if (string.IsNullOrEmpty(ownerUID)) return true;
        
        return player.PlayerUID == ownerUID;
    }
    
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetString("ownerUID", ownerUID);
        tree.SetBool("isLocked", isLocked);
    }
    
    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        ownerUID = tree.GetString("ownerUID", "");
        isLocked = tree.GetBool("isLocked");
    }
}
```

### Animated Chests

```csharp
public class BlockEntityAnimatedChest : BlockEntityChest
{
    private float openProgress = 0f;
    private bool isOpen = false;
    
    public void OpenChest()
    {
        isOpen = true;
        RegisterGameTickListener(OnAnimationTick, 50);
    }
    
    public void CloseChest()
    {
        isOpen = false;
    }
    
    private void OnAnimationTick(float dt)
    {
        if (isOpen && openProgress < 1f)
        {
            openProgress += dt * 2f; // 0.5 second to open
            if (openProgress >= 1f)
            {
                openProgress = 1f;
                UnregisterGameTickListener(OnAnimationTick);
            }
            MarkDirty(true);
        }
        else if (!isOpen && openProgress > 0f)
        {
            openProgress -= dt * 2f;
            if (openProgress <= 0f)
            {
                openProgress = 0f;
                UnregisterGameTickListener(OnAnimationTick);
            }
            MarkDirty(true);
        }
    }
}
```

## Inventory Features

### Auto-Sorting

```csharp
public void SortInventory()
{
    var items = new List<ItemStack>();
    
    // Collect all items
    for (int i = 0; i < inventory.Count; i++)
    {
        if (!inventory[i].Empty)
        {
            items.Add(inventory[i].Itemstack);
            inventory[i].Itemstack = null;
        }
    }
    
    // Sort by code and stack size
    items.Sort((a, b) =>
    {
        int codeCompare = string.Compare(a.Collectible.Code.ToString(), b.Collectible.Code.ToString());
        if (codeCompare != 0) return codeCompare;
        return b.StackSize.CompareTo(a.StackSize);
    });
    
    // Reinsert items
    foreach (var stack in items)
    {
        ItemStackMoveOperation op = new ItemStackMoveOperation(Api.World, EnumMouseButton.Left, 0, EnumMergePriority.AutoMerge, stack.StackSize);
        op.ActingPlayer = null;
        
        inventory.TryPutItem(stack, ref op);
    }
    
    MarkDirty(true);
}
```

### Search/Filter

```csharp
public class GuiDialogSearchableChest : GuiDialogChest
{
    private string searchText = "";
    
    private void SetupDialog()
    {
        // ... other setup ...
        
        ElementBounds searchBounds = ElementBounds.Fixed(0, 0, 200, 30);
        
        SingleComposer = capi.Gui
            .CreateCompo("searchchest" + BlockEntityPosition, ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar("Chest", OnTitleBarClose)
            .BeginChildElements(bgBounds)
                .AddTextInput(searchBounds, OnSearchChanged, CairoFont.WhiteDetailText(), "searchInput")
                .AddItemSlotGrid(Inventory, SendInvPacket, cols, GetVisibleSlots(), slotBounds, "chestSlots")
            .EndChildElements()
            .Compose();
    }
    
    private void OnSearchChanged(string text)
    {
        searchText = text.ToLowerInvariant();
        SingleComposer.ReCompose();
    }
    
    private int[] GetVisibleSlots()
    {
        if (string.IsNullOrEmpty(searchText))
        {
            return Enumerable.Range(0, Inventory.Count).ToArray();
        }
        
        var visible = new List<int>();
        for (int i = 0; i < Inventory.Count; i++)
        {
            if (!Inventory[i].Empty)
            {
                string itemName = Inventory[i].Itemstack.GetName().ToLowerInvariant();
                if (itemName.Contains(searchText))
                {
                    visible.Add(i);
                }
            }
        }
        
        return visible.ToArray();
    }
}
```

## JSON Configuration

**Block JSON:**
```json
{
  "code": "chest",
  "class": "BlockChest",
  "entityClass": "BlockEntityChest",
  "variantgroups": [
    {
      "code": "type",
      "states": ["normal", "reinforced", "ornate"]
    }
  ],
  "shape": { "base": "block/wood/chest/normal" },
  "blockmaterial": "Wood",
  "resistance": 1.5,
  "maxStackSize": 8,
  "creativeinventory": {
    "general": ["*"],
    "decorative": ["*"]
  },
  "sounds": {
    "place": "game:block/planks",
    "break": "game:block/planks",
    "hit": "game:block/planks"
  },
  "drops": [
    {
      "type": "item",
      "code": "chest-{type}",
      "quantity": { "avg": 1 }
    }
  ]
}
```

## Best Practices

1. **Always use BlockEntityContainer** as base class for chests
2. **Synchronize inventory** between client and server using MarkDirty()
3. **Handle chunk unloading** - inventory data must be saved
4. **Drop items on break** - don't lose player items
5. **Use proper slot rendering** - GuiElementItemSlotGrid
6. **Implement proper serialization** - ToTreeAttributes/FromTreeAttributes
7. **Test with full inventory** - edge cases matter
8. **Support shift-clicking** - standard player expectation
9. **Add sound effects** - open/close sounds enhance experience
10. **Consider performance** - don't update GUI every tick

## Common Patterns

### Quick Stack Button
```csharp
.AddSmallButton("Quick Stack", OnQuickStack, quickStackBounds)

private bool OnQuickStack()
{
    // Move matching items from player inventory to chest
    return true;
}
```

### Lock/Unlock Button
```csharp
.AddToggleButton("Locked", OnToggleLock, lockBounds, "lockButton")

private void OnToggleLock(bool on)
{
    (BlockEntity as BlockEntityLockableChest)?.SetLocked(on);
}
```

### Capacity Display
```csharp
private string GetCapacityText()
{
    int used = 0;
    for (int i = 0; i < Inventory.Count; i++)
    {
        if (!Inventory[i].Empty) used++;
    }
    return $"{used}/{Inventory.Count}";
}
```

---

This guide covers the essential patterns for implementing chest-like storage containers in Vintage Story. Adapt these patterns based on your specific needs!