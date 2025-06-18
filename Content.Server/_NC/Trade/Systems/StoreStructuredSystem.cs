using Content.Shared._NC.Trade.UiDto;
using Content.Shared._NC.Trade.Prototypes;
using Content.Shared._NC.Currency;
using Robust.Shared.Prototypes;

namespace Content.Server._NC.Trade.Systems;

/// <summary>
/// Обрабатывает UI события магазина, отправляет состояние клиенту, вызывает логику покупки/продажи/обмена.
/// </summary>
public sealed class StoreStructuredSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly NcStoreLogicSystem _logic = default!;

    // Вызывается когда игрок хочет купить товар
    public CurrencyOpResult Buy(EntityUid user, string listingId)
    {
        if (!_protos.TryIndex<StoreListingPrototype>(listingId, out var listing))
            return CurrencyOpResult.Invalid;
        return _logic.Buy(user, listing);
    }

    // Продажа
    public CurrencyOpResult Sell(EntityUid user, EntityUid item, string listingId)
    {
        if (!_protos.TryIndex<StoreListingPrototype>(listingId, out var listing))
            return CurrencyOpResult.Invalid;
        return _logic.Sell(user, item, listing);
    }

    // Обмен (пример — в простейшем виде)
    public CurrencyOpResult Exchange(EntityUid user, EntityUid givenItem, string listingId, string? receiveProduct = null, int? currencyAmount = null)
    {
        if (!_protos.TryIndex<StoreListingPrototype>(listingId, out var listing))
            return CurrencyOpResult.Invalid;
        return _logic.Exchange(user, givenItem, listing, receiveProduct, currencyAmount);
    }

    // Получить UI состояние магазина (для клиента)
    public StoreUiState GetUiState(EntityUid user, StorePresetStructuredPrototype preset)
    {
        var buyListings = new List<StoreListingData>();
        var sellListings = new List<StoreListingData>();
        var exchangeListings = new List<StoreListingData>();

        foreach (var listingId in preset.Listings)
        {
            if (!_protos.TryIndex<StoreListingPrototype>(listingId, out var listing))
                continue;

            var data = new StoreListingData(listing.Product, listing.Price, listing.Currency);

            switch (listing.Mode)
            {
                case StoreMode.Buy:
                    buyListings.Add(data);
                    break;
                case StoreMode.Sell:
                    sellListings.Add(data);
                    break;
                case StoreMode.Exchange:
                    exchangeListings.Add(data);
                    break;
            }
        }

        // Баланс — по первой валюте покупок, например:
        var balance = 0;
        if (buyListings.Count > 0)
        {
            var currency = buyListings[0].Currency;
            var handler = CurrencyRegistry.TryGet(currency);
            if (handler != null)
                balance = handler.GetBalance(user);
        }

        return new StoreUiState(balance, buyListings, sellListings, exchangeListings);
    }
}
