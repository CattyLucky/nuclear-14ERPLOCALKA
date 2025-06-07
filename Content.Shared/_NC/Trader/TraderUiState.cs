using Robust.Shared.Serialization;

namespace Content.Shared._NC.Trader;

[Serializable, NetSerializable]
public sealed class TraderUpdateState(Dictionary<string, TraderListingData> inventory, int balance, string currencyAccepted)
    : BoundUserInterfaceState
{
    public readonly Dictionary<string, TraderListingData> Inventory = inventory;
    public readonly int Balance = balance;
    public readonly string CurrencyAccepted = currencyAccepted;
}

[Serializable, NetSerializable]
public sealed class TraderListingData
{
    public string Id = string.Empty;
    public string ProtoId = string.Empty;
    public int Price;
    public string Category = string.Empty;
    public string Name = string.Empty;
    public string? Icon = null;
    public string? Description = null;
}
