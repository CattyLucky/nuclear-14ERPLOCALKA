namespace Content.Server._NC.Trade.Systems;


[RegisterComponent]
public sealed partial class StoreStructuredComponent : Component
{
    [DataField("presetId")] public string PresetId = string.Empty;
    [DataField("listingIds")] public List<string> ListingIds = new();
}
