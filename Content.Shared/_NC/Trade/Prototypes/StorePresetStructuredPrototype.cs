using Robust.Shared.Prototypes;

namespace Content.Shared._NC.Trade.Prototypes;

/// <summary>
/// Shared‑прототип набора листингов магазина (ассортимент). Привязывается к автомату/магазину.
/// </summary>
[Prototype("NcStorePresetStructured")]
public sealed partial class StorePresetStructuredPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// Список id‑листингов (StoreListingPrototype), которые входят в этот магазин.
    /// </summary>
    [DataField("listings", required: true)]
    public List<string> Listings { get; private set; } = new();
}
