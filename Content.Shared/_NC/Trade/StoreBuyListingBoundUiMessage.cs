using Robust.Shared.Serialization;

namespace Content.Shared._NC.Trade;

[Serializable, NetSerializable]
public sealed class StoreBuyListingBoundUiMessage : BoundUserInterfaceMessage
{
    public string ListingId;
    public uint ActorUid;

    public StoreBuyListingBoundUiMessage(string listingId, uint actorUid)
    {
        ListingId = listingId;
        ActorUid = actorUid;
    }
}
