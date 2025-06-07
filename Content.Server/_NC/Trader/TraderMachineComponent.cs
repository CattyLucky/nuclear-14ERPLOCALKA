using Robust.Shared.GameStates;
using Content.Shared._NC.Trader;

namespace Content.Server._NC.Trader;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TraderMachineComponent : Component
{
    [DataField("currencyAccepted"), AutoNetworkedField]
    public string CurrencyAccepted = "CapCoin";

    // YAML-структура: Категория -> Прототип -> Цена
    [DataField("inventory")]
    public Dictionary<string, Dictionary<string, int>> InventoryRaw = new();

    // Конвертированный список для UI (id -> listing)
    [ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<string, TraderListingData> Listings = new();

    [ViewVariables(VVAccess.ReadWrite)]
    public int StoredCurrency = 0;

    /// <summary>
    /// Последний игрок, активировавший интерфейс автомата.
    /// Используется на сервере при получении BuyItemMessage.
    /// </summary>
    [ViewVariables]
    public EntityUid? LastUser;
}
