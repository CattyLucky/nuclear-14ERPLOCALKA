namespace Content.Shared._NC.Currency
{

    public interface ICurrencyHandler
    {

        string Id { get; }


        int GetBalance(EntityUid owner);


        CurrencyOpResult Debit(EntityUid owner, int amount);


        CurrencyOpResult Credit(EntityUid owner, int amount);


        bool CanAfford(EntityUid owner, int amount);


        void InvalidateBalanceCache(EntityUid owner);


        string? StackTypeId { get; }
    }
}

