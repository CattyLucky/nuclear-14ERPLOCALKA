using System.Linq;
using Content.Shared._NC.Trade;
using Robust.Shared.Prototypes;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Stacks;
using Content.Shared.Inventory;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Containers;

namespace Content.Server._NC.Trade;

public sealed class NcStoreLogicSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _ents = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly SharedStackSystem _stacks = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    private static readonly ISawmill Sawmill = Logger.GetSawmill("ncstore-logic");

    public int GetBalance(EntityUid user, string stackType)
    {
        Sawmill.Debug($"GetBalance: user={user}, stackType={stackType}");
        var total = 0;
        foreach (var entity in EnumerateDeepItemsUnique(user))
            if (_ents.TryGetComponent(entity, out StackComponent? stack)
                && stack.StackTypeId == stackType)
                total += stack.Count;

        Sawmill.Debug($"GetBalance: user={user}, stackType={stackType}, balance={total}");
        return total;
    }
    public bool TryBuy(string listingId, EntityUid machine, NcStoreComponent? store, EntityUid user)
    {
        if (store == null || store.Listings.Count == 0)
            return false;

        var listing = store.Listings.FirstOrDefault(x => x.Id == listingId && x.Mode == StoreMode.Buy);
        if (listing == null)
            return false;

        var price = (int)listing.Cost.First().Value;
        var currencyStackType = store.CurrencyWhitelist.FirstOrDefault();
        if (string.IsNullOrEmpty(currencyStackType))
            return false;

        if (GetBalance(user, currencyStackType) < price)
            return false;
        if (!TryTakeCurrency(user, currencyStackType, price))
            return false;

        SpawnProduct(listing.ProductEntity, user);
        Sawmill.Info($"TryBuy: BUY success. Spawning {listing.ProductEntity}");
        return true;
    }

    public bool TrySell(string listingId, EntityUid machine, NcStoreComponent? store, EntityUid user)
    {
        if (store == null || store.Listings.Count == 0)
            return false;

        var listing = store.Listings.FirstOrDefault(x => x.Id == listingId && x.Mode == StoreMode.Sell);
        if (listing == null)
            return false;

        var price = Math.Abs((int)listing.Cost.First().Value);
        var currencyStackType = store.CurrencyWhitelist.FirstOrDefault();
        if (string.IsNullOrEmpty(currencyStackType))
            return false;

        if (!RemoveItemByProto(user, listing.ProductEntity))
            return false;

        GiveCurrency(user, currencyStackType, price);
        Sawmill.Info($"TrySell: SELL success. User {user} sold {listing.ProductEntity} for {currencyStackType} ({price})");
        return true;
    }


    public bool TryExchange(string listingId, EntityUid machine, NcStoreComponent? store, EntityUid user, StoreExchangeListingBoundUiMessage msg) => false;

    private IEnumerable<EntityUid> EnumerateDeepItemsUnique(EntityUid owner)
    {
        var visited = new HashSet<EntityUid>();

        void Enqueue(EntityUid uid, Queue<EntityUid> queue)
        {
            if (visited.Add(uid))
                queue.Enqueue(uid);
        }

        var queue = new Queue<EntityUid>();

        if (_ents.TryGetComponent(owner, out InventoryComponent? inventory))
        {
            var slotEnum = new InventorySystem.InventorySlotEnumerator(inventory);
            while (slotEnum.NextItem(out var item))
                Enqueue(item, queue);
        }

        if (_ents.TryGetComponent(owner, out ItemSlotsComponent? itemSlots))
        {
            foreach (var slot in itemSlots.Slots.Values)
                if (slot.HasItem && slot.Item.HasValue)
                    Enqueue(slot.Item.Value, queue);
        }

        if (_ents.TryGetComponent(owner, out HandsComponent? hands))
        {
            foreach (var hand in hands.Hands.Values)
                if (hand.HeldEntity.HasValue)
                    Enqueue(hand.HeldEntity.Value, queue);
        }

        if (_ents.TryGetComponent(owner, out ContainerManagerComponent? cmcRoot))
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

            if (_ents.TryGetComponent(current, out ContainerManagerComponent? cmc))
            {
                foreach (var container in cmc.Containers.Values)
                {
                    foreach (var child in container.ContainedEntities)
                        Enqueue(child, queue);
                }
            }
        }
    }

    private bool TryTakeCurrency(EntityUid user, string stackType, int amount)
    {
        foreach (var entity in EnumerateDeepItemsUnique(user))
            if (_ents.TryGetComponent(entity, out StackComponent? stack)
                && stack.StackTypeId == stackType)
            {
                var toRemove = Math.Min(stack.Count, amount);
                _ents.System<SharedStackSystem>().SetCount(entity, stack.Count - toRemove, stack);
                amount -= toRemove;
                if (amount <= 0)
                    return true;
            }

        return false;
    }

    private void GiveCurrency(EntityUid user, string stackType, int amount)
    {
        if (amount <= 0)
            return;

        foreach (var ent in EnumerateDeepItemsUnique(user))
            if (_ents.TryGetComponent(ent, out StackComponent? stack) &&
                stack.StackTypeId == stackType)
            {
                stack.Unlimited = true;
                _stacks.SetCount(ent, stack.Count + amount, stack);
                return;
            }

        if (!_protos.TryIndex<StackPrototype>(stackType, out var proto))
            return;

        var coords  = _ents.GetComponent<TransformComponent>(user).Coordinates;
        var spawned = _ents.SpawnEntity(proto.Spawn, coords);

        if (_ents.TryGetComponent(spawned, out StackComponent? newStack))
        {
            newStack.Unlimited = true;
            _stacks.SetCount(spawned, amount, newStack);
        }

        if (_ents.HasComponent<HandsComponent>(user))
            _hands.TryPickupAnyHand(user, spawned, checkActionBlocker: false);
    }


    private bool RemoveItemByProto(EntityUid user, string protoId)
    {
        foreach (var entity in EnumerateDeepItemsUnique(user))
        {
            var meta = _ents.GetComponent<MetaDataComponent>(entity);
            if (meta.EntityPrototype?.ID == protoId)
            {
                if (_ents.TryGetComponent(entity, out StackComponent? stack) && stack.Count > 1)
                    _stacks.SetCount(entity, stack.Count - 1, stack);
                else
                    _ents.DeleteEntity(entity);
                return true;
            }
        }
        return false;
    }


    private void SpawnProduct(string protoId, EntityUid user)
    {
        var userXform = _ents.GetComponent<TransformComponent>(user);
        var coords = userXform.Coordinates;
        var spawned = _ents.SpawnEntity(protoId, coords);

        if (_ents.HasComponent<HandsComponent>(user))
            _hands.TryPickupAnyHand(user, spawned, checkActionBlocker: false);
    }
}
