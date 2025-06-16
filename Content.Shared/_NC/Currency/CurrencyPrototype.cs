using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._NC.Currency;

[Prototype("NcCurrency")]
public sealed partial class CurrencyPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = string.Empty;

    [DataField(
        "entity",
        required: true,
        customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string Entity { get; private set; } = string.Empty;
}
