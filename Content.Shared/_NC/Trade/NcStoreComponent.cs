using Robust.Shared.GameStates;


namespace Content.Shared._NC.Trade;


[RegisterComponent, NetworkedComponent]
public sealed partial class NcStoreComponent : Component
{
    [DataField("categories")]
    public List<string> Categories = new();

    [DataField("currencyWhitelist")]
    public List<string> CurrencyWhitelist = new();

    public EntityUid? CurrentUser = null;

    [DataField("listings")]
    public List<StoreListingPrototype> Listings = new();

    [DataField("preset")]
    public string? Preset;

    [DataField("access")]
    public List<List<string>>? Access;
}
