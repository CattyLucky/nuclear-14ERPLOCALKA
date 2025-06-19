using Robust.Shared.Serialization;

namespace Content.Shared._NC.Trade;

[Serializable, NetSerializable]
public sealed class StoreSellListingBoundUiMessage : BoundUserInterfaceMessage
{
    public string ListingId;
    public uint ActorUid;

    public StoreSellListingBoundUiMessage(string listingId, uint actorUid)
    {
        ListingId = listingId;
        ActorUid = actorUid;
    }
}
