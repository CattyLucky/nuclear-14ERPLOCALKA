using Content.Shared._NC.Currency;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;


namespace Content.Shared._NC.Trade.Prototypes;

[Prototype("NcStoreListing")]
public sealed partial class StoreListingPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = string.Empty;

    [DataField("mode", required: true)]           public StoreMode Mode { get; private set; }
    [DataField("price", required: true)]          public int       Price { get; private set; }
    [DataField(
        "currency",
        required: true,
        customTypeSerializer: typeof(PrototypeIdSerializer<CurrencyPrototype>))]
    public string Currency { get; private set; } = default!;

    [DataField(
        "product",
        required: true,
        customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string Product { get; private set; } = default!;
}

public enum StoreMode { Buy, Sell, Exchange, }
