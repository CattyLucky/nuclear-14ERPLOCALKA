using Robust.Shared.Prototypes;


namespace Content.Shared._NC.Trade.Prototypes;

[Prototype("NcStorePresetStructured")]
public sealed partial class StorePresetStructuredPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = string.Empty;

    // Список ID StoreListingPrototype
    [DataField("listings", required: true)]
    public List<string> Listings { get; private set; } = new();
}
