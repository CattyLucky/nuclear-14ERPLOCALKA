using Robust.Shared.Prototypes;

namespace Content.Shared._NC.Trade.Conditions;

/// <summary>
/// Shared-описание условия: требует наличия определённого предмета (ID прототипа).
/// Серверная логика проверки и удаления предмета реализуется отдельно.
/// </summary>
[Prototype("NcListingCondition")]
public sealed partial class SellConsumesItemCondition : ListingConditionPrototype
{
    [DataField("requiredItem", required: true)]
    public string RequiredItem = string.Empty;
}
