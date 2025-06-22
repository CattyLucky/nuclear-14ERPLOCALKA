using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;


namespace Content.Shared._NC.Trade;


[Serializable, NetSerializable, Prototype("ncStoreListing")]
public sealed class StoreListingPrototype : IPrototype
{
    [DataField("categories")]
    public List<string> Categories = new();

    [DataField("conditions")]
    public List<ListingConditionPrototype> Conditions = new();

    [DataField("cost")]
    public Dictionary<string, float> Cost = new();

    [IdDataField]
    public string Id = string.Empty; // Только этот атрибут!

    [DataField("mode")]
    public StoreMode Mode = StoreMode.Buy;

    [DataField("productEntity")]
    public string ProductEntity = string.Empty;

    public string ID => Id;
}
