using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace CompostingRedux.Helpers
{
    /// <summary>
    /// Custom inventory for the compost bin that tracks individual items and their decomposition state.
    /// Supports layering mechanics and proper item management.
    /// </summary>
    public class CompostBinInventory : InventoryBase, ISlotProvider
    {
        private ItemSlot[] slots;
        
        /// <summary>
        /// Gets all slots in the inventory.
        /// </summary>
        public ItemSlot[] Slots => slots;
        
        /// <summary>
        /// Maximum number of item slots in the compost bin.
        /// Each slot can hold a stack of the same item type.
        /// </summary>
        public const int SlotCount = 32;

        /// <summary>
        /// Creates a new compost bin inventory.
        /// </summary>
        public CompostBinInventory(string inventoryID, ICoreAPI api) : base(inventoryID, api)
        {
            slots = GenEmptySlots(SlotCount);
        }

        /// <summary>
        /// Creates a new compost bin inventory with class and instance ID.
        /// </summary>
        public CompostBinInventory(string className, string instanceID, ICoreAPI api) 
            : base(className, instanceID, api)
        {
            slots = GenEmptySlots(SlotCount);
        }

        /// <summary>
        /// Total number of slots in this inventory.
        /// </summary>
        public override int Count => slots.Length;

        /// <summary>
        /// Gets or sets a slot by index.
        /// </summary>
        public override ItemSlot this[int slotId]
        {
            get
            {
                if (slotId < 0 || slotId >= Count) return null!;
                return slots[slotId];
            }
            set
            {
                if (slotId < 0 || slotId >= Count) 
                    throw new ArgumentOutOfRangeException(nameof(slotId));
                if (value == null) 
                    throw new ArgumentNullException(nameof(value));
                slots[slotId] = value;
            }
        }

        /// <summary>
        /// Gets the total number of items currently in the inventory.
        /// </summary>
        public int TotalItemCount
        {
            get
            {
                if (slots == null) return 0;
                int count = 0;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != null && !slots[i].Empty)
                    {
                        count += slots[i].StackSize;
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// Gets all non-empty slots in order they were added.
        /// </summary>
        public IEnumerable<ItemSlot> GetFilledSlots()
        {
            if (slots == null) return Enumerable.Empty<ItemSlot>();
            return slots.Where(slot => slot != null && !slot.Empty);
        }

        /// <summary>
        /// Checks if the inventory can accept the given item stack.
        /// </summary>
        public bool CanAccept(ItemStack itemStack, int maxCapacity)
        {
            if (itemStack == null || itemStack.StackSize == 0) return false;
            
            int currentTotal = TotalItemCount;
            return currentTotal < maxCapacity;
        }

        /// <summary>
        /// Tries to add an item stack to the inventory, respecting max capacity.
        /// Returns the number of items actually added.
        /// </summary>
        public int TryAddItems(ItemStack itemStack, int maxCapacity)
        {
            if (itemStack == null || itemStack.StackSize == 0) return 0;
            if (Api?.World == null) return 0;

            int availableSpace = maxCapacity - TotalItemCount;
            if (availableSpace <= 0) return 0;

            int amountToAdd = Math.Min(itemStack.StackSize, availableSpace);
            
            // Try to merge with existing stacks first
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null || slots[i].Empty || slots[i].Itemstack == null) continue;
                
                if (slots[i].Itemstack.Equals(Api.World, itemStack, Vintagestory.API.Config.GlobalConstants.IgnoredStackAttributes))
                {
                    int spaceInSlot = slots[i].Itemstack.Collectible.MaxStackSize - slots[i].StackSize;
                    if (spaceInSlot > 0)
                    {
                        int mergeAmount = Math.Min(amountToAdd, spaceInSlot);
                        slots[i].Itemstack.StackSize += mergeAmount;
                        slots[i].MarkDirty();
                        amountToAdd -= mergeAmount;
                        
                        if (amountToAdd <= 0) return Math.Min(itemStack.StackSize, availableSpace);
                    }
                }
            }

            // Add to new slots
            while (amountToAdd > 0)
            {
                int emptySlotIndex = GetFirstEmptySlot();
                if (emptySlotIndex == -1) break; // No more slots available

                ItemStack stackToAdd = itemStack.Clone();
                int slotMaxSize = stackToAdd.Collectible.MaxStackSize;
                stackToAdd.StackSize = Math.Min(amountToAdd, slotMaxSize);
                
                slots[emptySlotIndex].Itemstack = stackToAdd;
                slots[emptySlotIndex].MarkDirty();
                
                amountToAdd -= stackToAdd.StackSize;
            }

            return Math.Min(itemStack.StackSize, availableSpace) - amountToAdd;
        }

        /// <summary>
        /// Finds the first empty slot in the inventory.
        /// Returns -1 if no empty slots are available.
        /// </summary>
        private int GetFirstEmptySlot()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].Empty) return i;
            }
            return -1;
        }

        /// <summary>
        /// Clears all items from the inventory.
        /// Used when harvesting finished compost.
        /// </summary>
        public void ClearAll()
        {
            if (slots == null) return;
            
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].Itemstack = null;
                    slots[i].MarkDirty();
                }
            }
        }

        /// <summary>
        /// Gets a summary of items in the inventory grouped by type.
        /// Returns a dictionary of item code -> total count.
        /// </summary>
        public Dictionary<string, int> GetItemSummary()
        {
            var summary = new Dictionary<string, int>();
            
            if (slots == null) return summary;
            
            foreach (var slot in slots)
            {
                if (slot == null || slot.Empty || slot.Itemstack == null || slot.Itemstack.Collectible == null || slot.Itemstack.Collectible.Code == null) continue;
                
                string itemCode = slot.Itemstack.Collectible.Code.ToString();
                if (summary.ContainsKey(itemCode))
                {
                    summary[itemCode] += slot.StackSize;
                }
                else
                {
                    summary[itemCode] = slot.StackSize;
                }
            }
            
            return summary;
        }

        /// <summary>
        /// Loads inventory from saved tree attributes.
        /// </summary>
        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            slots = SlotsFromTreeAttributes(tree);
            
            // Ensure slots is never null - initialize empty array if needed
            if (slots == null)
            {
                slots = GenEmptySlots(SlotCount);
            }
        }

        /// <summary>
        /// Saves inventory to tree attributes.
        /// </summary>
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            SlotsToTreeAttributes(slots, tree);
        }

        /// <summary>
        /// Called when a slot is modified.
        /// </summary>
        public override void OnItemSlotModified(ItemSlot slot)
        {
            base.OnItemSlotModified(slot);
        }
    }
}