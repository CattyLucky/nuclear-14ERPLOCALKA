using System.Linq;
using Content.Shared._NC.Trade;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;


namespace Content.Server._NC.Trade;


public sealed class NcStoreLogicSystem : EntitySystem
{
    private static readonly ISawmill Sawmill = Logger.GetSawmill("ncstore-logic");
    [Dependency] private readonly IEntityManager _ents = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly SharedStackSystem _stacks = default!;

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

    private bool TryPickCurrencyForBuy(
        NcStoreComponent store,
        StoreListingPrototype listing,
        EntityUid user,
        out string currency,
        out int price
    )
    {
        currency = string.Empty;
        price = 0;

        var balances = new Dictionary<string, int>(store.CurrencyWhitelist.Count);
        foreach (var cur in store.CurrencyWhitelist)
            balances[cur] = GetBalance(user, cur);

        foreach (var cur in store.CurrencyWhitelist)
        {
            if (!listing.Cost.TryGetValue(cur, out var priceF))
                continue;

            var p = (int) MathF.Ceiling(priceF);
            if (p <= 0)
                continue;

            if (!balances.TryGetValue(cur, out var bal) || bal < p)
                continue;

            currency = cur;
            price = p;
            return true;
        }

        return false;
    }


    private bool TryPickCurrencyForSell(
        NcStoreComponent store,
        StoreListingPrototype listing,
        out string currency,
        out int price
    )
    {
        currency = string.Empty;
        price = 0;

        foreach (var cur in store.CurrencyWhitelist)
        {
            if (!listing.Cost.TryGetValue(cur, out var priceF))
                continue;

            var p = (int) MathF.Ceiling(priceF);
            if (p <= 0)
                continue;

            currency = cur;
            price = p;
            return true;
        }

        return false;
    }

    public bool TryBuy(string listingId, EntityUid machine, NcStoreComponent? store, EntityUid user)
    {
        if (store == null || store.Listings.Count == 0)
            return false;

        var listing = store.Listings.FirstOrDefault(x => x.Id == listingId && x.Mode == StoreMode.Buy);
        if (listing == null)
            return false;

        if (!_protos.TryIndex<EntityPrototype>(listing.ProductEntity, out _))
            return false;

        if (!TryPickCurrencyForBuy(store, listing, user, out var currency, out var price))
            return false;

        if (!TryTakeCurrency(user, currency, price))
            return false;

        if (!TrySpawnProduct(listing.ProductEntity, user))
        {
            GiveCurrency(user, currency, price);
            return false;
        }

        Sawmill.Info($"TryBuy: OK {listing.ProductEntity} x1 for {price} {currency}");
        return true;
    }


    public bool TrySell(string listingId, EntityUid machine, NcStoreComponent? store, EntityUid user)
    {
        if (store == null || store.Listings.Count == 0)
            return false;

        var listing = store.Listings.FirstOrDefault(x => x.Id == listingId && x.Mode == StoreMode.Sell);
        if (listing == null)
            return false;

        if (!TryPickCurrencyForSell(store, listing, out var currency, out var price))
            return false;

        if (price <= 0)
            return false;

        if (!_protos.TryIndex<StackPrototype>(currency, out _))
            return false;

        if (!RemoveItemsByProto(user, listing.ProductEntity, 1))
            return false;

        GiveCurrency(user, currency, price);
        Sawmill.Info($"TrySell: SELL success. User {user} sold {listing.ProductEntity} for {currency} ({price})");
        return true;
    }


    public bool TryExchange(
        string listingId,
        EntityUid machine,
        NcStoreComponent? store,
        EntityUid user,
        StoreExchangeListingBoundUiMessage msg
    ) =>
        false;

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
        if (amount <= 0)
            return true;

        var cands = new List<(EntityUid Ent, int Count)>();
        var total = 0;
        foreach (var ent in EnumerateDeepItemsUnique(user))
            if (_ents.TryGetComponent(ent, out StackComponent? st)
                && st.StackTypeId == stackType
                && st.Count > 0)
            {
                cands.Add((ent, st.Count));
                total += st.Count;
            }

        if (total < amount)
            return false;
        cands.Sort((a, b) => a.Count.CompareTo(b.Count));

        var left = amount;
        foreach (var (ent, have) in cands)
        {
            if (left <= 0)
                break;

            var take = Math.Min(have, left);
            if (_ents.TryGetComponent(ent, out StackComponent? st))
            {
                var newCount = st.Count - take;
                _stacks.SetCount(ent, newCount, st);
                if (newCount <= 0 && _ents.EntityExists(ent))
                    _ents.DeleteEntity(ent);
            }

            left -= take;
        }

        return true;
    }


    private void GiveCurrency(EntityUid user, string stackType, int amount)
    {
        if (amount <= 0)
            return;

        if (!_protos.TryIndex<StackPrototype>(stackType, out var proto))
            return;

        foreach (var ent in EnumerateDeepItemsUnique(user))
        {
            if (amount <= 0)
                break;

            if (!_ents.TryGetComponent(ent, out StackComponent? st) || st.StackTypeId != stackType)
                continue;

            if (proto.MaxCount is { } max)
            {
                var canAdd = Math.Max(0, max - st.Count);
                if (canAdd <= 0)
                    continue;

                var add = Math.Min(canAdd, amount);
                _stacks.SetCount(ent, st.Count + add, st);
                amount -= add;
            }
            else
            {
                _stacks.SetCount(ent, st.Count + amount, st);
                amount = 0;
                break;
            }
        }

        if (amount <= 0)
            return;

        var coords = _ents.GetComponent<TransformComponent>(user).Coordinates;

        while (amount > 0)
        {
            var add = proto.MaxCount is { } maxPerStack
                ? Math.Min(amount, Math.Max(1, maxPerStack))
                : amount;

            var spawned = _ents.SpawnEntity(proto.Spawn, coords);

            if (_ents.TryGetComponent(spawned, out StackComponent? newStack))
                _stacks.SetCount(spawned, add, newStack);

            if (_ents.HasComponent<HandsComponent>(user))
                _hands.TryPickupAnyHand(user, spawned, false);

            amount -= add;
        }
    }


    private int CountItemsByProto(EntityUid user, string protoId)
    {
        var total = 0;

        foreach (var ent in EnumerateDeepItemsUnique(user))
        {
            if (!_ents.TryGetComponent(ent, out MetaDataComponent? meta))
                continue;
            if (meta.EntityPrototype?.ID != protoId)
                continue;

            if (_ents.TryGetComponent(ent, out StackComponent? stack))
                total += Math.Max(stack.Count, 0);
            else
                total += 1;
        }

        return total;
    }

    private bool RemoveItemsByProto(EntityUid user, string protoId, int count)
    {
        if (count <= 0)
            return true;

        var have = CountItemsByProto(user, protoId);
        if (have < count)
            return false;

        var candidates = new List<(EntityUid Ent, int Available, bool IsStack)>();
        foreach (var ent in EnumerateDeepItemsUnique(user))
        {
            if (!_ents.TryGetComponent(ent, out MetaDataComponent? meta))
                continue;
            if (meta.EntityPrototype?.ID != protoId)
                continue;

            if (_ents.TryGetComponent(ent, out StackComponent? stack))
                candidates.Add((ent, Math.Max(stack.Count, 0), true));
            else
                candidates.Add((ent, 1, false));
        }

        candidates.Sort((a, b) =>
        {
            if (a.IsStack != b.IsStack)
                return a.IsStack ? -1 : 1;
            return a.Available.CompareTo(b.Available);
        });

        var left = count;
        foreach (var (ent, available, isStack) in candidates)
        {
            if (left <= 0)
                break;

            var take = Math.Min(available, left);

            if (isStack)
            {
                if (_ents.TryGetComponent(ent, out StackComponent? stack))
                {
                    var newCount = stack.Count - take;
                    _stacks.SetCount(ent, newCount, stack);
                    if (newCount <= 0 && _ents.EntityExists(ent))
                        _ents.DeleteEntity(ent);
                }
            }
            else
            {
                if (_ents.EntityExists(ent))
                    _ents.DeleteEntity(ent);
            }

            left -= take;
        }

        return left <= 0;
    }


    private bool TrySpawnProduct(string protoId, EntityUid user)
    {
        try
        {
            var coords = _ents.GetComponent<TransformComponent>(user).Coordinates;
            var spawned = _ents.SpawnEntity(protoId, coords);
            if (_ents.HasComponent<HandsComponent>(user))
                _hands.TryPickupAnyHand(user, spawned, false);
            return true;
        }
        catch (Exception e)
        {
            Sawmill.Error($"Spawn failed for {protoId}: {e}");
            return false;
        }
    }
}
