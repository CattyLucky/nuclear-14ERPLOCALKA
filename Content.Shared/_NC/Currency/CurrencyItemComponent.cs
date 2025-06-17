using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._NC.Currency
{
    [RegisterComponent, NetworkedComponent]
    public sealed partial class CurrencyItemComponent : Component
    {
        [DataField("currency", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<CurrencyPrototype>))]
        public string Currency = default!;

        [DataField("amount")]
        public int Amount = 1;

        [DataField("stackable")]
        public bool Stackable = false;

    }
}
