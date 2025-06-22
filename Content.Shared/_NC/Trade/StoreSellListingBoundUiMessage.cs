using Robust.Shared.Serialization;


namespace Content.Shared._NC.Trade;


[Serializable, NetSerializable]
public sealed class StoreSellListingBoundUiMessage : BoundUserInterfaceMessage
{
    public uint ActorUid;
    public string ListingId;

    public StoreSellListingBoundUiMessage(string listingId, uint actorUid)
    {
        ListingId = listingId;
        ActorUid = actorUid;
    }
}
