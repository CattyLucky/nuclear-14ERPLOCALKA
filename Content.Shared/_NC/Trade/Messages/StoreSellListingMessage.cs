using Robust.Shared.Serialization;

namespace Content.Shared._NC.Trade.Messages;

[NetSerializable]
public sealed class StoreSellListingMessage : BoundUserInterfaceMessage
{
    public readonly string ProtoId;
    public readonly EntityUid ItemUid;

    public StoreSellListingMessage(string protoId, EntityUid itemUid)
    {
        ProtoId = protoId;
        ItemUid = itemUid;
    }
}
