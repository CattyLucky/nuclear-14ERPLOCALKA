using Content.Shared._NC.Trade;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Map;

namespace Content.Server._NC.Trade;

/// <summary>
/// Система загрузки структурированного магазина из пресета (YAML-прототипа).
/// Наполняет компонент NcStoreComponent при инициализации карты (MapInitEvent).
/// </summary>
public sealed class StoreSystemStructuredLoader : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    private static readonly ISawmill Sawmill = Logger.GetSawmill("ncstore-loader");

    public override void Initialize() =>
        // Важно: используем MapInitEvent — гарантированная полная инициализация!
        SubscribeLocalEvent<NcStoreComponent, MapInitEvent>(OnMapInit);

    private void OnMapInit(EntityUid uid, NcStoreComponent comp, MapInitEvent args)
    {
        if (string.IsNullOrWhiteSpace(comp.Preset))
        {
            Sawmill.Warning($"[NcStore] Нет указанного пресета у магазина {ToPrettyString(uid)}");
            return;
        }

        if (!_prototypes.TryIndex<StorePresetStructuredPrototype>(comp.Preset, out var preset))
        {
            Sawmill.Error($"[NcStore] Пресет '{comp.Preset}' не найден для магазина {ToPrettyString(uid)}");
            return;
        }

        Sawmill.Info($"[NcStore] Загружается пресет '{comp.Preset}' для {ToPrettyString(uid)}");

        comp.CurrencyWhitelist.Clear();
        comp.CurrencyWhitelist.Add(preset.Currency);

        comp.Categories.Clear();
        comp.Listings.Clear();

        int count = 0;
        foreach (var (mode, categories) in preset.Catalog)
        {
            foreach (var (category, entries) in categories)
            {
                if (!comp.Categories.Contains(category))
                    comp.Categories.Add(category);

                foreach (var entry in entries)
                {
                    var id = $"{mode}_{category}_{entry.Proto}_{_random.Next(100000)}";
                    var listing = new StoreListingPrototype
                    {
                        Id = id,
                        ProductEntity = entry.Proto,
                        Name = entry.Name,
                        Description = entry.Description,
                        Icon = entry.Icon,
                        Cost = new() { [preset.Currency] = entry.Price, },
                        Categories = [category],
                        Conditions = new()
                    };

                    comp.Listings.Add(listing);
                    count++;
                }
            }
        }

        Sawmill.Info($"[NcStore] Всего товаров: {count} для {ToPrettyString(uid)}");
    }
}
