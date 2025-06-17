namespace Content.Shared._NC.Currency
{
    /// <summary>
    /// Результат операций с валютой (снятие/выдача и т.п.).
    /// </summary>
    public enum CurrencyOpResult
    {
        Success,
        InsufficientFunds,
        Invalid,
        NoHandler,
        Overflow,
        Denied
    }
}
