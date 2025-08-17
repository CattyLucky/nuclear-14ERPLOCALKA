using Robust.Shared.Serialization;


namespace Content.Shared._NC.Trade;


[Serializable, NetSerializable,]
public sealed class StoreSellListingBoundUiMessage : BoundUserInterfaceMessage
{
    public StoreSellListingBoundUiMessage(string listingId, int count)
    {
        ListingId = listingId;
        Count = count;
    }

    public string ListingId { get; }
    public int Count { get; }
}
