using Content.Server.Stack;
using Content.Shared._NC.Currency;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server._NC.Currency;

public sealed class CapCoinHandlerSystem : EntitySystem, ICurrencyHandler
{
    #region Dependencies

    [Dependency] private readonly IEntityManager _ents = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly StackSystem _stacks = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    #endregion

    private static readonly ISawmill Sawmill = Logger.GetSawmill("capcoin");

    public string Id => "CapCoin";
    public string? StackTypeId => _stackTypeId;

    private string _stackTypeId = default!;
    private StackPrototype? _stackProto;

    public override void Initialize()
    {
        Sawmill.Info("Initializing CapCoinHandlerSystem...");

        if (!_protos.TryIndex<CurrencyPrototype>(Id, out var currencyProto))
        {
            Sawmill.Error($"CurrencyPrototype '{Id}' not found — CapCoin disabled.");
            return;
        }

        var entityProtoId = currencyProto.Entity;
        if (!_protos.TryIndex<EntityPrototype>(entityProtoId, out var entityProto) ||
            !entityProto.TryGetComponent(out StackComponent? stackComp, IoCManager.Resolve<IComponentFactory>()))
        {
            Sawmill.Error($"Entity '{entityProtoId}' for CapCoin does not have a StackComponent!");
            return;
        }

        _stackTypeId = stackComp.StackTypeId;
        if (!_protos.TryIndex(_stackTypeId, out _stackProto))
            Sawmill.Warning($"StackPrototype '{_stackTypeId}' not found.");
        else
            Sawmill.Info($"Detected CapCoin as stack-based currency. StackTypeId: {_stackTypeId}");

        if (CurrencyRegistry.Register(this))
            Sawmill.Info("Registered CapCoinHandlerSystem in CurrencyRegistry.");
        else
            Sawmill.Warning("CapCoinHandlerSystem registration in CurrencyRegistry failed (duplicate?).");
    }

    public override void Shutdown()
    {
        Sawmill.Info("Shutting down CapCoinHandlerSystem...");
        base.Shutdown();
        CurrencyRegistry.Unregister(Id);
        Sawmill.Info("Unregistered CapCoinHandlerSystem from CurrencyRegistry.");
    }

    public int GetBalance(EntityUid owner)
    {
        Sawmill.Debug($"GetBalance called for {owner}");

        if (_ents.TryGetComponent(owner, out CurrencyBalanceTrackerComponent? tracker) &&
            tracker.Balances.TryGetValue(Id, out var cached))
        {
            Sawmill.Debug($"[GetBalance] Cache HIT for {owner} ({Id}): {cached}");
            return cached;
        }

        var total = 0;
        foreach (var uid in CurrencyHelpers.EnumerateDeepItemsUnique(owner, _ents))
        {
            if (_ents.TryGetComponent(uid, out StackComponent? stack) &&
                stack.StackTypeId == _stackTypeId)
            {
                total += stack.Count;
                Sawmill.Debug($"[GetBalance] Found stack {uid}, +{stack.Count}, running total {total}");
            }
        }

        if (!_ents.HasComponent<CurrencyBalanceTrackerComponent>(owner))
        {
            Sawmill.Debug($"[GetBalance] Adding CurrencyBalanceTrackerComponent for {owner}");
            _ents.AddComponent<CurrencyBalanceTrackerComponent>(owner);
        }

        var trackerRef = _ents.GetComponent<CurrencyBalanceTrackerComponent>(owner);
        trackerRef.Set(owner, Id, total, _ents);
        Sawmill.Debug($"[GetBalance] Set cache for {owner} ({Id}): {total}");

        return total;
    }

    public bool CanAfford(EntityUid owner, int amount)
    {
        var can = GetBalance(owner) >= amount;
        Sawmill.Debug($"CanAfford({owner}, {amount}) = {can}");
        return can;
    }

    public CurrencyOpResult Debit(EntityUid owner, int amount)
    {
        Sawmill.Info($"[Debit] Request: {amount} CapCoin from {owner}");

        if (amount <= 0)
        {
            Sawmill.Warning($"[Debit] Requested to debit 0 or negative CapCoin ({amount}) for {owner}");
            return CurrencyOpResult.Invalid;
        }

        if (!CanAfford(owner, amount))
        {
            Sawmill.Warning($"[Debit] Not enough CapCoin for {owner}: requested {amount}, has {GetBalance(owner)}");
            return CurrencyOpResult.InsufficientFunds;
        }

        var remaining = amount;

        foreach (var uid in CurrencyHelpers.EnumerateDeepItemsUnique(owner, _ents))
        {
            if (remaining == 0)
                break;

            if (!_ents.TryGetComponent(uid, out StackComponent? stack) || stack.StackTypeId != _stackTypeId)
                continue;

            var take = Math.Min(stack.Count, remaining);
            _stacks.SetCount(uid, stack.Count - take, stack);
            Sawmill.Debug($"[Debit] Stack {uid}: set count to {stack.Count - take}");
            if (stack.Count - take <= 0)
            {
                _ents.DeleteEntity(uid);
                Sawmill.Debug($"[Debit] Stack {uid} deleted");
            }
            remaining -= take;
        }

        if (remaining > 0)
        {
            Sawmill.Error($"[Debit] Logic error: Could not debit full amount for {owner} (left: {remaining})");
            return CurrencyOpResult.Invalid;
        }

        RaiseInvalidateBalanceEvent(owner);
        Sawmill.Info($"[Debit] SUCCESS: Debited {amount} CapCoin from {owner}");
        return CurrencyOpResult.Success;
    }

    public CurrencyOpResult Credit(EntityUid owner, int amount)
    {
        Sawmill.Info($"[Credit] Request: {amount} CapCoin to {owner}");

        if (amount <= 0)
        {
            Sawmill.Warning($"[Credit] Requested to credit 0 or negative CapCoin ({amount}) for {owner}");
            return CurrencyOpResult.Invalid;
        }

        var coords = _ents.GetComponent<TransformComponent>(owner).Coordinates;
        EntityUid? firstStack = null;

        // Попробуй найти хотя бы один существующий стек у владельца
        foreach (var uid in CurrencyHelpers.EnumerateDeepItemsUnique(owner, _ents))
        {
            if (_ents.TryGetComponent(uid, out StackComponent? stack) && stack.StackTypeId == _stackTypeId)
            {
                _stacks.SetCount(uid, stack.Count + amount, stack);
                Sawmill.Debug($"[Credit] Added {amount} to stack {uid} (new count: {stack.Count + amount})");
                firstStack = uid;
                break;
            }
        }

        // Если не нашли ни одного стака — спавним новый
        if (firstStack == null)
        {
            var uid = _stacks.Spawn(amount, _stackProto!, coords);
            Sawmill.Debug($"[Credit] Spawned new stack {uid} ({amount}) for {owner}");
            if (!TryInsertBest(owner, uid))
            {
                _xform.SetCoordinates(uid, coords);
                Sawmill.Warning($"[Credit] Stack {uid} dropped on ground.");
            }
        }

        RaiseInvalidateBalanceEvent(owner);
        Sawmill.Info($"[Credit] SUCCESS: Credited {amount} CapCoin to {owner}");
        return CurrencyOpResult.Success;
    }

    public void InvalidateBalanceCache(EntityUid owner)
    {
        Sawmill.Debug($"InvalidateBalanceCache for {owner} ({Id})");
        if (_ents.TryGetComponent(owner, out CurrencyBalanceTrackerComponent? tracker))
        {
            tracker.Remove(owner, Id, _ents);
            Sawmill.Debug($"[InvalidateBalanceCache] Cache removed for {owner} ({Id})");
        }
    }

    private void RaiseInvalidateBalanceEvent(EntityUid owner)
    {
        Sawmill.Debug($"RaiseInvalidateBalanceEvent for {owner}");
        var ev = new CurrencyCacheInvalidateEvent(owner, Id);
        RaiseLocalEvent(owner, ev);
    }

    private bool TryInsertBest(EntityUid owner, EntityUid item)
    {
        // Оставь свою или пустую, если неважно
        return true;
    }

    #region Helpers

    public sealed class CurrencyCacheInvalidateEvent : EntityEventArgs
    {
        public readonly string? CurrencyId;
        public readonly EntityUid Owner;

        public CurrencyCacheInvalidateEvent(EntityUid owner, string? currencyId = null)
        {
            Owner = owner;
            CurrencyId = currencyId;
        }
    }
    #endregion
}
