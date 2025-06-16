using Content.Server.Stack;
using Content.Shared._NC.Currency;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server._NC.Currency;

public sealed class CurrencyExchangeSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _ents = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly StackSystem _stacks = default!;

    public CurrencyOpResult Exchange(
        EntityUid owner,
        string fromCurrencyId,
        string toCurrencyId,
        int amount,
        float rate = 1.0f
    )
    {
        if (!CurrencyRegistry.TryGet(fromCurrencyId, out var fromHandler) ||
            !CurrencyRegistry.TryGet(toCurrencyId, out var toHandler) ||
            fromHandler == null || toHandler == null)
            return CurrencyOpResult.Invalid;

        if (amount <= 0 || rate <= 0f)
            return CurrencyOpResult.Invalid;

        var amountTo = (int)Math.Floor(amount * rate);

        if (fromHandler.GetBalance(owner) < amount)
            return CurrencyOpResult.InsufficientFunds;

        var debitResult = fromHandler.Debit(owner, amount);
        if (debitResult != CurrencyOpResult.Success)
            return debitResult;

        var creditResult = toHandler.Credit(owner, amountTo);
        if (creditResult != CurrencyOpResult.Success)
        {
            fromHandler.Credit(owner, amount); // Rollback!
            return creditResult;
        }

        return CurrencyOpResult.Success;
    }


    public CurrencyOpResult ExchangeItemToCurrency(
        EntityUid owner,
        EntityUid item,
        string toCurrencyId,
        int currencyAmount
    )
    {
        if (!_ents.EntityExists(item))
            return CurrencyOpResult.Invalid;

        if (!CurrencyRegistry.TryGet(toCurrencyId, out var toHandler) || toHandler == null)
            return CurrencyOpResult.Invalid;

        if (currencyAmount <= 0)
            return CurrencyOpResult.Invalid;

        var creditResult = toHandler.Credit(owner, currencyAmount);
        if (creditResult != CurrencyOpResult.Success)
            return creditResult;

        _ents.DeleteEntity(item);
        return CurrencyOpResult.Success;
    }


    public bool ExchangeItemToItem(
        EntityUid owner,
        EntityUid giveItem,
        string receivePrototypeId
    )
    {
        if (!_ents.EntityExists(giveItem))
            return false;

        var coords = _ents.GetComponent<TransformComponent>(owner).Coordinates;
        var spawned = _ents.SpawnEntity(receivePrototypeId, coords);
        if (!_ents.EntityExists(spawned))
            return false;

        _ents.DeleteEntity(giveItem);
        return true;
    }

    public bool ExchangeItemToStack(
        EntityUid owner,
        EntityUid giveItem,
        string stackPrototypeId,
        int count
    )
    {
        if (!_ents.EntityExists(giveItem))
            return false;

        if (count <= 0)
            return false;

        var coords = _ents.GetComponent<TransformComponent>(owner).Coordinates;
        if (!_protos.TryIndex<StackPrototype>(stackPrototypeId, out var stackProto))
            return false;

        var spawned = _stacks.Spawn(count, stackProto, coords);

        if (!_ents.EntityExists(spawned))
            return false;

        if (!_ents.TryGetComponent(spawned, out StackComponent? stackComp))
            return false;

        if (stackComp.Count != count)
            return false;

        _ents.DeleteEntity(giveItem);
        return true;
    }
}
