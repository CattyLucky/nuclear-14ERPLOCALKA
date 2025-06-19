using System.Linq;
using Content.Shared._NC.Currency;
using Content.Shared._NC.Trade;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Content.Server._NC.Currency;

namespace Content.Server._NC.Trade;

public sealed class NcStoreLogicSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _ents = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly CurrencyExchangeSystem _exchange = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public int GetBalance(EntityUid user, string currencyId)
    {
        if (!CurrencyRegistry.TryGet(currencyId, out var handler) || handler == null)
            return 0;
        return handler.GetBalance(user);
    }


    public bool TryPurchase(string listingId, EntityUid machine, NcStoreComponent? store, EntityUid user)
    {
        if (store == null || store.Listings.Count == 0)
            return false;

        var listing = store.Listings.FirstOrDefault(x => x.Id == listingId);
        if (listing == null || listing.Cost.Count == 0 || string.IsNullOrEmpty(listing.ProductEntity))
            return false;

        var currencyId = store.CurrencyWhitelist.FirstOrDefault();
        if (string.IsNullOrEmpty(currencyId) || !CurrencyRegistry.TryGet(currencyId, out var handler) || handler == null)
            return false;

        var price = (int)listing.Cost.First().Value;
        var isSell = price < 0;

        var coords = _transform.ToMapCoordinates(_ents.GetComponent<TransformComponent>(machine).Coordinates);





        if (!isSell)
        {
            if (!handler.CanAfford(user, price))
                return false;
            if (handler.Debit(user, price) != CurrencyOpResult.Success)
                return false;

            SpawnProduct(listing.ProductEntity, coords);
            return true;
        }

        if (string.IsNullOrEmpty(listing.ProductEntity))
            return false;
        if (!RemoveItem(user, listing.ProductEntity))
            return false;
        if (handler.Credit(user, -price) != CurrencyOpResult.Success)
            return false;

        return true;
    }



    public bool TryExchange(string listingId, EntityUid machine, NcStoreComponent? store, EntityUid user, StoreExchangeListingBoundUiMessage msg)
{
    if (store == null || store.Listings.Count == 0)
        return false;

    var listing = store.Listings.FirstOrDefault(x => x.Id == listingId);
    if (listing == null)
        return false;

    var coords = _transform.ToMapCoordinates(_ents.GetComponent<TransformComponent>(machine).Coordinates);


    switch (msg.ExchangeType)
    {
        case StoreExchangeType.CurrencyToCurrency:
            if (string.IsNullOrEmpty(msg.FromCurrencyId) || string.IsNullOrEmpty(msg.ToCurrencyId))
                return false;
            var result = _exchange.Exchange(user, msg.FromCurrencyId, msg.ToCurrencyId, msg.Amount, msg.ExchangeRate);
            return result == CurrencyOpResult.Success;

        case StoreExchangeType.ItemToCurrency:
            if (string.IsNullOrEmpty(msg.ItemProtoId) || string.IsNullOrEmpty(msg.ToCurrencyId))
                return false;
            if (!RemoveItem(user, msg.ItemProtoId))
                return false;
            if (!CurrencyRegistry.TryGet(msg.ToCurrencyId, out var handlerItemCur) || handlerItemCur == null)
                return false;
            return handlerItemCur.Credit(user, msg.Amount) == CurrencyOpResult.Success;

        case StoreExchangeType.CurrencyToItem:
            if (string.IsNullOrEmpty(msg.FromCurrencyId) || string.IsNullOrEmpty(msg.ItemProtoId))
                return false;
            if (!CurrencyRegistry.TryGet(msg.FromCurrencyId, out var handlerCurItem) || handlerCurItem == null)
                return false;
            if (!handlerCurItem.CanAfford(user, msg.Amount))
                return false;
            if (handlerCurItem.Debit(user, msg.Amount) != CurrencyOpResult.Success)
                return false;
            SpawnProduct(msg.ItemProtoId, coords);
            return true;

        case StoreExchangeType.ItemToItem:
            if (string.IsNullOrEmpty(msg.ItemProtoId) || string.IsNullOrEmpty(msg.ToItemProtoId))
                return false;
            if (!RemoveItem(user, msg.ItemProtoId))
                return false;
            SpawnProduct(msg.ToItemProtoId, coords);
            return true;

        default:
            return false;
    }
}

    private bool RemoveItem(EntityUid user, string protoId)
    {
        foreach (var item in CurrencyHelpers.EnumerateDeepItemsUnique(user, _ents))
            if (_ents.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID == protoId)
            {
                _ents.DeleteEntity(item);
                return true;
            }

        return false;
    }

    private void SpawnProduct(string protoId, MapCoordinates coords) =>
        _ents.SpawnEntity(protoId, coords);
}
