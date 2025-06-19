using System.Linq;
using Content.Shared._NC.Currency;
using Content.Shared._NC.Trade;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Shared.Utility;

namespace Content.Server._NC.Trade;

/// <summary>
/// Система работы с UI магазина, обновляет состояние для клиента.
/// </summary>
public sealed class StoreStructuredSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly NcStoreLogicSystem _logic = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<NcStoreComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnActivate(EntityUid uid, NcStoreComponent comp, ActivateInWorldEvent args)
    {
        // Используем Enum как ключ
        if (!_ui.HasUi(uid, StoreUiKey.Key))
            return;

        if (!_ui.IsUiOpen(uid, StoreUiKey.Key, args.User))
            _ui.OpenUi(uid, StoreUiKey.Key, args.User);

        UpdateUiState(uid, comp, args.User);
    }

    public void UpdateUiState(EntityUid uid, NcStoreComponent comp, EntityUid user)
    {
        // Получаем валюту
        var currencyId = comp.CurrencyWhitelist.FirstOrDefault() ?? string.Empty;
        var balance = 0;

        if (!string.IsNullOrEmpty(currencyId) && CurrencyRegistry.TryGet(currencyId, out var handler) && handler != null)
            balance = handler.GetBalance(user);

        // Формируем список товаров
        var data = comp.Listings
            .Where(x => !string.IsNullOrEmpty(x.Name))
            .Select(x =>
            {
                var cost = x.Cost.Count > 0 ? x.Cost.First().Value : 0;
                var icon = x.Icon ?? new SpriteSpecifier.Rsi(new("/Textures/_Nuclear14/Objects/Misc/currency.rsi"), "caps");
                var categories = (x.Categories?.Any() ?? false)
                    ? x.Categories.ToList()
                    : new List<string> { "Разное" };
                var mode = cost < 0 ? StoreMode.Sell : StoreMode.Buy;

                return new StoreListingData(
                    x.Id,
                    x.Name!,
                    x.Description ?? string.Empty,
                    icon,
                    (int)cost,
                    categories, // <-- теперь это список!
                    mode,
                    currencyId
                );
            })

            .ToList();

        // Обновляем UI
        _ui.SetUiState(uid, StoreUiKey.Key, new StoreUiState(balance, data));
    }
}
