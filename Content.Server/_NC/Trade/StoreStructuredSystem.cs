using System.Linq;
using Content.Shared._NC.Currency;
using Content.Shared._NC.Trade;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._NC.Trade;

/// <summary>
/// Система работы с UI магазина, обновляет состояние для клиента.
/// </summary>
public sealed class StoreStructuredSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly NcStoreLogicSystem _logic = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<NcStoreComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnActivate(EntityUid uid, NcStoreComponent comp, ActivateInWorldEvent args)
    {
        // Открываем интерфейс только если он еще не открыт
        if (!_ui.HasUi(uid, StoreUiKey.Key))
            return;

        if (!_ui.IsUiOpen(uid, StoreUiKey.Key, args.User))
            _ui.OpenUi(uid, StoreUiKey.Key, args.User);

        UpdateUiState(uid, comp, args.User);
    }

    /// <summary>
    /// Пытается получить иконку из компонента Sprite прототипа сущности (EntityPrototype).
    /// </summary>
    private SpriteSpecifier? GetEntityPrototypeIcon(string protoId)
    {
        if (!_protos.TryIndex<EntityPrototype>(protoId, out var proto) || proto == null)
            return null;

        foreach (var comp in proto.Components)
        {
            // Найдём первый компонент с RsiPath и Layers
            var compType = comp.GetType();
            var rsiProp = compType.GetProperty("RsiPath");
            var layersProp = compType.GetProperty("Layers");

            if (rsiProp != null && layersProp != null)
            {
                var rsiPath = rsiProp.GetValue(comp) as ResPath?;
                var layers = layersProp.GetValue(comp) as IEnumerable<object>;
                var state = "base";

                if (layers != null)
                {
                    var firstLayer = layers.Cast<dynamic>().FirstOrDefault();
                    if (firstLayer != null)
                    {
                        var layerState = firstLayer?.State as string;
                        if (!string.IsNullOrEmpty(layerState))
                            state = layerState;
                    }
                }

                if (rsiPath != null)
                    return new SpriteSpecifier.Rsi(rsiPath.Value, state);
            }
        }
        return null;
    }



    /// <summary>
    /// Обновляет состояние UI магазина (список товаров и баланс валюты).
    /// </summary>
    public void UpdateUiState(EntityUid uid, NcStoreComponent comp, EntityUid user)
    {
        // Валюта магазина (первая из списка)
        var currencyId = comp.CurrencyWhitelist.FirstOrDefault() ?? string.Empty;

        // Получаем баланс пользователя
        var balance = 0;
        if (!string.IsNullOrEmpty(currencyId) && CurrencyRegistry.TryGet(currencyId, out var handler) && handler != null)
            balance = handler.GetBalance(user);

        // Формируем список товаров для UI
        var data = comp.Listings
            .Where(x => !string.IsNullOrEmpty(x.Name))
            .Select(x =>
            {
                var cost = x.Cost.Count > 0 ? x.Cost.First().Value : 0f;

                // Подбираем иконку: сначала свою, затем из прототипа сущности, затем fallback
                SpriteSpecifier icon = x.Icon
                    ?? (!string.IsNullOrEmpty(x.ProductEntity) ? GetEntityPrototypeIcon(x.ProductEntity) : null)
                    ?? new SpriteSpecifier.Rsi(
                        new ResPath("/Textures/_Nuclear14/Objects/Misc/currency.rsi"),
                        "caps"
                    );

                // Категории: если нет — "Разное"
                var categories = (x.Categories?.Any() ?? false)
                    ? x.Categories.ToList()
                    : new List<string> { "Разное" };

                // Режим (покупка/продажа)
                var mode = cost < 0 ? StoreMode.Sell : StoreMode.Buy;

                return new StoreListingData(
                    x.Id,
                    x.Name!,
                    x.Description ?? string.Empty,
                    icon,
                    (int)cost,
                    categories,
                    mode,
                    currencyId
                );
            })

            .ToList();
        _ui.SetUiState(uid, StoreUiKey.Key, new StoreUiState(balance, data));
    }
}
