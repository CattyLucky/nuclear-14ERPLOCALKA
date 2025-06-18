using Content.Server.Stack;
using Content.Shared._NC.Currency;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._NC.Currency;

/// <summary>
///     Server‑side currency handler for <c>CapCoin</c>.
///     Works with both stack‑based coins and individual coin entities.
/// </summary>
public sealed partial class CapCoinHandlerSystem : EntitySystem, ICurrencyHandler
{
    #region Dependencies

    [Dependency] private readonly IEntityManager       _ents      = default!;
    [Dependency] private readonly IPrototypeManager    _protos    = default!;
    [Dependency] private readonly StackSystem          _stacks    = default!;
    [Dependency] private readonly InventorySystem      _invSys    = default!;
    [Dependency] private readonly SharedHandsSystem    _handsSys  = default!;
    [Dependency] private readonly ItemSlotsSystem      _itemSlots = default!;
    [Dependency] private readonly SharedTransformSystem _xform    = default!;

    #endregion

    private static readonly ISawmill Sawmill = Logger.GetSawmill("capcoin");

    public string Id => "CapCoin";

    private string          _coinProtoId = default!;
    private string?         _stackTypeId;
    private StackPrototype? _stackProto;
    public  string?         StackTypeId => _stackTypeId;

    #region Initialize / Shutdown

    public override void Initialize()
    {
        base.Initialize();

        if (!_protos.TryIndex<CurrencyPrototype>(Id, out var currencyProto))
        {
            Sawmill.Error($"CurrencyPrototype '{Id}' not found — CapCoin disabled.");
            return;
        }

        _coinProtoId = currencyProto.Entity;

        // Определяем, является ли монета стаком
        if (_protos.TryIndex<EntityPrototype>(_coinProtoId, out var entityProto) &&
            entityProto.TryGetComponent(out StackComponent? stackComp, IoCManager.Resolve<IComponentFactory>()))
        {
            _stackTypeId = stackComp.StackTypeId;

            if (!_protos.TryIndex(_stackTypeId, out _stackProto))
                Sawmill.Warning($"StackPrototype '{_stackTypeId}' not found.");
        }

        CurrencyRegistry.Register(this);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CurrencyRegistry.Unregister(Id);
    }

    #endregion

    #region ICurrencyHandler

    public bool CanAfford(EntityUid owner, int amount) =>
        GetBalance(owner) >= amount;

    public int GetBalance(EntityUid owner)
    {
        // 1. Кэш
        if (_ents.TryGetComponent(owner, out CurrencyBalanceTrackerComponent? tracker) &&
            tracker.Balances.TryGetValue(Id, out var cached))
            return cached;

        // 2. Пересчёт
        var total = 0;
        foreach (var uid in CurrencyHelpers.EnumerateDeepItemsUnique(owner, _ents))
        {
            if (_ents.TryGetComponent(uid, out CurrencyItemComponent? coin) && coin.Currency == Id)
            {
                total += coin.Amount;
                continue;
            }

            if (_stackTypeId != null &&
                _ents.TryGetComponent(uid, out StackComponent? stack) &&
                stack.StackTypeId == _stackTypeId)
                total += stack.Count;
        }

        // 3. Кэшируем (без Dirty, тут не нужен лишний нетворкинг)
        if (!_ents.HasComponent<CurrencyBalanceTrackerComponent>(owner))
            _ents.AddComponent<CurrencyBalanceTrackerComponent>(owner);

        var trackerRef = _ents.GetComponent<CurrencyBalanceTrackerComponent>(owner);
        trackerRef.Balances[Id] = total;
        // Не вызываем _ents.Dirty тут — кэш только для сервера, не для клиента!

        return total;
    }

    public CurrencyOpResult Debit(EntityUid owner, int amount)
    {
        if (amount <= 0)
        {
            Sawmill.Warning($"[Debit] Requested to debit 0 or negative CapCoin ({amount}) for {owner}");
            return CurrencyOpResult.Invalid;
        }

        if (!CanAfford(owner, amount))
            return CurrencyOpResult.InsufficientFunds;

        var plan      = new List<(EntityUid uid, int take, bool isStack)>();
        var remaining = amount;

        foreach (var uid in CurrencyHelpers.EnumerateDeepItemsUnique(owner, _ents))
        {
            if (remaining == 0)
                break;

            // Одиночные монеты
            if (_ents.TryGetComponent(uid, out CurrencyItemComponent? coin) && coin.Currency == Id)
            {
                var take = Math.Min(coin.Amount, remaining);
                plan.Add((uid, take, false));
                remaining -= take;
                continue;
            }

            // Стаки
            if (_stackTypeId != null &&
                _ents.TryGetComponent(uid, out StackComponent? stack) &&
                stack.StackTypeId == _stackTypeId)
            {
                var take = Math.Min(stack.Count, remaining);
                plan.Add((uid, take, true));
                remaining -= take;
            }
        }

        if (remaining > 0)
        {
            Sawmill.Error($"[Debit] Can't cover {amount} CapCoin for {owner} (missing {remaining}).");
            return CurrencyOpResult.Invalid;
        }

        foreach (var (uid, take, isStack) in plan)
            if (isStack)
            {
                var stack = _ents.GetComponent<StackComponent>(uid);
                var after = stack.Count - take;
                if (after < 0)
                {
                    Sawmill.Error($"[Debit] Tried to overdraw stack {uid}: stack.Count={stack.Count}, take={take}");
                    continue;
                }
                _stacks.SetCount(uid, after, stack);
                if (after == 0)
                    _ents.DeleteEntity(uid);
            }
            else
            {
                var coin = _ents.GetComponent<CurrencyItemComponent>(uid);
                var after = coin.Amount - take;
                if (after < 0)
                {
                    Sawmill.Error($"[Debit] Tried to overdraw coin {uid}: coin.Amount={coin.Amount}, take={take}");
                    continue;
                }
                if (after == 0)
                    _ents.DeleteEntity(uid);
                else
                {
                    coin.Amount = after;
                    _ents.Dirty(uid, coin);
                }
            }

        // Инвалидируем кэш только через событие (CurrencyCacheInvalidationSystem)
        RaiseInvalidateBalanceEvent(owner);

        return CurrencyOpResult.Success;
    }

    public CurrencyOpResult Credit(EntityUid owner, int amount)
    {
        if (amount <= 0)
        {
            Sawmill.Warning($"[Credit] Requested to credit 0 or negative CapCoin ({amount}) for {owner}");
            return CurrencyOpResult.Invalid;
        }

        var coords    = _ents.GetComponent<TransformComponent>(owner).Coordinates;
        var remaining = FillPartialStacks(owner, amount);

        if (remaining == 0)
        {
            RaiseInvalidateBalanceEvent(owner);
            return CurrencyOpResult.Success;
        }

        if (!HasFreeSlot(owner))
            Sawmill.Warning($"[Credit] No free slot for {remaining} CapCoin – dropping to ground.");

        if (_stackProto != null)
        {
            var max = _stackProto.MaxCount ?? int.MaxValue;
            while (remaining > 0)
            {
                var spawnCount = Math.Min(remaining, max);
                SpawnStack(owner, _stackProto, spawnCount, coords);
                remaining -= spawnCount;
            }
        }
        else
            SpawnCoins(owner, remaining, coords);

        RaiseInvalidateBalanceEvent(owner);
        return CurrencyOpResult.Success;
    }

    /// <summary>
    /// Не вызывайте это напрямую — используйте систему инвалидирования!
    /// </summary>
    public void InvalidateBalanceCache(EntityUid owner)
    {
        if (_ents.TryGetComponent(owner, out CurrencyBalanceTrackerComponent? tracker) &&
            tracker.Balances.Remove(Id))
        {
            // Кэш только серверный, нет Dirty
        }
    }

    /// <summary>
    /// Вызвать событие для сброса кэша через CurrencyCacheInvalidationSystem
    /// </summary>
    private void RaiseInvalidateBalanceEvent(EntityUid owner)
    {
        var ev = new CurrencyCacheInvalidateEvent(owner, Id);
        RaiseLocalEvent(owner, ev);
    }

    #endregion

    #region Internal helpers

    private int FillPartialStacks(EntityUid owner, int amount)
    {
        if (_stackTypeId == null || _stackProto == null)
            return amount;

        var max = _stackProto.MaxCount ?? int.MaxValue;

        foreach (var uid in CurrencyHelpers.EnumerateDeepItemsUnique(owner, _ents))
        {
            if (amount == 0)
                break;

            if (!_ents.TryGetComponent(uid, out StackComponent? stack) || stack.StackTypeId != _stackTypeId)
                continue;

            var current = Math.Min(stack.Count, max); // исправляем жирные стаки
            if (current != stack.Count)
            {
                Sawmill.Error($"[FillPartialStacks] Fat stack {uid} with {stack.Count} CapCoin, capping to {max}.");
                _stacks.SetCount(uid, current, stack);
            }

            var space = max - current;
            var add   = Math.Min(space, amount);

            if (add == 0)
                continue;

            _stacks.SetCount(uid, current + add, stack);
            amount -= add;
        }

        return amount;
    }

    private void SpawnStack(EntityUid owner, StackPrototype proto, int amount, EntityCoordinates coords)
    {
        var uid = _stacks.Spawn(amount, proto, coords);
        if (!TryInsertBest(owner, uid))
        {
            _xform.SetCoordinates(uid, coords);
            Sawmill.Warning($"[Credit] Stack {uid} dropped on ground.");
        }
    }

    private void SpawnCoins(EntityUid owner, int amount, EntityCoordinates coords)
    {
        for (var i = 0; i < amount; i++)
        {
            var uid = _ents.SpawnEntity(_coinProtoId, coords);
            if (!TryInsertBest(owner, uid))
            {
                _xform.SetCoordinates(uid, coords);
                Sawmill.Warning($"[Credit] Coin {uid} dropped on ground.");
            }
        }
    }

    private bool TryInsertBest(EntityUid owner, EntityUid item)
    {
        // item-slots
        if (_ents.TryGetComponent(owner, out ItemSlotsComponent? slots))
        {
            foreach (var slot in slots.Slots.Values)
                if (!slot.Locked && !slot.HasItem && _itemSlots.TryInsert(owner, slot, item, owner))
                    return true;
        }

        // inventory
        if (_ents.TryGetComponent(owner, out InventoryComponent? inv))
        {
            foreach (var def in inv.Slots)
            {
                if (_invSys.TryGetSlotEntity(owner, def.Name, out _))
                    continue;

                if (_invSys.TryEquip(owner, owner, item, def.Name, false, false, false, inv))
                    return true;
            }
        }

        // hands
        return _ents.TryGetComponent(owner, out HandsComponent? _) &&
               _handsSys.TryPickupAnyHand(owner, item, false);
    }

    private bool HasFreeSlot(EntityUid owner)
    {
        // item-slots
        if (_ents.TryGetComponent(owner, out ItemSlotsComponent? slots))
        {
            foreach (var slot in slots.Slots.Values)
                if (!slot.Locked && !slot.HasItem)
                    return true;
        }

        // inventory
        if (_ents.TryGetComponent(owner, out InventoryComponent? inv))
        {
            foreach (var def in inv.Slots)
                if (!_invSys.TryGetSlotEntity(owner, def.Name, out _))
                    return true;
        }

        // hands
        if (_ents.TryGetComponent(owner, out HandsComponent? hands))
        {
            foreach (var hand in _handsSys.EnumerateHands(owner, hands))
                if (hand.HeldEntity == null)
                    return true;
        }

        // storage containers
        if (_ents.TryGetComponent(owner, out ContainerManagerComponent? cmc))
        {
            var em = _ents;
            foreach (var cont in cmc.Containers.Values)
                if (cont.ContainedEntities.Count < GetContainerLimit(cont, em))
                    return true;
        }

        return false;
    }

    private static int GetContainerLimit(BaseContainer container, IEntityManager em) =>
        container switch
        {
            ContainerSlot => 1,
            { Owner: var own, } when em.TryGetComponent(own, out StorageComponent? storage)
                => storage.Grid.GetArea(),
            _ => int.MaxValue
        };

    #endregion
}
