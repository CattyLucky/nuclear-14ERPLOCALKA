using Robust.Shared.GameStates;

namespace Content.Shared._NC.Trade;

/// <summary>
/// Shared-компонент магазина. Содержит настройки пресета, валюту, категории, товары.
/// </summary>
[RegisterComponent, NetworkedComponent,]
public sealed partial class NcStoreComponent : Component
{
    [DataField("preset")]
    public string? Preset;

    [DataField("currencyWhitelist")]
    public List<string> CurrencyWhitelist = new();

    [DataField("categories")]
    public List<string> Categories = new();

    [DataField("listings")]
    public List<StoreListingPrototype> Listings = new();
}
