using Robust.Shared.Prototypes;

namespace Content.Shared._NC.Trade;

[Prototype("storePresetStructured")]
public sealed partial class StorePresetStructuredPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("currency", required: true)]
    public string Currency = string.Empty;

    [DataField("catalog", required: true)]
    public Dictionary<string, Dictionary<string, List<StoreCatalogEntry>>> Catalog = new();

[DataDefinition]
public sealed partial class StoreCatalogEntry
{
    [DataField("proto", required: true)]
    public string Proto = string.Empty;

    [DataField("price")]
    public int Price = 0;

    [DataField("category")]
    public string Category = string.Empty;
}
}
