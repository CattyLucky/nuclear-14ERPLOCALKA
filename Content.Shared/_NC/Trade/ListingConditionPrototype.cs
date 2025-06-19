using Robust.Shared.Serialization;

namespace Content.Shared._NC.Trade;

/// <summary>
/// Прототип условия для магазина/обмена.
/// </summary>
[Serializable, NetSerializable,]
public sealed partial class ListingConditionPrototype
{
    [DataField("condition")]
    public object? Condition;
}
