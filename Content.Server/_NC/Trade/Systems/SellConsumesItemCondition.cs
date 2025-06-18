using Content.Shared._NC.Currency;
using Content.Shared._NC.Trade.Conditions;


namespace Content.Server._NC.Trade.Systems;

public sealed partial class SellConsumesItemCondition : ListingConditionPrototype
{
    [DataField("requiredItem", required: true)]
    public string RequiredItem = default!; // id прототипа предмета

    public override bool CanBuy(EntityUid store, EntityUid user)
    {
        var ents = IoCManager.Resolve<IEntityManager>();
        foreach (var item in CurrencyHelpers.EnumerateDeepItemsUnique(user, ents))
        {
            if (ents.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID == RequiredItem)
                return true;
        }
        return false;
    }

    public override void Apply(EntityUid store, EntityUid user)
    {
        var ents = IoCManager.Resolve<IEntityManager>();
        foreach (var item in CurrencyHelpers.EnumerateDeepItemsUnique(user, ents))
        {
            if (ents.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID == RequiredItem)
            {
                ents.DeleteEntity(item);
                break;
            }
        }
    }
}
