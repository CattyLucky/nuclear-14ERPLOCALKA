using System.Linq;
using Content.Shared._NC.Trade;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;


namespace Content.Server._NC.Trade;


public sealed class NcStoreSystem : EntitySystem
{
    private static readonly ISawmill Sawmill = Logger.GetSawmill("ncstore");

    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IEntitySystemManager _sysMan = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<NcStoreComponent, StoreBuyListingBoundUiMessage>(OnBuyRequest);
        SubscribeLocalEvent<NcStoreComponent, StoreSellListingBoundUiMessage>(OnSellRequest);
        SubscribeLocalEvent<NcStoreComponent, StoreExchangeListingBoundUiMessage>(OnExchangeRequest);
    }

    private void OnBuyRequest(EntityUid uid, NcStoreComponent comp, StoreBuyListingBoundUiMessage msg)
    {
        var actor = msg.Actor;
        if (!ValidateUiAccess(uid, actor))
            return;

        var logic = _sysMan.GetEntitySystem<NcStoreLogicSystem>();
        var listing = comp.Listings.FirstOrDefault(x => x.Id == msg.ListingId);
        if (listing == null)
        {
            Sawmill.Warning($"[Buy] Listing not found: {msg.ListingId}");
            return;
        }

        var result = false;
        if (listing.Mode == StoreMode.Buy)
            result = logic.TryBuy(listing.Id, uid, comp, actor);
        else if (listing.Mode == StoreMode.Sell)
            result = logic.TrySell(listing.Id, uid, comp, actor);
        else
            Sawmill.Warning($"[Buy] Unsupported listing mode: {listing.Mode}");

        if (result)
        {
            Sawmill.Info($"[Buy] {ToPrettyString(actor)} купил '{msg.ListingId}' у {ToPrettyString(uid)}.");

            _audio.PlayPvs(
                "/Audio/Effects/Cargo/ping.ogg",
                uid,
                AudioParams.Default.WithVolume(-2f));

            _sysMan.GetEntitySystem<StoreStructuredSystem>().UpdateUiState(uid, comp, actor);
        }
        else
            Sawmill.Warning($"[Buy] Покупка не удалась: listingId={msg.ListingId}, user={ToPrettyString(actor)}.");
    }

    private void OnSellRequest(EntityUid uid, NcStoreComponent comp, StoreSellListingBoundUiMessage msg)
    {
        var actor = msg.Actor;
        if (!ValidateUiAccess(uid, actor))
            return;

        var logic = _sysMan.GetEntitySystem<NcStoreLogicSystem>();
        var listing = comp.Listings.FirstOrDefault(x => x.Id == msg.ListingId && x.Mode == StoreMode.Sell);
        if (listing == null)
        {
            Sawmill.Warning($"[Sell] Listing not found: {msg.ListingId}");
            return;
        }

        var result = logic.TrySell(listing.Id, uid, comp, actor);

        if (result)
        {
            Sawmill.Info($"[Sell] {ToPrettyString(actor)} продал '{msg.ListingId}' у {ToPrettyString(uid)}.");

            _audio.PlayPvs(
                "/Audio/Effects/Cargo/ping.ogg",
                uid,
                AudioParams.Default.WithVolume(-2f));

            _sysMan.GetEntitySystem<StoreStructuredSystem>().UpdateUiState(uid, comp, actor);
        }
        else
            Sawmill.Warning($"[Sell] Продажа не удалась: listingId={msg.ListingId}, user={ToPrettyString(actor)}.");
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
        if (_entMan.TryGetComponent(storeUid, out NcStoreComponent? storeComp))
        {
            if (_entMan.TryGetComponent(storeUid, out AccessReaderComponent? reader))
            {
                if (!_access.IsAllowed(user, storeUid, reader))
                {
                    Sawmill.Warning($"[UI] Нет доступа: {ToPrettyString(user)} -> {ToPrettyString(storeUid)}.");
                    return false;
                }
            }
            else if (storeComp.Access is { Count: > 0, })
            {
                var fake = new AccessReaderComponent();
                fake.AccessLists.Clear();

                foreach (var group in storeComp.Access)
                {
                    var set = new HashSet<ProtoId<AccessLevelPrototype>>();

                    foreach (var token in group)
                    {
                        if (IoCManager.Resolve<IPrototypeManager>().TryIndex<AccessLevelPrototype>(token, out _))
                        {
                            set.Add(new(token));
                            continue;
                        }

                        if (IoCManager.Resolve<IPrototypeManager>().TryIndex<AccessGroupPrototype>(token, out var grp))
                        {
                            if (set.Count > 0)
                            {
                                fake.AccessLists.Add(set);
                                set = new();
                            }

                            foreach (var lvl in grp.Tags)
                                fake.AccessLists.Add(new() { lvl, });

                            continue;
                        }

                        Sawmill.Warning(
                            $"[Access] Unknown access token '{token}' on {ToPrettyString(storeUid)}; skipping.");
                    }

                    if (set.Count > 0)
                        fake.AccessLists.Add(set);
                }

                if (fake.AccessLists.Count == 0)
                {
                    Sawmill.Warning(
                        $"[Access] All access groups invalid/empty on {ToPrettyString(storeUid)}; denying.");
                    return false;
                }

                if (!_access.IsAllowed(user, storeUid, fake)) // <— порядок аргументов исправлен
                {
                    Sawmill.Warning(
                        $"[UI] Нет доступа (fallback): {ToPrettyString(user)} -> {ToPrettyString(storeUid)}.");
                    return false;
                }
            }

            if (storeComp.CurrentUser == null || storeComp.CurrentUser != user)
            {
                Sawmill.Warning(
                    $"[UI] Store busy: {ToPrettyString(storeUid)}. Current={ToPrettyString(storeComp.CurrentUser)}, Attempt={ToPrettyString(user)}");
                return false;
            }
        }

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

        return true;
    }
}
