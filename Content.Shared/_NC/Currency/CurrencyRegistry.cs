using System.Collections.Concurrent;
using System.Linq;
namespace Content.Shared._NC.Currency;

public static class CurrencyRegistry
{
    private static readonly ConcurrentDictionary<string, ICurrencyHandler> Handlers = new();

    private static readonly ISawmill Sawmill = Logger.GetSawmill("currency-registry");

    public static bool Register(ICurrencyHandler handler)
    {
        if (!Handlers.TryAdd(handler.Id, handler))
        {
            Sawmill.Warning($"Handler with id '{handler.Id}' is already registered.");
            return false;
        }

        Sawmill.Debug($"Registered currency handler '{handler.Id}'.");
        return true;
    }

    public static bool Unregister(string id)
    {
        if (!Handlers.TryRemove(id, out _))
        {
            Sawmill.Debug($"Tried to unregister '{id}', but it was not found.");
            return false;
        }

        Sawmill.Debug($"Unregistered currency handler '{id}'.");
        return true;
    }

    public static bool TryGet(string id, out ICurrencyHandler? handler) => Handlers.TryGetValue(id, out handler);

    public static void Clear()
    {
        Handlers.Clear();
        Sawmill.Debug("Cleared all handlers.");
    }

    public static IEnumerable<ICurrencyHandler> GetAllHandlers() => Handlers.Values.ToArray();
}
