using Robust.Shared.Serialization;


namespace Content.Shared._NC.Trade;


[Serializable, NetSerializable]
public sealed class StoreBuyListingBoundUiMessage : BoundUserInterfaceMessage
{
    public uint ActorUid;
    public string ListingId;

    public StoreBuyListingBoundUiMessage(string listingId, uint actorUid)
    {
        ListingId = listingId;
        ActorUid = actorUid;
    }
}
