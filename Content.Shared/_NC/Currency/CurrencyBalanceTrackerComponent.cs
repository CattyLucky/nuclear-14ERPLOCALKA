using System.Runtime.CompilerServices;
using Robust.Shared.GameStates;

namespace Content.Shared._NC.Currency;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CurrencyBalanceTrackerComponent : Component
{
    [DataField("balances"), AutoNetworkedField, ViewVariables]
    public Dictionary<string, int> Balances = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Get(string currencyId) =>
        Balances.TryGetValue(currencyId, out var amount) ? amount : 0;

    public void Set(EntityUid owner, string currencyId, int amount, IEntityManager ents)
    {
        if (amount == 0)
        {
            Remove(owner, currencyId, ents);
            return;
        }

        if (Balances.TryGetValue(currencyId, out var current) && current == amount)
            return;

        Balances[currencyId] = amount;
        ents.Dirty(owner, this);
    }

    public void Remove(EntityUid owner, string currencyId, IEntityManager ents)
    {
        if (Balances.Remove(currencyId))
            ents.Dirty(owner, this);
    }
}
