namespace Content.Shared._NC.Currency
{
    /// <summary>
    /// Универсальный интерфейс обработчика валюты.
    /// </summary>
    public interface ICurrencyHandler
    {
        /// <summary>
        /// Уникальный идентификатор валюты.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Получить баланс указанного владельца.
        /// </summary>
        int GetBalance(EntityUid owner);

        /// <summary>
        /// Списать сумму. Возвращает результат операции.
        /// </summary>
        CurrencyOpResult Debit(EntityUid owner, int amount);

        /// <summary>
        /// Начислить сумму. Возвращает результат операции.
        /// </summary>
        CurrencyOpResult Credit(EntityUid owner, int amount);

        /// <summary>
        /// Проверить, хватает ли баланса.
        /// </summary>
        bool CanAfford(EntityUid owner, int amount);
    }
}
