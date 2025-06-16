using Robust.Shared.Prototypes;

namespace Content.Shared._NC.Trade.Conditions;

[Prototype("NcListingCondition")]
public abstract partial class ListingConditionPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = string.Empty;

    public virtual bool CanList(EntityUid store, EntityUid user) => true;

    public virtual bool CanBuy(EntityUid store, EntityUid user) => true;

    public virtual void Apply(EntityUid store, EntityUid user) { }
}
