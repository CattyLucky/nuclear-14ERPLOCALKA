using Robust.Shared.Serialization;


namespace Content.Shared._NC.Trade;


[Serializable, NetSerializable]
public sealed class StoreListingData
{
    public string Category;
    public string CurrencyId;
    public string Id;
    public StoreMode Mode;
    public int Price;
    public string ProductEntity; // protoId

    public StoreListingData(
        string id,
        string productEntity,
        int price,
        string category,
        string currencyId,
        StoreMode mode
    )
    {
        Id = id;
        ProductEntity = productEntity;
        Price = price;
        Category = category;
        CurrencyId = currencyId;
        Mode = mode;
    }
}

public enum StoreMode
{
    Buy,
    Sell,
    Exchange
}
