using Robust.Shared.Serialization;

namespace Content.Shared._NC.Trader;

[Serializable, NetSerializable]
public sealed class BuyItemMessage : BoundUserInterfaceMessage
{
    public string ProductId;
    public EntityUid Sender;
    public int Amount;

    public BuyItemMessage(string productId, EntityUid sender, int amount = 1)
    {
        ProductId = productId;
        Sender = sender;
        Amount = amount;
    }
}

