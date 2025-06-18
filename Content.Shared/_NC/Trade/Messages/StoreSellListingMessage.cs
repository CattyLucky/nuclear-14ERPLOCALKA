using Robust.Shared.Serialization;

namespace Content.Shared._NC.Trade.Messages;

[NetSerializable]
public sealed class StoreSellListingMessage(string protoId) : BoundUserInterfaceMessage
{
    public readonly string ProtoId = protoId;
}
