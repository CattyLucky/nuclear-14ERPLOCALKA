using Content.Shared._NC.Currency;
using Content.Shared.Hands;
using Content.Shared.Inventory.Events;
using Content.Shared.Stacks;
using Robust.Shared.Containers;

namespace Content.Server._NC.Currency;

/// <summary>
/// Слушает все изменения валютных сущностей и инвалидирует кэш баланса владельца.
/// </summary>
public sealed class CurrencyCacheInvalidationSystem : EntitySystem
{
    private static readonly ISawmill Sawmill = Logger.GetSawmill("currency-invalidation");

    public override void Initialize()
    {
        Sawmill.Info("CurrencyCacheInvalidationSystem initializing.");
        SubscribeLocalEvent<CurrencyItemComponent, ComponentShutdown>(OnCurrencyItemShutdown);
        SubscribeLocalEvent<CurrencyItemComponent, ComponentRemove   >(OnCurrencyItemRemove);

        SubscribeLocalEvent<StackComponent, ComponentShutdown     >(OnStackShutdown);
        SubscribeLocalEvent<StackComponent, ComponentRemove       >(OnStackRemove);
        SubscribeLocalEvent<StackComponent, StackCountChangedEvent>(OnStackCountChanged);

        SubscribeLocalEvent<EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<EntRemovedFromContainerMessage >(OnEntRemoved);

        SubscribeLocalEvent<GotEquippedEvent      >(OnGotEquipped);
        SubscribeLocalEvent<GotUnequippedEvent    >(OnGotUnequipped);
        SubscribeLocalEvent<GotEquippedHandEvent  >(OnGotEquippedHand);
        SubscribeLocalEvent<GotUnequippedHandEvent>(OnGotUnequippedHand);

        SubscribeLocalEvent<CurrencyCacheInvalidateEvent>(OnManualInvalidate);
    }

    #region Container events

    private void OnEntInserted(EntInsertedIntoContainerMessage ev)
    {
        Sawmill.Debug($"OnEntInserted: {ev.Entity} into container {ev.Container.Owner}");
        if (!IsCurrency(ev.Entity, out var handler) || handler == null)
            return;

        if (FindOwner(ev.Entity) is { } owner)
        {
            Sawmill.Debug($"OnEntInserted: invalidating cache for owner {owner} by handler {handler.Id}");
            handler.InvalidateBalanceCache(owner);
        }
    }

    private void OnEntRemoved(EntRemovedFromContainerMessage ev)
    {
        Sawmill.Debug($"OnEntRemoved: {ev.Entity} from container {ev.Container.Owner}");
        if (!IsCurrency(ev.Entity, out var handler) || handler == null)
            return;

        if (ev.Container.Owner != EntityUid.Invalid)
        {
            Sawmill.Debug($"OnEntRemoved: invalidating cache for container owner {ev.Container.Owner} by handler {handler.Id}");
            handler.InvalidateBalanceCache(ev.Container.Owner);
        }

        if (FindOwner(ev.Entity) is { } owner)
        {
            Sawmill.Debug($"OnEntRemoved: invalidating cache for entity owner {owner} by handler {handler.Id}");
            handler.InvalidateBalanceCache(owner);
        }
    }

    #endregion

    #region Inventory events

    private void OnGotEquipped(GotEquippedEvent ev)
    {
        Sawmill.Debug($"OnGotEquipped: {ev.Equipee} equipped {ev.Equipment}");
        if (IsCurrency(ev.Equipment, out var handler) && handler != null)
        {
            Sawmill.Debug($"OnGotEquipped: invalidating cache for {ev.Equipee} by handler {handler.Id}");
            handler.InvalidateBalanceCache(ev.Equipee);
        }
    }

    private void OnGotUnequipped(GotUnequippedEvent ev)
    {
        Sawmill.Debug($"OnGotUnequipped: {ev.Equipee} unequipped {ev.Equipment}");
        if (IsCurrency(ev.Equipment, out var handler) && handler != null)
        {
            Sawmill.Debug($"OnGotUnequipped: invalidating cache for {ev.Equipee} by handler {handler.Id}");
            handler.InvalidateBalanceCache(ev.Equipee);
        }
    }

    #endregion

    #region Hand events

    private void OnGotEquippedHand(GotEquippedHandEvent ev)
    {
        Sawmill.Debug($"OnGotEquippedHand: {ev.User} equipped {ev.Equipped} in hand");
        if (IsCurrency(ev.Equipped, out var handler) && handler != null)
        {
            Sawmill.Debug($"OnGotEquippedHand: invalidating cache for {ev.User} by handler {handler.Id}");
            handler.InvalidateBalanceCache(ev.User);
        }
    }

    private void OnGotUnequippedHand(GotUnequippedHandEvent ev)
    {
        Sawmill.Debug($"OnGotUnequippedHand: {ev.User} unequipped {ev.Unequipped} from hand");
        if (IsCurrency(ev.Unequipped, out var handler) && handler != null)
        {
            Sawmill.Debug($"OnGotUnequippedHand: invalidating cache for {ev.User} by handler {handler.Id}");
            handler.InvalidateBalanceCache(ev.User);
        }
    }

    #endregion

    #region Manual invalidate

    private void OnManualInvalidate(CurrencyCacheInvalidateEvent ev)
    {
        Sawmill.Debug($"OnManualInvalidate: {ev.Owner}, currencyId={ev.CurrencyId}");
        if (ev.CurrencyId is { } id)
        {
            if (CurrencyRegistry.TryGet(id, out var handler) && handler != null)
            {
                Sawmill.Debug($"OnManualInvalidate: invalidating cache for {ev.Owner} by handler {id}");
                handler.InvalidateBalanceCache(ev.Owner);
            }
            else
            {
                Sawmill.Warning($"OnManualInvalidate: handler {id} not found or is null for {ev.Owner}");
            }

            return;
        }

        foreach (var handler in CurrencyRegistry.GetAllHandlers())
        {
            Sawmill.Debug($"OnManualInvalidate: invalidating cache for {ev.Owner} by handler {handler.Id}");
            handler.InvalidateBalanceCache(ev.Owner);
        }
    }

    public static void Invalidate(EntityUid owner, string? currencyId = null)
    {
        Sawmill.Debug($"Static Invalidate: {owner}, currencyId={currencyId}");
        var ev = new CurrencyCacheInvalidateEvent(owner, currencyId);
        var em = IoCManager.Resolve<IEntityManager>();
        em.System<CurrencyCacheInvalidationSystem>().RaiseLocalEvent(owner, ev);
    }

    #endregion

    #region Component change handlers

    private void OnCurrencyItemShutdown(EntityUid uid, CurrencyItemComponent comp, ref ComponentShutdown _)
    {
        Sawmill.Debug($"OnCurrencyItemShutdown: {uid}");
        HandleCurrencyItemChanged(uid, comp);
    }

    private void OnCurrencyItemRemove(EntityUid uid, CurrencyItemComponent comp, ref ComponentRemove _)
    {
        Sawmill.Debug($"OnCurrencyItemRemove: {uid}");
        HandleCurrencyItemChanged(uid, comp);
    }

    private void HandleCurrencyItemChanged(EntityUid uid, CurrencyItemComponent comp)
    {
        Sawmill.Debug($"HandleCurrencyItemChanged: {uid}, currency={comp.Currency}");
        if (FindOwner(uid) is not { } owner)
        {
            Sawmill.Warning($"HandleCurrencyItemChanged: owner not found for {uid}");
            return;
        }

        var handler = CurrencyRegistry.TryGet(comp.Currency, out var h) ? h : null;
        if (handler == null)
        {
            Sawmill.Warning($"HandleCurrencyItemChanged: handler for {comp.Currency} not found");
            return;
        }

        Sawmill.Debug($"HandleCurrencyItemChanged: invalidating cache for {owner} by handler {handler.Id}");
        handler.InvalidateBalanceCache(owner);
    }

    private void OnStackShutdown(EntityUid uid, StackComponent comp, ref ComponentShutdown _)
    {
        Sawmill.Debug($"OnStackShutdown: {uid}");
        HandleStackChanged(uid, comp);
    }

    private void OnStackRemove(EntityUid uid, StackComponent comp, ref ComponentRemove _)
    {
        Sawmill.Debug($"OnStackRemove: {uid}");
        HandleStackChanged(uid, comp);
    }

    private void OnStackCountChanged(EntityUid uid, StackComponent comp, ref StackCountChangedEvent _)
    {
        Sawmill.Debug($"OnStackCountChanged: {uid}");
        HandleStackChanged(uid, comp);
    }

    private void HandleStackChanged(EntityUid uid, StackComponent comp)
    {
        foreach (var handler in CurrencyRegistry.GetAllHandlers())
            if (handler.StackTypeId == comp.StackTypeId && FindOwner(uid) is { } owner)
            {
                Sawmill.Debug($"HandleStackChanged: invalidating cache for {owner} by handler {handler.Id}");
                handler.InvalidateBalanceCache(owner);
            }
    }

    #endregion

    #region Helpers

    private bool IsCurrency(EntityUid uid, out ICurrencyHandler? handler)
    {
        handler = null;
        if (EntityManager.TryGetComponent(uid, out CurrencyItemComponent? coin))
        {
            if (CurrencyRegistry.TryGet(coin.Currency, out handler))
            {
                Sawmill.Debug($"IsCurrency: {uid} is coin of {coin.Currency}");
                return true;
            }
        }
        if (EntityManager.TryGetComponent(uid, out StackComponent? stack))
        {
            foreach (var h in CurrencyRegistry.GetAllHandlers())
                if (h.StackTypeId == stack.StackTypeId)
                {
                    handler = h;
                    Sawmill.Debug($"IsCurrency: {uid} is stack of {stack.StackTypeId}");
                    return true;
                }
        }
        Sawmill.Debug($"IsCurrency: {uid} is not a currency item");
        return false;
    }

    private bool IsCurrency(EntityUid uid) => IsCurrency(uid, out _);

    private EntityUid? FindOwner(EntityUid entity)
    {
        if (EntityManager.TryGetComponent(entity, out TransformComponent? xform) &&
            xform.ParentUid != EntityUid.Invalid)
        {
            Sawmill.Debug($"FindOwner: {entity} parent {xform.ParentUid}");
            return xform.ParentUid;
        }

        if (EntityManager.TryGetComponent(entity, out ContainerManagerComponent? cmc))
        {
            foreach (var cont in cmc.Containers.Values)
                if (cont.Owner != EntityUid.Invalid)
                {
                    Sawmill.Debug($"FindOwner: {entity} container owner {cont.Owner}");
                    return cont.Owner;
                }
        }
        Sawmill.Debug($"FindOwner: {entity} owner not found");
        return null;
    }
    public sealed class CurrencyCacheInvalidateEvent : EntityEventArgs
    {
        public readonly string?  CurrencyId;
        public readonly EntityUid Owner;

        public CurrencyCacheInvalidateEvent(EntityUid owner, string? currencyId = null)
        {
            Owner = owner;
            CurrencyId = currencyId;
        }
    }

    #endregion
}
