using Robust.Shared.Prototypes;
using Robust.Shared.Serialization; // Обязательно!
using Robust.Shared.Utility;

namespace Content.Shared._NC.Trade;

/// <summary>
/// YAML-прототип магазина с валютой, категориями и каталогом.
/// </summary>
[Prototype("storePresetStructured")]
public sealed partial class StorePresetStructuredPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("currency", required: true)]
    public string Currency = string.Empty;

    [DataField("catalog", required: true)]
    public Dictionary<string, Dictionary<string, List<StoreCatalogEntry>>> Catalog = new();
}

// ЭТОТ КЛАСС ОБЯЗАТЕЛЬНО ДОЛЖЕН БЫТЬ ПОМЕЧЕН [DataDefinition] !!!
[DataDefinition] // <--- ВАЖНО!
public sealed partial class StoreCatalogEntry
{
    [DataField("proto", required: true)]
    public string Proto = string.Empty;

    [DataField("name")]
    public string? Name;

    [DataField("description")]
    public string? Description;

    [DataField("icon")]
    public SpriteSpecifier? Icon;

    [DataField("price")]
    public int Price = 0;
}
