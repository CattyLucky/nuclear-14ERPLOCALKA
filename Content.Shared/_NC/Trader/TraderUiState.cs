using Robust.Shared.Serialization;

namespace Content.Shared._NC.Trader;

/// <summary>
/// Сервер отправляет клиенту: список товаров и баланс.
/// </summary>
[Serializable, NetSerializable,]
public sealed class TraderUpdateState(Dictionary<string, TraderListingData> inventory, int balance)
    : BoundUserInterfaceState
{
    public readonly Dictionary<string, TraderListingData> Inventory = inventory;
    public readonly int Balance = balance;
}

/// <summary>
/// Описание одного товара в UI автомата.
/// </summary>
[Serializable, NetSerializable,]
public sealed class TraderListingData
{
    public string Id = string.Empty;
    public int Price;
    public string Category = string.Empty;
    public string Name = string.Empty;
    public string? Icon = null; // ID спрайта прототипа
}
