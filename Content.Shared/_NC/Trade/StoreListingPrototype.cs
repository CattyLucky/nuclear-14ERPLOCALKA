using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using System.Collections.Generic;

namespace Content.Shared._NC.Trade
{
    [Serializable, NetSerializable]
    [Prototype("ncStoreListing")]
    public sealed class StoreListingPrototype : IPrototype
    {
        [IdDataField]
        public string Id = string.Empty; // Только этот атрибут!

        [DataField("productEntity")]
        public string ProductEntity = string.Empty;

        [DataField("name")]
        public string? Name;

        [DataField("description")]
        public string? Description;

        [DataField("icon")]
        public SpriteSpecifier? Icon;

        [DataField("cost")]
        public Dictionary<string, float> Cost = new();

        [DataField("categories")]
        public List<string> Categories = new();

        [DataField("conditions")]
        public List<ListingConditionPrototype> Conditions = new();

        public string ID => Id;
    }
}
