using Robust.Shared.Serialization;

namespace Content.Shared._NC.Trade.Messages;

[NetSerializable]
public sealed class StoreExchangeListingMessage : BoundUserInterfaceMessage
{
    [DataField("protoId")]
    public readonly string ProtoId;

    [DataField("itemUid")]
    public readonly EntityUid ItemUid;

    public StoreExchangeListingMessage(string protoId, EntityUid itemUid)
    {
        ProtoId = protoId;
        ItemUid = itemUid;
    }
}
