using System.Linq;
using Content.Shared._NC.Trade;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;

namespace Content.Server._NC.Trade;

public sealed class StoreStructuredSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly NcStoreLogicSystem _logic = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<NcStoreComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<NcStoreComponent, RequestUiRefreshMessage>(OnUiRefreshRequest);
    }

    private void OnActivate(EntityUid uid, NcStoreComponent comp, ActivateInWorldEvent args)
    {
        if (!_ui.HasUi(uid, StoreUiKey.Key))
            return;

        if (!_ui.IsUiOpen(uid, StoreUiKey.Key, args.User))
            _ui.OpenUi(uid, StoreUiKey.Key, args.User);
        comp.CurrentUser = args.User;
        UpdateUiState(uid, comp, args.User);
    }
    private void OnUiRefreshRequest(EntityUid uid,
        NcStoreComponent comp,
        RequestUiRefreshMessage msg)
    {
        if (comp.CurrentUser != null)
            UpdateUiState(uid, comp, comp.CurrentUser.Value);
    }

    public void UpdateUiState(EntityUid uid, NcStoreComponent comp, EntityUid user)
    {
        // Валюта магазина (первая из списка)
        var currencyProtoId = comp.CurrencyWhitelist.FirstOrDefault() ?? string.Empty;

        // Получаем баланс пользователя — теперь через логику магазина (работает через стаки/предметы)
        var balance = 0;
        if (!string.IsNullOrEmpty(currencyProtoId))
            balance = _logic.GetBalance(user, currencyProtoId);

        // Формируем список товаров для UI (только protoId, цена, категория и валюта)
        var data = comp.Listings
            .Where(x => !string.IsNullOrEmpty(x.ProductEntity))
            .Select(x =>
            {
                var cost = x.Cost.Count > 0 ? x.Cost.First().Value : 0f;

                var categories = (x.Categories?.Any() ?? false)
                    ? x.Categories.ToList()
                    : new List<string> { "Разное" };

                var category = categories.FirstOrDefault() ?? "Разное";
                var mode = x.Mode;

                return new StoreListingData(
                    x.Id,
                    x.ProductEntity, // protoId
                    (int)cost,
                    category,
                    currencyProtoId,
                    mode
                );
            })
            .ToList();

        _ui.SetUiState(uid, StoreUiKey.Key, new StoreUiState(balance, data));
    }
}
