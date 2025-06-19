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

    public CurrencyOpResult Exchange(EntityUid owner, string fromCurrencyId, string toCurrencyId, int amount, float rate = 1f)
    {
        Sawmill.Info($"Exchange: {amount} {fromCurrencyId} -> {toCurrencyId} for {owner} at rate {rate}");

        if (amount <= 0 || rate <= 0f)
        {
            Sawmill.Warning($"Exchange: invalid amount/rate: amount={amount}, rate={rate}");
            return CurrencyOpResult.Invalid;
        }

        if (!CurrencyRegistry.TryGet(fromCurrencyId, out var fromHandler) || fromHandler == null ||
            !CurrencyRegistry.TryGet(toCurrencyId,   out var toHandler)   || toHandler   == null)
        {
            Sawmill.Error($"Exchange: handler(s) not found: from={fromCurrencyId}, to={toCurrencyId}");
            return CurrencyOpResult.NoHandler;
        }

        var raw   = (double) amount * rate;
        var amtTo = raw > int.MaxValue ? int.MaxValue : (int) Math.Floor(raw);
        if (amtTo <= 0)
        {
            Sawmill.Warning($"Exchange: result amount too low, amtTo={amtTo}");
            return CurrencyOpResult.Invalid;
        }

        var debitRes = fromHandler.Debit(owner, amount);
        if (debitRes != CurrencyOpResult.Success)
        {
            Sawmill.Warning($"Exchange: debit failed for {owner}: {debitRes}");
            return debitRes;
        }

        var creditRes = toHandler.Credit(owner, amtTo);
        if (creditRes == CurrencyOpResult.Success)
        {
            Sawmill.Info($"Exchange success for {owner}: {amount} {fromCurrencyId} -> {amtTo} {toCurrencyId}");
            return CurrencyOpResult.Success;
        }

        var rollback = fromHandler.Credit(owner, amount);
        Sawmill.Error($"[Exchange] rollback {owner}: {creditRes}, rollbackResult={rollback}");
        return creditRes;
    }

    public CurrencyOpResult ExchangeItemToCurrency(
        EntityUid owner,
        EntityUid item,
        string    toCurrencyId,
        int       currencyAmount
    )
    {
        Sawmill.Info($"ExchangeItemToCurrency: owner={owner}, item={item}, toCurrencyId={toCurrencyId}, amount={currencyAmount}");

        if (!_ents.EntityExists(item) || currencyAmount <= 0)
        {
            Sawmill.Warning($"ExchangeItemToCurrency: invalid item or amount");
            return CurrencyOpResult.Invalid;
        }

        if (!CurrencyRegistry.TryGet(toCurrencyId, out var toHandler) || toHandler == null)
        {
            Sawmill.Error($"ExchangeItemToCurrency: handler not found for {toCurrencyId}");
            return CurrencyOpResult.NoHandler;
        }

        var creditRes = toHandler.Credit(owner, currencyAmount);
        if (creditRes != CurrencyOpResult.Success)
        {
            Sawmill.Warning($"ExchangeItemToCurrency: credit failed {creditRes}");
            return creditRes;
        }

        _ents.DeleteEntity(item);
        Sawmill.Info($"ExchangeItemToCurrency: exchanged and deleted {item}");
        return CurrencyOpResult.Success;
    }

    public bool ExchangeItemToItem(
        EntityUid owner,
        EntityUid giveItem,
        string    receivePrototypeId
    )
    {
        Sawmill.Info($"ExchangeItemToItem: owner={owner}, giveItem={giveItem}, receivePrototypeId={receivePrototypeId}");

        if (!_ents.EntityExists(giveItem))
        {
            Sawmill.Warning($"ExchangeItemToItem: giveItem does not exist");
            return false;
        }

        var coords  = _ents.GetComponent<TransformComponent>(owner).Coordinates;
        var spawned = _ents.SpawnEntity(receivePrototypeId, coords);

        if (!_ents.EntityExists(spawned))
        {
            Sawmill.Error($"ExchangeItemToItem: spawned entity {receivePrototypeId} failed");
            return false;
        }

        _ents.DeleteEntity(giveItem);
        Sawmill.Info($"ExchangeItemToItem: exchanged and deleted {giveItem}, spawned {spawned}");
        return true;
    }

    public bool ExchangeItemToStack(
        EntityUid owner,
        EntityUid giveItem,
        string    stackPrototypeId,
        int       count
    )
    {
        Sawmill.Info($"ExchangeItemToStack: owner={owner}, giveItem={giveItem}, stackPrototypeId={stackPrototypeId}, count={count}");

        if (!_ents.EntityExists(giveItem) || count <= 0)
        {
            Sawmill.Warning($"ExchangeItemToStack: invalid giveItem or count");
            return false;
        }

        if (!_proto.TryIndex<StackPrototype>(stackPrototypeId, out var stackProto))
        {
            Sawmill.Error($"ExchangeItemToStack: stackProto not found for {stackPrototypeId}");
            return false;
        }

        var coords  = _ents.GetComponent<TransformComponent>(owner).Coordinates;
        var spawned = _stacks.Spawn(count, stackProto, coords);

        if (!_ents.EntityExists(spawned) ||
            !_ents.TryGetComponent(spawned, out StackComponent? sc) ||
            sc.Count != count)
        {
            Sawmill.Error($"ExchangeItemToStack: spawn failed or incorrect count for {stackPrototypeId}");
            return false;
        }

        _ents.DeleteEntity(giveItem);
        Sawmill.Info($"ExchangeItemToStack: exchanged and deleted {giveItem}, spawned stack {spawned} (count={sc.Count})");
        return true;
    }
}
