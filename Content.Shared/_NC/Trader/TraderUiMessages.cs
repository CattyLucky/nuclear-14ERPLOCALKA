using Robust.Shared.Serialization;

namespace Content.Shared._NC.Trader;

[Serializable, NetSerializable,]
public sealed class BuyItemMessage(string productId, EntityUid sender) : BoundUserInterfaceMessage
{
    public string ProductId = productId;
    public EntityUid Sender = sender;
}
