using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Robust.Shared.Containers;

namespace Content.Shared._NC.Currency;

public static class CurrencyHelpers
{

    public static IEnumerable<EntityUid> EnumerateDeepItemsUnique(EntityUid owner, IEntityManager ents, HashSet<EntityUid>? visited = null)
    {
        visited ??= new();

        void Enqueue(EntityUid uid, Queue<EntityUid> queue)
        {
            if (visited.Add(uid))
                queue.Enqueue(uid);
        }

        var queue = new Queue<EntityUid>();

        if (ents.TryGetComponent(owner, out InventoryComponent? inventory))
        {
            var slotEnum = new InventorySystem.InventorySlotEnumerator(inventory);
            while (slotEnum.NextItem(out var item))
                Enqueue(item, queue);
        }

        if (ents.TryGetComponent(owner, out ItemSlotsComponent? itemSlots))
        {
            foreach (var slot in itemSlots.Slots.Values)
                if (slot.HasItem && slot.Item.HasValue)
                    Enqueue(slot.Item.Value, queue);
        }

        if (ents.TryGetComponent(owner, out HandsComponent? hands))
        {
            foreach (var hand in hands.Hands.Values)
                if (hand.HeldEntity.HasValue)
                    Enqueue(hand.HeldEntity.Value, queue);
        }

        if (ents.TryGetComponent(owner, out ContainerManagerComponent? cmcRoot))
        {
            foreach (var container in cmcRoot.Containers.Values)
            {
                foreach (var entity in container.ContainedEntities)
                    Enqueue(entity, queue);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;

            if (ents.TryGetComponent(current, out ContainerManagerComponent? cmc))
            {
                foreach (var container in cmc.Containers.Values)
                {
                    foreach (var child in container.ContainedEntities)
                        Enqueue(child, queue);
                }
            }
        }
    }
}
