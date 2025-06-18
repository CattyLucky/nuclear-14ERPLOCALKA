using Content.Server.Stack;
using Content.Shared._NC.Currency;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server._NC.Currency;

public sealed class CurrencyExchangeSystem : EntitySystem
{
    #region Dependencies

    [Dependency] private readonly IEntityManager   _ents   = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly StackSystem      _stacks = default!;

    #endregion

    private static readonly ISawmill Sawmill = Logger.GetSawmill("currency-exchange");

    public CurrencyOpResult Exchange(
        EntityUid owner,
        string    fromCurrencyId,
        string    toCurrencyId,
        int       amount,
        float     rate = 1f
    )
    {
        if (amount <= 0 || rate <= 0)
            return CurrencyOpResult.Invalid;

        if (!CurrencyRegistry.TryGet(fromCurrencyId, out var fromHandler) ||
            !CurrencyRegistry.TryGet(toCurrencyId, out var toHandler))
            return CurrencyOpResult.NoHandler;

        var raw = (double) amount * rate;
        var amtTo = raw > int.MaxValue ? int.MaxValue : (int) Math.Floor(raw);

        if (amtTo <= 0)
            return CurrencyOpResult.Invalid;

        var debitRes = fromHandler.Debit(owner, amount);
        if (debitRes != CurrencyOpResult.Success)
            return debitRes;

        var creditRes = toHandler.Credit(owner, amtTo);
        if (creditRes == CurrencyOpResult.Success)
            return CurrencyOpResult.Success;
        fromHandler.Credit(owner, amount);
        Sawmill.Warning($"[Exchange] rollback for {owner}: {creditRes}");
        return creditRes;
    }

    public CurrencyOpResult ExchangeItemToCurrency(
        EntityUid owner,
        EntityUid item,
        string    toCurrencyId,
        int       currencyAmount
    )
    {
        if (!_ents.EntityExists(item) || currencyAmount <= 0)
            return CurrencyOpResult.Invalid;

        if (!CurrencyRegistry.TryGet(toCurrencyId, out var toHandler))
            return CurrencyOpResult.NoHandler;

        var creditRes = toHandler.Credit(owner, currencyAmount);
        if (creditRes != CurrencyOpResult.Success)
            return creditRes;

        _ents.DeleteEntity(item);
        return CurrencyOpResult.Success;
    }

    public bool ExchangeItemToItem(
        EntityUid owner,
        EntityUid giveItem,
        string    receivePrototypeId
    )
    {
        if (!_ents.EntityExists(giveItem))
            return false;

        var coords  = _ents.GetComponent<TransformComponent>(owner).Coordinates;
        var spawned = _ents.SpawnEntity(receivePrototypeId, coords);

        if (!_ents.EntityExists(spawned))
            return false;

        _ents.DeleteEntity(giveItem);
        return true;
    }

    public bool ExchangeItemToStack(
        EntityUid owner,
        EntityUid giveItem,
        string    stackPrototypeId,
        int       count
    )
    {
        if (!_ents.EntityExists(giveItem) || count <= 0)
            return false;

        if (!_proto.TryIndex<StackPrototype>(stackPrototypeId, out var stackProto))
            return false;

        var coords  = _ents.GetComponent<TransformComponent>(owner).Coordinates;
        var spawned = _stacks.Spawn(count, stackProto, coords);

        if (!_ents.EntityExists(spawned) ||
            !_ents.TryGetComponent(spawned, out StackComponent? sc) ||
            sc.Count != count)
            return false;

        _ents.DeleteEntity(giveItem);
        return true;
    }
}
