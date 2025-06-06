using Robust.Shared.GameStates;

namespace Content.Shared._NC.Trader;

[RegisterComponent]
[NetworkedComponent]
public sealed partial class CurrencyItemComponent : Component
{
    /// <summary>
    /// Тип валюты (например, CapCoin, Ruble, Credit).
    /// </summary>
    [DataField("currencyType")]
    public string CurrencyType = "CapCoin";

    /// <summary>
    /// Сколько стоит этот предмет (например, монетка на 5 = 5 CapCoin).
    /// </summary>
    [DataField("value")]
    public int Value = 1;
}
