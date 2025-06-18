using Content.Shared._NC.Currency;
using Content.Shared._NC.Trade.Prototypes;

namespace Content.Server._NC.Trade.Systems;

public sealed class NcStoreLogicSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _ents = default!;

    /// <summary>Покупка товара</summary>
    public CurrencyOpResult Buy(EntityUid user, StoreListingPrototype listing)
    {
        var handler = CurrencyRegistry.TryGet(listing.Currency);
        if (handler == null)
            return CurrencyOpResult.NoHandler;

        if (!handler.CanAfford(user, listing.Price))
            return CurrencyOpResult.InsufficientFunds;

        if (handler.Debit(user, listing.Price) != CurrencyOpResult.Success)
            return CurrencyOpResult.Invalid;

        var coords = _ents.GetComponent<TransformComponent>(user).Coordinates;
        _ents.SpawnEntity(listing.Product, coords);
        return CurrencyOpResult.Success;
    }

    /// <summary>Продажа предмета (игрок отдаёт предмет, получает валюту)</summary>
    public CurrencyOpResult Sell(EntityUid user, EntityUid item, StoreListingPrototype listing)
    {
        var handler = CurrencyRegistry.TryGet(listing.Currency);
        if (handler == null)
            return CurrencyOpResult.NoHandler;

        _ents.DeleteEntity(item);
        return handler.Credit(user, listing.Price);
    }

    /// <summary>Обмен предмета на предмет или валюту</summary>
    public CurrencyOpResult Exchange(EntityUid user, EntityUid givenItem, StoreListingPrototype listing, string? receiveProduct = null, int? currencyAmount = null)
    {
        _ents.DeleteEntity(givenItem);

        if (!string.IsNullOrEmpty(receiveProduct))
        {
            var coords = _ents.GetComponent<TransformComponent>(user).Coordinates;
            _ents.SpawnEntity(receiveProduct, coords);
            return CurrencyOpResult.Success;
        }
        if (currencyAmount is { } amount)
        {
            var handler = CurrencyRegistry.TryGet(listing.Currency);
            if (handler == null)
                return CurrencyOpResult.NoHandler;
            return handler.Credit(user, amount);
        }
        return CurrencyOpResult.Invalid;
    }
}
