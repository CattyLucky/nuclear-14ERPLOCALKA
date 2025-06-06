using System.Linq;
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
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

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

        // Слежение за CapCoin
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
                if (!_prototypes.TryIndex<EntityPrototype>(id, out var proto))
                    continue;

                var key = category == TraderCategory.Sell ? $"sell/{id}" : id;

                string? iconPath = null;

                if (proto.Components.TryGetValue("Sprite", out var spriteComp) &&
                    spriteComp is IDictionary<string, object> dict)
                {
                    if (dict.TryGetValue("sprite", out var spriteVal) &&
                        spriteVal is string spriteStr)
                    {
                        if (dict.TryGetValue("state", out var stateVal) &&
                            stateVal is string stateStr)
                            iconPath = $"{spriteStr}/{stateStr}.png";
                        else
                            iconPath = $"{spriteStr}/icon.png";
                    }
                }

                comp.Listings[key] = new TraderListingData
                {
                    Id = key,
                    Name = proto.Name,
                    Price = price,
                    Category = category,
                    Icon = iconPath,
                    SpawnResultId = id
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
    }

    private void OnBuyRequest(EntityUid uid, TraderMachineComponent comp, BuyItemMessage msg)
    {
        if (msg.ProductId == "refresh")
        {
            SendUpdate(uid, comp, msg.Sender);
            return;
        }

        if (!_entMan.EntityExists(msg.Sender) || !TryComp<ActorComponent>(msg.Sender, out var actor))
            return;

        var buyer = actor.Owner;

        if (!comp.Listings.TryGetValue(msg.ProductId, out var listing))
            return;

        if (listing.Category == TraderCategory.Sell)
            TrySellItem(buyer, comp, listing, uid);
        else
            TryBuyItem(buyer, comp, listing, uid);
    }

    private void TryBuyItem(EntityUid buyer, TraderMachineComponent comp, TraderListingData listing, EntityUid machine)
    {
        if (!TryGetCurrency(buyer, comp.CurrencyAccepted, listing.Price, out var toConsume))
        {
            _popup.PopupEntity("Недостаточно средств.", machine, buyer);
            return;
        }

        foreach (var ent in toConsume)
            _entMan.DeleteEntity(ent);

        if (listing.SpawnResultId is not { } resultId)
        {
            _popup.PopupEntity("Ошибка: невозможно купить этот товар.", machine, buyer);
            return;
        }

        var spawned = Spawn(resultId, Transform(buyer).Coordinates);
        _hands.PickupOrDrop(buyer, spawned);
        _popup.PopupEntity($"Куплено {listing.Name} за {listing.Price}.", machine, buyer);

        SendUpdate(machine, comp, buyer);
    }

    private void TrySellItem(EntityUid seller, TraderMachineComponent comp, TraderListingData listing, EntityUid machine)
    {
        var realId = listing.Id.Replace("sell/", "");

        if (!_entMan.TryGetComponent(seller, out HandsComponent? hands) ||
            !_entMan.TryGetComponent(seller, out InventoryComponent? _))
        {
            _popup.PopupEntity("Нет подходящего предмета.", machine, seller);
            return;
        }

        var heldItems = _hands.EnumerateHeld(seller, hands)
            .Concat(GetHeldInventoryItems(seller))
            .ToList();

        var found = heldItems
            .FirstOrDefault(e => _entMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID == realId);

        if (found == default)
        {
            _popup.PopupEntity("Нет подходящего предмета.", machine, seller);
            return;
        }

        _entMan.DeleteEntity(found);

        for (int i = 0; i < listing.Price; i++)
        {
            var coin = Spawn("CapCoin", Transform(seller).Coordinates);
            _hands.PickupOrDrop(seller, coin);
        }

        _popup.PopupEntity($"Продано {listing.Name} за {listing.Price} CapCoin.", machine, seller);

        comp.Listings.Remove(listing.Id);
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

        _ui.SetUiState(machine, TraderUiKey.Key, new TraderUpdateState(comp.Listings, balance));
    }

    private void OnCurrencyChanged(EntityUid uid, CurrencyItemComponent comp, EntityEventArgs args)
    {
        if (!_entMan.TryGetComponent<TransformComponent>(uid, out var xform))
            return;

        var nearbySessions = Filter.Pvs(xform.Coordinates, 5f, _entMan);

        foreach (var session in nearbySessions.Recipients)
        {
            var playerEnt = session.AttachedEntity;
            if (playerEnt == null)
                continue;

            foreach (var machine in EntityQuery<TraderMachineComponent>())
            {
                if (playerEnt == null || !_ui.IsUiOpen(machine.Owner, TraderUiKey.Key, playerEnt.Value))
                    continue;

                SendUpdate(machine.Owner, machine, playerEnt.Value);
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

        if (_entMan.TryGetComponent(player, out InventoryComponent? _))
        {
            foreach (var item in GetHeldInventoryItems(player))
                if (IsCurrency(item, currencyType, out var val))
                {
                    found.Add(item);
                    total += val;
                    if (total >= amount)
                        return true;
                }
        }

        return false;
    }

    private IEnumerable<EntityUid> GetHeldInventoryItems(EntityUid uid)
    {
        var slots = new[] { "pocket1", "pocket2", "belt", "back", "gloves", "shoes", "id", };
        foreach (var slot in slots)
            if (_inventory.TryGetSlotEntity(uid, slot, out var item))
                yield return item.Value;
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
