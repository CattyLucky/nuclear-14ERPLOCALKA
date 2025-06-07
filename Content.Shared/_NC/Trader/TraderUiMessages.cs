using Robust.Shared.Serialization;

namespace Content.Shared._NC.Trader;

[Serializable, NetSerializable]
public sealed class BuyItemMessage : BoundUserInterfaceMessage
{
    public string ProductId;
    public int Amount;

    public BuyItemMessage(string productId, int amount)
    {
        ProductId = productId;
        Amount = amount;
    }
}
