using System.Linq;
using Content.Shared._NC.Currency;
using Content.Shared._NC.Trade;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Content.Server._NC.Currency;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Stacks;


namespace Content.Server._NC.Trade;

public sealed class NcStoreLogicSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _ents = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly CurrencyExchangeSystem _exchange = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedStackSystem _stacks= default!;
    private static readonly ISawmill Sawmill = Logger.GetSawmill("ncstore-logic");

    public int GetBalance(EntityUid user, string currencyId)
    {
        Sawmill.Debug($"GetBalance: user={user}, currencyId={currencyId}");
        if (!CurrencyRegistry.TryGet(currencyId, out var handler) || handler == null)
        {
            Sawmill.Warning($"GetBalance: handler not found for currencyId={currencyId}");
            return 0;
        }
        var bal = handler.GetBalance(user);
        Sawmill.Debug($"GetBalance: user={user}, currencyId={currencyId}, balance={bal}");
        return bal;
    }

    public bool TryPurchase(string listingId, EntityUid machine, NcStoreComponent? store, EntityUid user)
    {
        Sawmill.Info($"TryPurchase: listingId={listingId}, machine={machine}, user={user}");
        if (store == null || store.Listings.Count == 0)
        {
            Sawmill.Warning("TryPurchase: Store is null or has no listings");
            return false;
        }

        var listing = store.Listings.FirstOrDefault(x => x.Id == listingId);
        if (listing == null)
        {
            Sawmill.Warning($"TryPurchase: Listing {listingId} not found in store");
            return false;
        }
        if (listing.Cost.Count == 0 || string.IsNullOrEmpty(listing.ProductEntity))
        {
            Sawmill.Warning($"TryPurchase: Listing {listingId} is invalid: no cost or no product");
            return false;
        }

        var currencyId = store.CurrencyWhitelist.FirstOrDefault();
        if (string.IsNullOrEmpty(currencyId) || !CurrencyRegistry.TryGet(currencyId, out var handler) || handler == null)
        {
            Sawmill.Warning($"TryPurchase: No valid currency handler for listing {listingId}");
            return false;
        }

        var price = (int)listing.Cost.First().Value;
        var isSell = price < 0;

        var coords = _transform.ToMapCoordinates(_ents.GetComponent<TransformComponent>(machine).Coordinates);

        Sawmill.Debug($"TryPurchase: user={user}, isSell={isSell}, price={price}, currency={currencyId}");

        if (!isSell)
        {
            if (!handler.CanAfford(user, price))
            {
                Sawmill.Warning($"TryPurchase: User {user} cannot afford price {price} in {currencyId}");
                return false;
            }
            var debitRes = handler.Debit(user, price);
            if (debitRes != CurrencyOpResult.Success)
            {
                Sawmill.Warning($"TryPurchase: Debit failed: {debitRes} for user={user}, price={price}");
                return false;
            }

            Sawmill.Info($"TryPurchase: Success. Spawning product {listing.ProductEntity} at {coords}");
            SpawnProduct(listing.ProductEntity, user);
            return true;
        }

        if (string.IsNullOrEmpty(listing.ProductEntity))
        {
            Sawmill.Warning("TryPurchase: ProductEntity is null or empty for sell listing");
            return false;
        }
        if (!RemoveItem(user, listing.ProductEntity))
        {
            Sawmill.Warning($"TryPurchase: User {user} does not have product {listing.ProductEntity} to sell");
            return false;
        }
        var creditRes = handler.Credit(user, -price);
        if (creditRes != CurrencyOpResult.Success)
        {
            Sawmill.Warning($"TryPurchase: Credit failed: {creditRes} for user={user}, amount={-price}");
            return false;
        }

        Sawmill.Info($"TryPurchase: Success. User {user} sold {listing.ProductEntity} for {currencyId} ({-price})");
        return true;
    }

    public bool TryExchange(string listingId, EntityUid machine, NcStoreComponent? store, EntityUid user, StoreExchangeListingBoundUiMessage msg)
    {
        Sawmill.Info($"TryExchange: listingId={listingId}, user={user}, type={msg.ExchangeType}");
        if (store == null || store.Listings.Count == 0)
        {
            Sawmill.Warning("TryExchange: Store is null or has no listings");
            return false;
        }

        var listing = store.Listings.FirstOrDefault(x => x.Id == listingId);
        if (listing == null)
        {
            Sawmill.Warning($"TryExchange: Listing {listingId} not found");
            return false;
        }

        var coords = _transform.ToMapCoordinates(_ents.GetComponent<TransformComponent>(machine).Coordinates);

        switch (msg.ExchangeType)
        {
            case StoreExchangeType.CurrencyToCurrency:
                if (string.IsNullOrEmpty(msg.FromCurrencyId) || string.IsNullOrEmpty(msg.ToCurrencyId))
                {
                    Sawmill.Warning("TryExchange: CurrencyToCurrency: missing currency IDs");
                    return false;
                }
                var result = _exchange.Exchange(user, msg.FromCurrencyId, msg.ToCurrencyId, msg.Amount, msg.ExchangeRate);
                Sawmill.Info($"TryExchange: CurrencyToCurrency result={result}");
                return result == CurrencyOpResult.Success;

            case StoreExchangeType.ItemToCurrency:
                if (string.IsNullOrEmpty(msg.ItemProtoId) || string.IsNullOrEmpty(msg.ToCurrencyId))
                {
                    Sawmill.Warning("TryExchange: ItemToCurrency: missing item protoId or currencyId");
                    return false;
                }
                if (!RemoveItem(user, msg.ItemProtoId))
                {
                    Sawmill.Warning($"TryExchange: ItemToCurrency: user {user} does not have item {msg.ItemProtoId}");
                    return false;
                }
                if (!CurrencyRegistry.TryGet(msg.ToCurrencyId, out var handlerItemCur) || handlerItemCur == null)
                {
                    Sawmill.Warning($"TryExchange: ItemToCurrency: handler not found for {msg.ToCurrencyId}");
                    return false;
                }
                var creditRes = handlerItemCur.Credit(user, msg.Amount);
                Sawmill.Info($"TryExchange: ItemToCurrency: creditRes={creditRes}");
                return creditRes == CurrencyOpResult.Success;

            case StoreExchangeType.CurrencyToItem:
                if (string.IsNullOrEmpty(msg.FromCurrencyId) || string.IsNullOrEmpty(msg.ItemProtoId))
                {
                    Sawmill.Warning("TryExchange: CurrencyToItem: missing currencyId or item protoId");
                    return false;
                }
                if (!CurrencyRegistry.TryGet(msg.FromCurrencyId, out var handlerCurItem) || handlerCurItem == null)
                {
                    Sawmill.Warning($"TryExchange: CurrencyToItem: handler not found for {msg.FromCurrencyId}");
                    return false;
                }
                if (!handlerCurItem.CanAfford(user, msg.Amount))
                {
                    Sawmill.Warning($"TryExchange: CurrencyToItem: user {user} cannot afford {msg.Amount} {msg.FromCurrencyId}");
                    return false;
                }
                var debitRes = handlerCurItem.Debit(user, msg.Amount);
                if (debitRes != CurrencyOpResult.Success)
                {
                    Sawmill.Warning($"TryExchange: CurrencyToItem: debit failed: {debitRes}");
                    return false;
                }
                Sawmill.Info($"TryExchange: CurrencyToItem: spawning product {msg.ItemProtoId} at {coords}");
                SpawnProduct(listing.ProductEntity, user);
                return true;

            case StoreExchangeType.ItemToItem:
                if (string.IsNullOrEmpty(msg.ItemProtoId) || string.IsNullOrEmpty(msg.ToItemProtoId))
                {
                    Sawmill.Warning("TryExchange: ItemToItem: missing item protoId(s)");
                    return false;
                }
                if (!RemoveItem(user, msg.ItemProtoId))
                {
                    Sawmill.Warning($"TryExchange: ItemToItem: user {user} does not have item {msg.ItemProtoId}");
                    return false;
                }
                Sawmill.Info($"TryExchange: ItemToItem: spawning product {msg.ToItemProtoId} at {coords}");
                SpawnProduct(listing.ProductEntity, user);
                return true;

            default:
                Sawmill.Warning($"TryExchange: Unknown exchange type {msg.ExchangeType}");
                return false;
        }
    }

    private bool RemoveItem(EntityUid user, string protoId)
    {
        Sawmill.Debug($"RemoveItem: user={user}, protoId={protoId}");
        foreach (var item in CurrencyHelpers.EnumerateDeepItemsUnique(user, _ents))
        {
            var meta = _ents.GetComponent<MetaDataComponent>(item);
            Sawmill.Info($"[RemoveItem] user={user}, item={item}, protoId={meta.EntityPrototype?.ID}, wanted={protoId}");

            // Только для нестакуемых
            if (meta.EntityPrototype?.ID == protoId)
            {
                _ents.DeleteEntity(item);
                Sawmill.Info($"[RemoveItem] Deleted item {item} (protoId={protoId}) from user {user}");
                return true;
            }
        }
        Sawmill.Warning($"[RemoveItem] User {user} does not have item with protoId={protoId}");
        return false;
    }




    private void SpawnProduct(string protoId, EntityUid user)
    {
        var userXform = _ents.GetComponent<TransformComponent>(user);
        var coords = userXform.Coordinates;

        Sawmill.Info($"SpawnProduct: Spawning {protoId} for user {user} at {coords}");
        var spawned = _ents.SpawnEntity(protoId, coords);

        // Пытаемся вложить в руки
        if (_ents.TryGetComponent(user, out HandsComponent? hands))
        {
            var handsSys = EntitySystem.Get<SharedHandsSystem>();
            if (!handsSys.TryPickupAnyHand(user, spawned, checkActionBlocker: false))
            {
                Sawmill.Warning($"SpawnProduct: No free hand for {user}, spawned {protoId} at their feet.");
                // Если не удалось вложить — предмет остаётся на координатах игрока
            }
            else
            {
                Sawmill.Info($"SpawnProduct: Spawned {protoId} placed in hand of {user}");
            }
        }
        else
        {
            Sawmill.Warning($"SpawnProduct: User {user} has no hands! Spawning {protoId} at their feet.");
        }
    }

}
