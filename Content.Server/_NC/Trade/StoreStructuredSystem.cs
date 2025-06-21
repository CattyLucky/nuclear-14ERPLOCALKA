using System.Linq;
using Content.Server.Popups;
using Content.Shared._NC.Trade;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._NC.Trade;

/// <summary>
/// Управляет взаимодействием игрока с торговым автоматом:
///   • ограничивает одного пользователя;
///   • обновляет UI;
///   • закрывает UI, если игрок ушёл далеко или закрыл окно.
/// </summary>
public sealed class StoreStructuredSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui  = default!;
    [Dependency] private readonly NcStoreLogicSystem _logic = default!;
    [Dependency] private readonly PopupSystem _popups       = default!;
    [Dependency] private readonly IGameTiming _timing       = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private const float AutoCloseDistance = 3f;   // метры
    private const float CheckInterval     = 0.5f; // сек

    private TimeSpan _nextCheck = TimeSpan.Zero;

    public override void Initialize()
    {
        SubscribeLocalEvent<NcStoreComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<NcStoreComponent, BoundUIClosedEvent>(OnUiClosed);
        SubscribeLocalEvent<NcStoreComponent, RequestUiRefreshMessage>(OnUiRefreshRequest);
    }

    /* ───────────────────────── Активация ───────────────────────── */

    private void OnActivate(EntityUid uid, NcStoreComponent comp, ActivateInWorldEvent ev)
    {
        // Если автомат уже кем-то занят ↴
        if (comp.CurrentUser != null && comp.CurrentUser != ev.User)
        {
            _popups.PopupEntity(Loc.GetString("ncstore-busy"), uid, ev.User);
            return;
        }

        if (!_ui.HasUi(uid, StoreUiKey.Key))
            return;

        comp.CurrentUser = ev.User; // фиксируем владельца сессии

        if (!_ui.IsUiOpen(uid, StoreUiKey.Key, ev.User))
            _ui.OpenUi(uid, StoreUiKey.Key, ev.User);

        UpdateUiState(uid, comp, ev.User);
    }

    /* ───────────────────────── UI события ───────────────────────── */

    private void OnUiClosed(EntityUid uid, NcStoreComponent comp, BoundUIClosedEvent ev)
    {
        if (ev.UiKey.Equals(StoreUiKey.Key))
            comp.CurrentUser = null; // освобождаем автомат
    }

    private void OnUiRefreshRequest(EntityUid uid, NcStoreComponent comp, RequestUiRefreshMessage msg)
    {
        if (comp.CurrentUser != null)
            UpdateUiState(uid, comp, comp.CurrentUser.Value);
    }

    /* ───────────────────────── Логика UI ───────────────────────── */

    public void UpdateUiState(EntityUid uid, NcStoreComponent comp, EntityUid user)
    {
        var currencyProtoId = comp.CurrencyWhitelist.FirstOrDefault() ?? string.Empty;
        var balance         = string.IsNullOrEmpty(currencyProtoId) ? 0 : _logic.GetBalance(user, currencyProtoId);

        var listings = comp.Listings
            .Where(l => !string.IsNullOrEmpty(l.ProductEntity))
            .Select(l =>
            {
                var price = l.Cost.Count > 0 ? l.Cost.First().Value : 0f;

                // Categories не null: проверяем Count
                var cat = l.Categories.Count > 0 ? l.Categories[0] : "Разное";

                return new StoreListingData(
                    l.Id,
                    l.ProductEntity,
                    (int)price,
                    cat,
                    currencyProtoId,
                    l.Mode);
            })
            .ToList();

        _ui.SetUiState(uid, StoreUiKey.Key, new StoreUiState(balance, listings));
    }


    /* ───────────────────────── Tick для автозакрытия ───────────────────────── */



    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + TimeSpan.FromSeconds(CheckInterval);

        var iter = EntityQueryEnumerator<NcStoreComponent, TransformComponent>();
        while (iter.MoveNext(out var uid, out var store, out var xform))
        {
            // автомат свободен
            if (store.CurrentUser is not { } userUid)
                continue;

            // игрок пропал / вышел
            if (!EntityManager.TryGetComponent(userUid, out TransformComponent? userXform))
            {
                store.CurrentUser = null;
                continue;
            }

            // дистанция > AutoCloseDistance → закрываем
            if (!_xform.InRange(xform.Coordinates, userXform.Coordinates, AutoCloseDistance))
            {
                _ui.CloseUi(uid, StoreUiKey.Key, userUid);
                store.CurrentUser = null;
            }
        }
    }
}
