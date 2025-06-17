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
public sealed class CapCoinHandlerSystem : EntitySystem, ICurrencyHandler
{
    #region Dependencies

    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly StackSystem _stacks = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    #endregion

    public string Id => "CapCoin";

    private string _coinPrototypeId = default!;
    private string? _stackTypeId;
    private StackPrototype? _stackPrototype;

    #region Initialization

    public override void Initialize()
    {
        base.Initialize();

        // Load currency prototype.
        if (!_prototypes.TryIndex<CurrencyPrototype>(Id, out var currencyProto))
            return;

        _coinPrototypeId = currencyProto.Entity;

        // Detect if the coin entity is a stack and cache its StackPrototype.
        if (_prototypes.TryIndex<EntityPrototype>(_coinPrototypeId, out var entityProto) &&
            entityProto.TryGetComponent(out StackComponent? stackComp, IoCManager.Resolve<IComponentFactory>()))
        {
            _stackTypeId = stackComp.StackTypeId;
            _prototypes.TryIndex(_stackTypeId, out _stackPrototype);
        }

        CurrencyRegistry.Register(this);
    }

    #endregion

    #region ICurrencyHandler implementation

    public bool CanAfford(EntityUid owner, int amount) => GetBalance(owner) >= amount;

    public int GetBalance(EntityUid owner)
    {
        var total = 0;

        foreach (var item in CurrencyHelpers.EnumerateDeepItemsUnique(owner, _entities))
            if (_entities.TryGetComponent(item, out CurrencyItemComponent? coin) && coin.Currency == Id)
                total += coin.Amount;
            else if (_stackTypeId != null &&
                _entities.TryGetComponent(item, out StackComponent? stack) &&
                stack.StackTypeId == _stackTypeId)
                total += stack.Count;

        return total;
    }

    public CurrencyOpResult Debit(EntityUid owner, int amount)
    {
        if (amount <= 0)
            return CurrencyOpResult.Invalid;
        if (!CanAfford(owner, amount))
            return CurrencyOpResult.InsufficientFunds;

        var plan = new List<(EntityUid entity, int take, bool isStack)>();
        var remaining = amount;

        foreach (var entity in CurrencyHelpers.EnumerateDeepItemsUnique(owner, _entities))
        {
            if (remaining <= 0)
                break;

            if (_entities.TryGetComponent(entity, out CurrencyItemComponent? coin) && coin.Currency == Id)
            {
                var take = Math.Min(coin.Amount, remaining);
                plan.Add((entity, take, false));
                remaining -= take;
            }
            else if (_stackTypeId != null &&
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
            Log.Warning($"[Debit] Unable to cover {amount} for {ToPrettyString(owner)}");
            return CurrencyOpResult.Invalid;
        }

        foreach (var (entity, take, isStack) in plan)
            if (isStack)
            {
                var stack = _entities.GetComponent<StackComponent>(entity);
                _stacks.SetCount(entity, stack.Count - take, stack);
                if (stack.Count - take == 0)
                    _entities.DeleteEntity(entity);
            }
            else
            {
                var coin = _entities.GetComponent<CurrencyItemComponent>(entity);
                coin.Amount -= take;
                if (coin.Amount == 0)
                    _entities.DeleteEntity(entity);
            }

        return CurrencyOpResult.Success;
    }

    public CurrencyOpResult Credit(EntityUid owner, int amount)
    {
        if (amount <= 0)
            return CurrencyOpResult.Invalid;

        var coords = _entities.GetComponent<TransformComponent>(owner).Coordinates;
        var remaining = FillPartialStacks(owner, amount);

        if (!HasFreeSlot(owner))
        {
            Log.Warning($"[Credit] No space to receive currency for {ToPrettyString(owner)}");
            return CurrencyOpResult.Invalid;
        }

        var spawned = new List<EntityUid>();
        var failed  = false;

        if (_stackPrototype != null)
        {
            if (remaining > 0)
                failed = !SpawnAndInsertStack(owner, _stackPrototype, remaining, spawned, coords);
        }
        else
        {
            for (var i = 0; i < remaining; i++)
            {
                var coin = _entities.SpawnEntity(_coinPrototypeId, coords);
                spawned.Add(coin);

                if (!TryInsertIntoBestSlot(owner, coin))
                {
                    _entities.System<SharedTransformSystem>().SetCoordinates(coin, coords);
                    Log.Warning($"[Credit] {coin} dropped on ground (no slot) for {ToPrettyString(owner)}");
                }
            }
        }

        if (failed)
        {
            foreach (var ent in spawned)
                if (_entities.EntityExists(ent))
                    _entities.DeleteEntity(ent);

            Log.Error($"[Credit] Potential currency loss on credit {amount} to {ToPrettyString(owner)}");
            return CurrencyOpResult.Invalid;
        }

        return CurrencyOpResult.Success;
    }

    #endregion

    #region Helpers

    private int FillPartialStacks(EntityUid owner, int amount)
    {
        if (_stackTypeId == null || _stackPrototype == null)
            return amount;

        foreach (var entity in CurrencyHelpers.EnumerateDeepItemsUnique(owner, _entities))
        {
            if (!_entities.TryGetComponent(entity, out StackComponent? stack) || stack.StackTypeId != _stackTypeId)
                continue;

            var max   = _stackPrototype.MaxCount.GetValueOrDefault(int.MaxValue);
            var space = Math.Max(0, max - stack.Count);
            var add   = Math.Min(space, amount);

            if (add <= 0)
                continue;

            _stacks.SetCount(entity, stack.Count + add, stack);
            amount -= add;

            if (amount == 0)
                break;
        }

        return amount;
    }

    private bool SpawnAndInsertStack(EntityUid owner, StackPrototype proto, int amount, ICollection<EntityUid> spawned, EntityCoordinates coords)
    {
        var stack = _stacks.Spawn(amount, proto, coords);
        spawned.Add(stack);

        if (TryInsertIntoBestSlot(owner, stack))
            return true;

        _entities.System<SharedTransformSystem>().SetCoordinates(stack, coords);
        Log.Warning($"[Credit] Stack {stack} dropped (no slot) for {ToPrettyString(owner)}");
        return true; // не критично: предмет лежит под ногами
    }

    private bool TryInsertIntoBestSlot(EntityUid owner, EntityUid entity)
    {
        // Item‑slots first.
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

        // Inventory slots.
        if (_entities.TryGetComponent(owner, out InventoryComponent? inventory))
        {
            foreach (var slot in inventory.Slots)
                if (_inventory.TryEquip(owner, owner, entity, slot.Name, false, false, false, inventory))
                    return true;
        }

        // Hands.
        return _entities.TryGetComponent(owner, out HandsComponent? _) &&
               _hands.TryPickupAnyHand(owner, entity, false);
    }

    private bool HasFreeSlot(EntityUid owner)
    {
        // Item‑slots.
        if (_entities.TryGetComponent(owner, out ItemSlotsComponent? itemSlots))
        {
            foreach (var slot in itemSlots.Slots.Values)
                if (!slot.Locked && !slot.HasItem)
                    return true;
        }

        // Inventory.
        if (_entities.TryGetComponent(owner, out InventoryComponent? inventory))
        {
            foreach (var slot in inventory.Slots)
                if (!_inventory.TryGetSlotEntity(owner, slot.Name, out _))
                    return true;
        }

        // Containers.
        if (_entities.TryGetComponent(owner, out ContainerManagerComponent? containers))
        {
            foreach (var container in containers.Containers.Values)
                if (container.ContainedEntities.Count < GetContainerLimit(container))
                    return true;
        }

        // Hands.
        if (_entities.TryGetComponent(owner, out HandsComponent? hands))
        {
            foreach (var hand in _hands.EnumerateHands(owner, hands))
                if (hand.HeldEntity == null)
                    return true;
        }

        return false;
    }

    private static int GetContainerLimit(BaseContainer container) =>
        container switch
    {
        ContainerSlot => 1,
        { Owner: { } owner, } when IoCManager.Resolve<IEntityManager>()
                                .TryGetComponent(owner, out StorageComponent? storage)
            => storage.Grid.GetArea(),
        _ => int.MaxValue
    };

    #endregion
}
