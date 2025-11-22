using Robust.Shared.Serialization;


namespace Content.Shared._NC.Trade;


/// <summary>
///     Состояние UI магазина для клиента.
/// </summary>
[Serializable, NetSerializable]
public sealed class StoreUiState : BoundUserInterfaceState
{
    public int Balance;
    public List<StoreListingData> Listings;

    public StoreUiState(int balance, List<StoreListingData> listings)
    {
        Balance = balance;
        Listings = listings;
    }
}
