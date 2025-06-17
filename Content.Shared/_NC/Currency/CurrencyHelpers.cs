using Content.Shared.Containers.ItemSlots;
using Content.Shared.Inventory;
using Content.Shared.Hands.Components;
using Robust.Shared.Containers;

namespace Content.Shared._NC.Currency
{
    public static class CurrencyHelpers
    {
        public static IEnumerable<EntityUid> EnumerateDeepItemsUnique(EntityUid owner, IEntityManager ents)
        {
            var seen = new HashSet<EntityUid>();

            if (ents.TryGetComponent(owner, out InventoryComponent? inventory))
            {
                var enumerator = new InventorySystem.InventorySlotEnumerator(inventory);
                while (enumerator.NextItem(out var item))
                    if (seen.Add(item))
                        yield return item;
            }
            if (ents.TryGetComponent(owner, out ItemSlotsComponent? itemSlots))
            {
                foreach (var slot in itemSlots.Slots.Values)
                    if (slot.HasItem && slot.Item != null)
                    {
                        var item = slot.Item.Value;
                        if (seen.Add(item))
                            yield return item;
                    }
            }

            if (ents.TryGetComponent(owner, out ContainerManagerComponent? containers))
            {
                foreach (var container in containers.Containers.Values)
                {
                    foreach (var entity in container.ContainedEntities)
                        if (seen.Add(entity))
                            yield return entity;
                }
            }

            if (ents.TryGetComponent(owner, out HandsComponent? hands))
            {
                foreach (var hand in hands.Hands.Values)
                    if (hand.HeldEntity != null)
                    {
                        var held = hand.HeldEntity.Value;
                        if (seen.Add(held))
                            yield return held;
                    }
            }
        }
    }
}
