using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._NC.Trade;

[Serializable, NetSerializable,]
public sealed class StoreListingData
{
    public string Id;
    public string Name;
    public string Description;
    public SpriteSpecifier Icon;
    public int Cost;
    public List<string> Categories;
    public StoreMode Mode;
    public string CurrencyId;

    public StoreListingData(
        string id,
        string name,
        string description,
        SpriteSpecifier icon,
        int cost,
        List<string> categories,
        StoreMode mode,
        string currencyId
    )
    {
        Id = id;
        Name = name;
        Description = description;
        Icon = icon;
        Cost = cost;
        Categories = categories;
        Mode = mode;
        CurrencyId = currencyId;
    }
}

public enum StoreMode
{
    Buy,
    Sell,
    Exchange
}
