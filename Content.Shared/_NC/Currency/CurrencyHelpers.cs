using System.Runtime.CompilerServices;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Robust.Shared.Containers;

namespace Content.Shared._NC.Currency;

public static class CurrencyHelpers
{
    public static IEnumerable<EntityUid> EnumerateDeepItemsUnique(EntityUid owner, IEntityManager ents)
    {
        var seen  = new HashSet<EntityUid>();
        var queue = new Queue<EntityUid>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Enqueue(EntityUid uid)
        {
            if (seen.Add(uid))
                queue.Enqueue(uid);
        }


        if (ents.TryGetComponent(owner, out InventoryComponent? inventory))
        {
            var slotEnum = new InventorySystem.InventorySlotEnumerator(inventory);
            while (slotEnum.NextItem(out var item))
                Enqueue(item);
        }

        if (ents.TryGetComponent(owner, out ItemSlotsComponent? itemSlots))
        {
            foreach (var slot in itemSlots.Slots.Values)
                if (slot.HasItem && slot.Item.HasValue)
                    Enqueue(slot.Item.Value);
        }

        if (ents.TryGetComponent(owner, out ContainerManagerComponent? cmcRoot))
        {
            foreach (var container in cmcRoot.Containers.Values)
            {
                foreach (var entity in container.ContainedEntities)
                    Enqueue(entity);
            }
        }

        if (ents.TryGetComponent(owner, out HandsComponent? hands))
        {
            foreach (var hand in hands.Hands.Values)
                if (hand.HeldEntity.HasValue)
                    Enqueue(hand.HeldEntity.Value);
        }


        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;

            if (!ents.TryGetComponent(current, out ContainerManagerComponent? cmc))
                continue;

            foreach (var container in cmc.Containers.Values)
            {
                foreach (var child in container.ContainedEntities)
                    Enqueue(child);
            }
        }
    }
}

