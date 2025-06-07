using Content.Shared.Inventory;

namespace Content.Shared._NC.Trader;

public static class InventoryHelpers
{
    public static IEnumerable<InventorySlot> EnumerateAllSlots(this InventorySystem inventory, EntityUid uid, InventoryComponent inv)
    {
        var knownSlots = new[] { "pocket1", "pocket2", "belt", "back", "gloves", "shoes", "id", };

        foreach (var slotName in knownSlots)
            yield return new InventorySlot(slotName);
    }

    public readonly struct InventorySlot
    {
        public readonly string Id;

        public InventorySlot(string id)
        {
            Id = id;
        }
    }
}
