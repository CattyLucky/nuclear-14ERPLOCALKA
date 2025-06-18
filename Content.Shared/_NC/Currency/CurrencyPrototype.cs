using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._NC.Currency
{
    [Prototype("currency")]
    public sealed class CurrencyPrototype : IPrototype
    {
        [IdDataField]
        public string ID { get; private set; } = default!;

        [DataField("entity", required: true)]
        public string Entity { get; private set; } = default!;

        [DataField("icon")]
        public SpriteSpecifier? Icon { get; private set; }

        [DataField("displayName")]
        public string? DisplayName { get; private set; }
    }
}
