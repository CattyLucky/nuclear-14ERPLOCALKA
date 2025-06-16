using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._NC.Currency;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CurrencyItemComponent : Component
{
    [DataField(required: true,
         customTypeSerializer: typeof(PrototypeIdSerializer<CurrencyPrototype>)),
     AutoNetworkedField] public string Currency = default!;

    [DataField, AutoNetworkedField] public int Amount = 1;
}
