using Robust.Shared.Serialization;


namespace Content.Shared._NC.Trade;


/// <summary>
///     Сообщение клиента на сервер для обмена/конверсии/бартера.
/// </summary>
[Serializable, NetSerializable]
public sealed class StoreExchangeListingBoundUiMessage : BoundUserInterfaceMessage
{
    public uint ActorUid;
    public int Amount;
    public float ExchangeRate;
    public StoreExchangeType ExchangeType;
    public string? FromCurrencyId;
    public string? ItemProtoId;
    public string ListingId;
    public string? ToCurrencyId;
    public string? ToItemProtoId;

    public StoreExchangeListingBoundUiMessage(
        StoreExchangeType exchangeType,
        string? fromCurrencyId,
        string? toCurrencyId,
        int amount,
        string? itemProtoId,
        string? toItemProtoId,
        float exchangeRate,
        uint actorUid,
        string listingId
    )
    {
        ExchangeType = exchangeType;
        FromCurrencyId = fromCurrencyId;
        ToCurrencyId = toCurrencyId;
        Amount = amount;
        ItemProtoId = itemProtoId;
        ToItemProtoId = toItemProtoId;
        ExchangeRate = exchangeRate;
        ActorUid = actorUid;
        ListingId = listingId;
    }
}

public enum StoreExchangeType
{
    CurrencyToCurrency,
    ItemToCurrency,
    CurrencyToItem,
    ItemToItem
}
