namespace Content.Shared._NC.Currency
{
    /// <summary>
    /// Реестр обработчиков валют (ICurrencyHandler).
    /// Доступ к нужному обработчику по id.
    /// </summary>
    public static class CurrencyRegistry
    {
        private static readonly Dictionary<string, ICurrencyHandler> Handlers = new();

        public static void Register(ICurrencyHandler handler)
        {
            Handlers[handler.Id] = handler;
        }

        public static bool TryGet(string id, out ICurrencyHandler? handler)
        {
            return Handlers.TryGetValue(id, out handler);
        }

        public static void Clear()
        {
            Handlers.Clear();
        }
    }
}
