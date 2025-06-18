using Robust.Shared.Prototypes;

namespace Content.Shared._NC.Trade.Conditions;

/// <summary>
/// Базовый shared-прототип для условий магазинов.
/// Содержит только контракт/описание, логику реализуют наследники на сервере.
/// </summary>
[Prototype("NcListingCondition")]
public abstract partial class ListingConditionPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// Можно ли показывать этот листинг для игрока (например, если условие на видимость).
    /// </summary>
    public virtual bool CanList(EntityUid store, EntityUid user) => true;

    /// <summary>
    /// Можно ли совершить покупку/обмен (условие доступности, например, нужный предмет, репутация и т.д.).
    /// </summary>
    public virtual bool CanBuy(EntityUid store, EntityUid user) => true;

    /// <summary>
    /// Применить эффект условия (например, удалить предмет, снять деньги и т.д.).
    /// </summary>
    public virtual void Apply(EntityUid store, EntityUid user) { }
}
