using Robust.Shared.Serialization;


namespace Content.Shared._NC.Trade;


[NetSerializable, Serializable,]
public sealed class StoreBuyListingBoundUiMessage : BoundUserInterfaceMessage
{
    public StoreBuyListingBoundUiMessage(string listingId, int count)
    {
        ListingId = listingId;
        Count = count;
    }

    public string ListingId { get; }
    public int Count { get; }
}
