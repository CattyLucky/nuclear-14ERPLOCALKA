namespace Content.Shared._NC.Currency;

public enum CurrencyOpResult
{
    Success,
    Invalid,
    InsufficientFunds
}

public interface ICurrencyHandler
{
    string Id { get; }
    int  GetBalance(EntityUid owner);
    CurrencyOpResult Debit  (EntityUid owner, int amount);
    CurrencyOpResult Credit (EntityUid owner, int amount);
}
