namespace Content.Shared._NC.Currency;

public static class CurrencyRegistry
{
    private static readonly Dictionary<string, ICurrencyHandler> Map = new();

    public static void Register(ICurrencyHandler handler) => Map[handler.Id] = handler;
    public static bool TryGet(string id, out ICurrencyHandler? h) => Map.TryGetValue(id, out h);
}
