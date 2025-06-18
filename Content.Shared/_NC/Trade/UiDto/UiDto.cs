using Robust.Shared.Serialization;

namespace Content.Shared._NC.Trade.UiDto;

[NetSerializable]
public sealed class StoreUiState(
    int balance,
    List<StoreListingData> buyListings,
    List<StoreListingData> sellListings,
    List<StoreListingData> exchangeListings
) : BoundUserInterfaceState
{
    public readonly int Balance = balance;
    public readonly List<StoreListingData> BuyListings = buyListings;
    public readonly List<StoreListingData> SellListings = sellListings;
    public readonly List<StoreListingData> ExchangeListings = exchangeListings;
}

[Serializable, NetSerializable]
public sealed class StoreListingData(string protoId, int price, string currency)
{
    public readonly string ProtoId = protoId;
    public readonly int Price = price;
    public readonly string Currency = currency;
}
