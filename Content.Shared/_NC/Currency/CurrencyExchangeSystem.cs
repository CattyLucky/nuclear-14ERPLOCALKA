namespace Content.Shared._NC.Currency
{
    /// <summary>
    /// Shared-интерфейс для обмена валют.
    /// </summary>
    public interface ICurrencyExchangeSystem
    {
        CurrencyOpResult Exchange(EntityUid owner, string fromCurrencyId, string toCurrencyId, int amount, float rate = 1.0f);
    }
}
