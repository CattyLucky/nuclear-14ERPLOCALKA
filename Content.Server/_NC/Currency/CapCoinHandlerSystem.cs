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
///     Поддерживает как стаки монет, так и одиночные сущности‑монеты.
/// </summary>
public sealed partial class CapCoinHandlerSystem : EntitySystem, ICurrencyHandler
{
    #region Dependencies

    [Dependency] private readonly IEntityManager         _entities   = default!;
    [Dependency] private readonly IPrototypeManager       _protos     = default!;
    [Dependency] private readonly StackSystem             _stacks     = default!;
    [Dependency] private readonly InventorySystem         _inventory  = default!;
    [Dependency] private readonly SharedHandsSystem       _hands      = default!;
    [Dependency] private readonly ItemSlotsSystem         _itemSlots  = default!;
    [Dependency] private readonly SharedTransformSystem   _transform  = default!;

    #endregion

    private static readonly ISawmill Sawmill = Logger.GetSawmill("capcoin");

    public string Id => "CapCoin"; // CurrencyPrototype id

    private string            _coinPrototypeId = default!; // Prefab одиночной монеты
    private string?           _stackTypeId;                // StackComponent.StackTypeId
    private StackPrototype?   _stackPrototype;             // Прототип стака (для спавна крупных сумм)
    public string? StackTypeId => _stackTypeId;
    #region Initialize / Shutdown

    public override void Initialize()
    {
        base.Initialize();

        // 1. Загружаем CurrencyPrototype (обязателен)
        if (!_protos.TryIndex<CurrencyPrototype>(Id, out var currencyProto))
        {
            Sawmill.Error($"CurrencyPrototype '{Id}' not found — CapCoinHandlerSystem disabled");
            return;
        }

        _coinPrototypeId = currencyProto.Entity;

        // 2. Определяем, стак ли это и кэшируем StackPrototype
        if (_protos.TryIndex<EntityPrototype>(_coinPrototypeId, out var entityProto) &&
            entityProto.TryGetComponent(out StackComponent? stackComp, IoCManager.Resolve<IComponentFactory>()))
        {
            _stackTypeId = stackComp.StackTypeId;
            if (!_protos.TryIndex(_stackTypeId, out _stackPrototype))
                Sawmill.Warning($"StackPrototype '{_stackTypeId}' referenced by CapCoin not found");
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

    public bool CanAfford(EntityUid owner, int amount) => GetBalance(owner) >= amount;

    public int GetBalance(EntityUid owner)
    {
        // 1. Пробуем кэш
        if (_entities.TryGetComponent(owner, out CurrencyBalanceTrackerComponent? tracker) &&
            tracker.Balances.TryGetValue(Id, out var cached))
            return cached;

        // 2. Считаем заново
        var total = 0;
        foreach (var item in CurrencyHelpers.EnumerateDeepItemsUnique(owner, _entities))
        {
            if (_entities.TryGetComponent(item, out CurrencyItemComponent? coin) && coin.Currency == Id)
            {
                total += coin.Amount;
                continue;
            }

            if (_stackTypeId != null &&
                _entities.TryGetComponent(item, out StackComponent? stack) &&
                stack.StackTypeId == _stackTypeId)
                total += stack.Count;
        }

        // 3. Записываем в кэш
        if (!_entities.HasComponent<CurrencyBalanceTrackerComponent>(owner))
            _entities.AddComponent<CurrencyBalanceTrackerComponent>(owner);

        var tracker2 = _entities.GetComponent<CurrencyBalanceTrackerComponent>(owner);
        tracker2.Balances[Id] = total;
        return total;
    }

    public CurrencyOpResult Debit(EntityUid owner, int amount)
    {
        if (amount <= 0)
            return CurrencyOpResult.Invalid;

        if (!CanAfford(owner, amount))
            return CurrencyOpResult.InsufficientFunds;

        // План (entity, take, isStack)
        var plan      = new List<(EntityUid entity, int take, bool isStack)>();
        var remaining = amount;

        foreach (var entity in CurrencyHelpers.EnumerateDeepItemsUnique(owner, _entities))
        {
            if (remaining == 0)
                break;

            // Одиночные монеты
            if (_entities.TryGetComponent(entity, out CurrencyItemComponent? coin) && coin.Currency == Id)
            {
                var take = Math.Min(coin.Amount, remaining);
                plan.Add((entity, take, false));
                remaining -= take;
                continue;
            }

            // Стаки монет
            if (_stackTypeId != null &&
                _entities.TryGetComponent(entity, out StackComponent? stack) &&
                stack.StackTypeId == _stackTypeId)
            {
                var take = Math.Min(stack.Count, remaining);
                plan.Add((entity, take, true));
                remaining -= take;
            }
        }

        if (remaining > 0)
        {
            Sawmill.Error($"[Debit] Unable to cover {amount} CapCoin for {ToPrettyString(owner)} (left {remaining})");
            return CurrencyOpResult.Invalid;
        }

        // Применяем план
        foreach (var (entity, take, isStack) in plan)
            if (isStack)
            {
                var stack      = _entities.GetComponent<StackComponent>(entity);
                var newCount   = stack.Count - take;
                _stacks.SetCount(entity, newCount, stack);
                if (newCount == 0)
                    _entities.DeleteEntity(entity);
            }
            else
            {
                var coin      = _entities.GetComponent<CurrencyItemComponent>(entity);
                var newAmount = coin.Amount - take;
                coin.Amount   = newAmount;
                _entities.Dirty(entity, coin);
                if (newAmount == 0)
                    _entities.DeleteEntity(entity);
            }

        InvalidateBalanceCache(owner);
        return CurrencyOpResult.Success;
    }

    public CurrencyOpResult Credit(EntityUid owner, int amount)
    {
        if (amount <= 0)
            return CurrencyOpResult.Invalid;

        var coords = _entities.GetComponent<TransformComponent>(owner).Coordinates;

        // 1. Сначала заполняем неполные стаки
        var remaining = FillPartialStacks(owner, amount);

        if (remaining == 0)
        {
            InvalidateBalanceCache(owner);
            return CurrencyOpResult.Success;
        }

        // 2. Проверяем, есть ли куда положить остаток
        if (!HasFreeSlot(owner))
            Sawmill.Warning($"[Credit] No free slot to receive {remaining} CapCoin for {ToPrettyString(owner)} — dropping");

        // 3. Спавним либо стаки, либо одиночные монеты
        if (_stackPrototype != null)
            SpawnAndPlaceStack(owner, _stackPrototype, remaining, coords);
        else
            SpawnAndPlaceCoins(owner, remaining, coords);

        InvalidateBalanceCache(owner);
        return CurrencyOpResult.Success;
    }

    #endregion

    #region Helpers

    /// <summary>
    ///     Заполняет неполные стаки владельца. Возвращает сколько ещё нужно выдать.
    /// </summary>
    private int FillPartialStacks(EntityUid owner, int amount)
    {
        if (_stackTypeId == null || _stackPrototype == null)
            return amount;

        foreach (var entity in CurrencyHelpers.EnumerateDeepItemsUnique(owner, _entities))
        {
            if (amount == 0)
                break;

            if (!_entities.TryGetComponent(entity, out StackComponent? stack) || stack.StackTypeId != _stackTypeId)
                continue;

            var max   = Math.Min(_stackPrototype.MaxCount ?? int.MaxValue, short.MaxValue);
            var space = Math.Max(0, max - stack.Count);
            var add   = Math.Min(space, amount);
            if (add == 0)
                continue;

            _stacks.SetCount(entity, stack.Count + add, stack);
            amount -= add;
        }

        return amount;
    }

    private void SpawnAndPlaceStack(EntityUid owner, StackPrototype proto, int amount, EntityCoordinates coords)
    {
        var ent = _stacks.Spawn(amount, proto, coords);
        if (!TryInsertIntoBestSlot(owner, ent))
        {
            _transform.SetCoordinates(ent, coords);
            Sawmill.Warning($"[Credit] Spawned stack {ent} dropped on ground for {ToPrettyString(owner)}");
        }
    }

    private void SpawnAndPlaceCoins(EntityUid owner, int amount, EntityCoordinates coords)
    {
        for (var i = 0; i < amount; i++)
        {
            var ent = _entities.SpawnEntity(_coinPrototypeId, coords);
            if (!TryInsertIntoBestSlot(owner, ent))
            {
                _transform.SetCoordinates(ent, coords);
                Sawmill.Warning($"[Credit] Spawned coin {ent} dropped on ground for {ToPrettyString(owner)}");
            }
        }
    }

    public void InvalidateBalanceCache(EntityUid owner)
    {
        if (_entities.TryGetComponent(owner, out CurrencyBalanceTrackerComponent? tracker))
            tracker.Balances.Remove(Id);
    }

    /// <summary>
    ///     Пытается вставить сущность в слоты, инвентарь или руки владельца.
    /// </summary>
    private bool TryInsertIntoBestSlot(EntityUid owner, EntityUid entity)
    {
        // Item‑slots
        if (_entities.TryGetComponent(owner, out ItemSlotsComponent? itemSlots))
        {
            foreach (var slot in itemSlots.Slots.Values)
            {
                if (slot.Locked || slot.HasItem)
                    continue;

                if (_itemSlots.TryInsert(owner, slot, entity, owner))
                    return true;
            }
        }

        // Inventory
        if (_entities.TryGetComponent(owner, out InventoryComponent? inventory))
        {
            foreach (var slot in inventory.Slots)
            {
                if (_inventory.TryGetSlotEntity(owner, slot.Name, out _))
                    continue; // занят

                if (_inventory.TryEquip(owner, owner, entity, slot.Name, false, false, false, inventory))
                    return true;
            }
        }

        // Hands
        return _entities.TryGetComponent(owner, out HandsComponent? _) &&
               _hands.TryPickupAnyHand(owner, entity, false);
    }

    private bool HasFreeSlot(EntityUid owner)
    {
        // Item‑slots
        if (_entities.TryGetComponent(owner, out ItemSlotsComponent? itemSlots))
        {
            foreach (var slot in itemSlots.Slots.Values)
                if (!slot.Locked && !slot.HasItem)
                    return true;
        }

        // Inventory
        if (_entities.TryGetComponent(owner, out InventoryComponent? inventory))
        {
            foreach (var slot in inventory.Slots)
                if (!_inventory.TryGetSlotEntity(owner, slot.Name, out _))
                    return true;
        }

        // Hands
        if (_entities.TryGetComponent(owner, out HandsComponent? hands))
        {
            foreach (var hand in _hands.EnumerateHands(owner, hands))
                if (hand.HeldEntity == null)
                    return true;
        }

        // Контейнеры / Storage
        if (_entities.TryGetComponent(owner, out ContainerManagerComponent? containers))
        {
            foreach (var container in containers.Containers.Values)
                if (container.ContainedEntities.Count < GetContainerLimit(container))
                    return true;
        }

        return false;
    }

    private static int GetContainerLimit(BaseContainer container) =>
        container switch
        {
            ContainerSlot                                         => 1,
            { Owner: { } owner, } when IoCManager.Resolve<IEntityManager>()
                .TryGetComponent(owner, out StorageComponent? storage)
                => storage.Grid.GetArea(),
            _                                                    => int.MaxValue
        };

    #endregion
}
