using Content.Shared._NC.Trader;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Containers;
using System.Linq;

namespace Content.Server._NC.Trader;

public sealed class TraderMachineSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TraderMachineComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TraderMachineComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<TraderMachineComponent, BuyItemMessage>(OnBuyRequest);

        SubscribeLocalEvent<CurrencyItemComponent, EntInsertedIntoContainerMessage>(OnCurrencyChanged);
        SubscribeLocalEvent<CurrencyItemComponent, EntRemovedFromContainerMessage>(OnCurrencyChanged);
        SubscribeLocalEvent<CurrencyItemComponent, ComponentShutdown>(OnCurrencyChanged);
    }

    private void OnMapInit(EntityUid uid, TraderMachineComponent comp, MapInitEvent args)
    {
        comp.Listings.Clear();

        foreach (var (category, entries) in comp.InventoryRaw)
        {
            foreach (var (id, price) in entries)
            {
                if (id == "refresh")
                    continue;

                if (!_prototypes.TryIndex<EntityPrototype>(id, out var proto))
                    continue;

                var key = category == TraderCategory.Sell ? $"sell/{id}" : id;

                string? iconPath = null;
                var description = proto.Description ?? string.Empty;

                if (proto.Components.TryGetValue("Sprite", out var spriteComp) &&
                    spriteComp is IDictionary<string, object> dict)
                {
                    if (dict.TryGetValue("sprite", out var spriteVal) && spriteVal is string spriteStr)
                    {
                        if (dict.TryGetValue("state", out var stateVal) && stateVal is string stateStr)
                            iconPath = $"{spriteStr}/{stateStr}.png";
                        else
                            iconPath = $"{spriteStr}/icon.png";
                    }
                }

                comp.Listings[key] = new TraderListingData
                {
                    Id = key,
                    ProtoId = id,
                    Name = proto.Name,
                    Price = price,
                    Category = category,
                    Icon = iconPath,
                    Description = description
                };
            }
        }
    }

    private void OnActivate(EntityUid uid, TraderMachineComponent comp, ActivateInWorldEvent args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        var session = actor.PlayerSession;
        _ui.OpenUi(uid, TraderUiKey.Key, session);
        SendUpdate(uid, comp, args.User);

        comp.LastUser = args.User;
    }

    private void OnBuyRequest(EntityUid uid, TraderMachineComponent comp, BuyItemMessage msg)
    {
        if (comp.LastUser is not { } buyer || !_entMan.EntityExists(buyer))
            return;

        if (!_ui.IsUiOpen(uid, TraderUiKey.Key, buyer))
            return;

        if (msg.ProductId == "refresh")
        {
            SendUpdate(uid, comp, buyer);
            return;
        }

        if (!comp.Listings.TryGetValue(msg.ProductId, out var listing))
            return;

        var clampedAmount = Math.Clamp(msg.Amount, 1, 1000);

        if (listing.Category == TraderCategory.Sell)
            TrySellItem(buyer, comp, listing, uid, clampedAmount);
        else
            TryBuyItem(buyer, comp, listing, uid, clampedAmount);
    }

    private void TryBuyItem(EntityUid buyer, TraderMachineComponent comp, TraderListingData listing, EntityUid machine, int amount)
    {
        var totalCost = listing.Price * amount;
        if (!TryGetCurrency(buyer, comp.CurrencyAccepted, totalCost, out var toConsume))
        {
            _popup.PopupEntity(Loc.GetString("trader-error-no-currency"), machine, buyer);
            return;
        }

        foreach (var ent in toConsume)
            _entMan.DeleteEntity(ent);

        if (listing.ProtoId is not { } resultId)
        {
            _popup.PopupEntity(Loc.GetString("trader-buy-error"), machine, buyer);
            return;
        }

        for (int i = 0; i < amount; i++)
        {
            var spawned = Spawn(resultId, Transform(buyer).Coordinates);
            _hands.PickupOrDrop(buyer, spawned);
        }

        _popup.PopupEntity(Loc.GetString("trader-bought",
            ("name", listing.Name),
            ("amount", amount),
            ("cost", totalCost),
            ("currency", comp.CurrencyAccepted)), machine, buyer);

        SendUpdate(machine, comp, buyer);
    }

    private void TrySellItem(EntityUid seller, TraderMachineComponent comp, TraderListingData listing, EntityUid machine, int amount)
    {
        var realId = listing.ProtoId;
        var heldItems = new List<EntityUid>();

        if (_entMan.TryGetComponent(seller, out HandsComponent? hands))
        {
            foreach (var ent in _hands.EnumerateHeld(seller, hands))
            {
                if (_entMan.TryGetComponent(ent, out MetaDataComponent? meta) &&
                    meta.EntityPrototype?.ID == realId)
                {
                    heldItems.Add(ent);
                    if (heldItems.Count >= amount)
                        break;
                }
            }
        }

        if (_entMan.TryGetComponent(seller, out InventoryComponent? inv))
        {
            foreach (var slot in _inventory.EnumerateAllSlots(seller, inv))
            {
                if (_inventory.TryGetSlotEntity(seller, slot.Id, out var ent) &&
                    _entMan.TryGetComponent(ent.Value, out MetaDataComponent? meta) &&
                    meta.EntityPrototype?.ID == realId)
                {
                    heldItems.Add(ent.Value);
                    if (heldItems.Count >= amount)
                        break;
                }
            }
        }

        if (heldItems.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("trader-error-no-items"), machine, seller);
            return;
        }

        var totalCoins = amount * listing.Price;
        var maxCoinsAllowed = 200;
        if (totalCoins > maxCoinsAllowed)
        {
            amount = maxCoinsAllowed / listing.Price;
            totalCoins = amount * listing.Price;
        }

        foreach (var item in heldItems.Take(amount))
            _entMan.DeleteEntity(item);

        for (int i = 0; i < totalCoins; i++)
        {
            var coin = Spawn(comp.CurrencyAccepted, Transform(seller).Coordinates);
            _hands.PickupOrDrop(seller, coin);
        }

        _popup.PopupEntity(Loc.GetString("trader-sold",
            ("name", listing.Name),
            ("amount", amount),
            ("cost", totalCoins),
            ("currency", comp.CurrencyAccepted)), machine, seller);

        SendUpdate(machine, comp, seller);
    }

    private void SendUpdate(EntityUid machine, TraderMachineComponent comp, EntityUid player)
    {
        var balance = 0;
        if (TryGetCurrency(player, comp.CurrencyAccepted, int.MaxValue, out var coins))
        {
            foreach (var coin in coins)
                if (IsCurrency(coin, comp.CurrencyAccepted, out var val))
                    balance += val;
        }

        _ui.SetUiState(machine, TraderUiKey.Key,
            new TraderUpdateState(comp.Listings, balance, comp.CurrencyAccepted));
    }

    private void OnCurrencyChanged(EntityUid uid, CurrencyItemComponent comp, EntityEventArgs args)
    {
        if (!_entMan.TryGetComponent<TransformComponent>(uid, out var xform))
            return;

        var nearbySessions = Filter.Pvs(xform.Coordinates, 5f, _entMan);

        foreach (var session in nearbySessions.Recipients)
        {
            var attached = session.AttachedEntity;
            if (attached == null || !_entMan.EntityExists(attached.Value))
                continue;

            var playerUid = attached.Value;
            var playerCoords = Transform(playerUid).Coordinates;

            foreach (var ent in EntityQuery<TraderMachineComponent>())
            {
                var machineUid = ent.Owner;
                var machineComp = ent;

                if (!Transform(machineUid).Coordinates.InRange(_entMan, playerCoords, 3f))
                    continue;

                if (!_ui.IsUiOpen(machineUid, TraderUiKey.Key, playerUid))
                    continue;

                SendUpdate(machineUid, machineComp, playerUid);
            }
        }
    }

    private bool TryGetCurrency(EntityUid player, string currencyType, int amount, out List<EntityUid> found)
    {
        found = new();
        var total = 0;

        if (_entMan.TryGetComponent(player, out HandsComponent? hands))
        {
            foreach (var held in _hands.EnumerateHeld(player, hands))
                if (IsCurrency(held, currencyType, out var val))
                {
                    found.Add(held);
                    total += val;
                    if (total >= amount)
                        return true;
                }
        }

        if (_entMan.TryGetComponent(player, out InventoryComponent? inv))
        {
            foreach (var slot in _inventory.EnumerateAllSlots(player, inv))
            {
                if (_inventory.TryGetSlotEntity(player, slot.Id, out var ent) &&
                    IsCurrency(ent.Value, currencyType, out var val))
                {
                    found.Add(ent.Value);
                    total += val;
                    if (total >= amount)
                        return true;
                }
            }
        }

        found.Clear();
        return false;
    }

    private bool IsCurrency(EntityUid uid, string currencyType, out int value)
    {
        value = 0;
        if (!_entMan.TryGetComponent(uid, out CurrencyItemComponent? currency))
            return false;

        if (currency.CurrencyType != currencyType)
            return false;

        value = currency.Value;
        return true;
    }
}

