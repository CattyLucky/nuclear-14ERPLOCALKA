using Content.Shared._NC.Trade;
using Robust.Server.GameObjects;

namespace Content.Server._NC.Trade;

/// <summary>
/// Серверная система взаимодействия с магазином через Bound UI.
/// </summary>
public sealed class NcStoreSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IEntitySystemManager _sysMan = default!;

    private static readonly ISawmill Sawmill = Logger.GetSawmill("ncstore");

    public override void Initialize()
    {
        SubscribeLocalEvent<NcStoreComponent, StoreBuyListingBoundUiMessage>(OnBuyRequest);
        SubscribeLocalEvent<NcStoreComponent, StoreExchangeListingBoundUiMessage>(OnExchangeRequest);
    }

    private void OnBuyRequest(EntityUid uid, NcStoreComponent comp, StoreBuyListingBoundUiMessage msg)
    {
        var actor = msg.Actor;
        if (!ValidateUiAccess(uid, actor))
            return;

        var logic = _sysMan.GetEntitySystem<NcStoreLogicSystem>();
        var result = logic.TryPurchase(msg.ListingId, uid, comp, actor);

        if (result)
        {
            Sawmill.Info($"[Buy] {ToPrettyString(actor)} купил '{msg.ListingId}' у {ToPrettyString(uid)}.");
            _sysMan.GetEntitySystem<StoreStructuredSystem>().UpdateUiState(uid, comp, actor);
        }
        else
            Sawmill.Warning($"[Buy] Покупка не удалась: listingId={msg.ListingId}, user={ToPrettyString(actor)}.");
    }

    private void OnExchangeRequest(EntityUid uid, NcStoreComponent comp, StoreExchangeListingBoundUiMessage msg)
    {
        var actor = msg.Actor;
        if (!ValidateUiAccess(uid, actor))
            return;

        var logic = _sysMan.GetEntitySystem<NcStoreLogicSystem>();
        var result = logic.TryExchange(msg.ListingId, uid, comp, actor, msg);

        if (result)
        {
            Sawmill.Info($"[Exchange] {ToPrettyString(actor)} обменял '{msg.ListingId}' у {ToPrettyString(uid)}.");
            _sysMan.GetEntitySystem<StoreStructuredSystem>().UpdateUiState(uid, comp, actor);
        }
        else
            Sawmill.Warning($"[Exchange] Обмен не удался: listingId={msg.ListingId}, user={ToPrettyString(actor)}.");
    }

    private bool ValidateUiAccess(EntityUid storeUid, EntityUid user)
    {
        if (!_entMan.EntityExists(user))
            return false;
        if (!_entMan.TryGetComponent(storeUid, out TransformComponent? storeXform) ||
            !_entMan.TryGetComponent(user, out TransformComponent? userXform))
            return false;

        if (!_transform.InRange(storeXform.Coordinates, userXform.Coordinates, 3f))
        {
            Sawmill.Warning($"[UI] User too far from store: {ToPrettyString(user)} -> {ToPrettyString(storeUid)}.");
            return false;
        }
        // Здесь можно добавить мьютекс для "занятости" автомата
        return true;
    }
}
