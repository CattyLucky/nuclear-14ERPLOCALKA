using Robust.Shared.Prototypes;


namespace Content.Shared._NC.Trade;


[Prototype("storePresetStructured")]
public sealed partial class StorePresetStructuredPrototype : IPrototype
{
    [DataField("catalog", required: true)]
    public Dictionary<string, Dictionary<string, List<StoreCatalogEntry>>> Catalog = new();

    [DataField("currency", required: true)]
    public string Currency = string.Empty;

    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataDefinition]
    public sealed partial class StoreCatalogEntry
    {
        [DataField("category")]
        public string Category = string.Empty;

        [DataField("price")]
        public int Price;

        [DataField("proto", required: true)]
        public string Proto = string.Empty;

        [DataField("count")] public int? Count { get; private set; }
    }
}
