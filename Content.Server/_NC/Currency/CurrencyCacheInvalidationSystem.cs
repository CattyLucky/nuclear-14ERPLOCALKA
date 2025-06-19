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
    public override void Initialize()
    {
        // CurrencyItem
        SubscribeLocalEvent<CurrencyItemComponent, ComponentShutdown>(OnCurrencyItemShutdown);
        SubscribeLocalEvent<CurrencyItemComponent, ComponentRemove   >(OnCurrencyItemRemove);

        // Stacks
        SubscribeLocalEvent<StackComponent, ComponentShutdown     >(OnStackShutdown);
        SubscribeLocalEvent<StackComponent, ComponentRemove       >(OnStackRemove);
        SubscribeLocalEvent<StackComponent, StackCountChangedEvent>(OnStackCountChanged);

        // Containers
        SubscribeLocalEvent<EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<EntRemovedFromContainerMessage >(OnEntRemoved);

        // Inventory / Hands
        SubscribeLocalEvent<GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<GotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<GotEquippedHandEvent>(OnGotEquippedHand);
        SubscribeLocalEvent<GotUnequippedHandEvent>(OnGotUnequippedHand);

        // Manual
        SubscribeLocalEvent<CurrencyCacheInvalidateEvent>(OnManualInvalidate);
    }

    #region Container events

    private void OnEntInserted(EntInsertedIntoContainerMessage ev)
    {
        if (!IsCurrency(ev.Entity, out var handler) || handler == null)
            return;

        if (FindOwner(ev.Entity) is { } owner)
            handler.InvalidateBalanceCache(owner);
    }

    private void OnEntRemoved(EntRemovedFromContainerMessage ev)
    {
        if (!IsCurrency(ev.Entity, out var handler) || handler == null)
            return;

        if (ev.Container.Owner != EntityUid.Invalid)
            handler.InvalidateBalanceCache(ev.Container.Owner);

        if (FindOwner(ev.Entity) is { } owner)
            handler.InvalidateBalanceCache(owner);
    }

    #endregion

    #region Inventory events

    private void OnGotEquipped(GotEquippedEvent ev)
    {
        if (IsCurrency(ev.Equipment, out var handler) && handler != null)
            handler.InvalidateBalanceCache(ev.Equipee);
    }

    private void OnGotUnequipped(GotUnequippedEvent ev)
    {
        if (IsCurrency(ev.Equipment, out var handler) && handler != null)
            handler.InvalidateBalanceCache(ev.Equipee);
    }

    #endregion

    #region Hand events

    private void OnGotEquippedHand(GotEquippedHandEvent ev)
    {
        if (IsCurrency(ev.Equipped, out var handler) && handler != null)
            handler.InvalidateBalanceCache(ev.User);
    }


    private void OnGotUnequippedHand(GotUnequippedHandEvent ev)
    {
        if (IsCurrency(ev.Unequipped, out var handler) && handler != null)
            handler.InvalidateBalanceCache(ev.User);
    }

    #endregion

    #region Manual invalidate

    private void OnManualInvalidate(CurrencyCacheInvalidateEvent ev)
    {
        if (ev.CurrencyId is { } id)
        {
            if (CurrencyRegistry.TryGet(id, out var handler) && handler != null)
                handler.InvalidateBalanceCache(ev.Owner);
            return;
        }

        foreach (var handler in CurrencyRegistry.GetAllHandlers())
            handler.InvalidateBalanceCache(ev.Owner);
    }

    public static void Invalidate(EntityUid owner, string? currencyId = null)
    {
        var ev = new CurrencyCacheInvalidateEvent(owner, currencyId);
        var em = IoCManager.Resolve<IEntityManager>();
        em.System<CurrencyCacheInvalidationSystem>().RaiseLocalEvent(owner, ev);
    }

    #endregion

    #region Component change handlers

    private void OnCurrencyItemShutdown(EntityUid uid, CurrencyItemComponent comp, ref ComponentShutdown _) =>
        HandleCurrencyItemChanged(uid, comp);

    private void OnCurrencyItemRemove(EntityUid uid, CurrencyItemComponent comp, ref ComponentRemove _) =>
        HandleCurrencyItemChanged(uid, comp);

    private void HandleCurrencyItemChanged(EntityUid uid, CurrencyItemComponent comp)
    {
        if (FindOwner(uid) is not { } owner)
            return;

        var handler = CurrencyRegistry.TryGet(comp.Currency);
        if (handler == null)
            return;

        handler.InvalidateBalanceCache(owner);
    }

    private void OnStackShutdown(EntityUid uid, StackComponent comp, ref ComponentShutdown _) =>
        HandleStackChanged(uid, comp);

    private void OnStackRemove(EntityUid uid, StackComponent comp, ref ComponentRemove _) =>
        HandleStackChanged(uid, comp);

    private void OnStackCountChanged(EntityUid uid, StackComponent comp, ref StackCountChangedEvent _) =>
        HandleStackChanged(uid, comp);

    private void HandleStackChanged(EntityUid uid, StackComponent comp)
    {
        foreach (var handler in CurrencyRegistry.GetAllHandlers())
            if (handler.StackTypeId == comp.StackTypeId && FindOwner(uid) is { } owner)
                handler.InvalidateBalanceCache(owner);
    }

    #endregion

    #region Helpers

    // Новый метод: определяет, относится ли Stack к валюте, и возвращает нужный хендлер
    private bool IsCurrency(EntityUid uid, out ICurrencyHandler? handler)
    {
        handler = null;
        if (EntityManager.TryGetComponent(uid, out CurrencyItemComponent? coin))
        {
            if (CurrencyRegistry.TryGet(coin.Currency, out handler))
                return true;
        }
        if (EntityManager.TryGetComponent(uid, out StackComponent? stack))
        {
            foreach (var h in CurrencyRegistry.GetAllHandlers())
                if (h.StackTypeId == stack.StackTypeId)
                {
                    handler = h;
                    return true;
                }
        }
        return false;
    }

    private bool IsCurrency(EntityUid uid) => IsCurrency(uid, out _);

    private EntityUid? FindOwner(EntityUid entity)
    {
        if (EntityManager.TryGetComponent(entity, out TransformComponent? xform) &&
            xform.ParentUid != EntityUid.Invalid)
            return xform.ParentUid;

        if (EntityManager.TryGetComponent(entity, out ContainerManagerComponent? cmc))
        {
            foreach (var cont in cmc.Containers.Values)
                if (cont.Owner != EntityUid.Invalid)
                    return cont.Owner;
        }
        return null;
    }

    #endregion
}

public sealed class CurrencyCacheInvalidateEvent(EntityUid owner, string? currencyId = null) : EntityEventArgs
{
    public readonly string?  CurrencyId = currencyId;
    public readonly EntityUid Owner = owner;
}
