using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Medical.Surgery.Tools;

[RegisterComponent, NetworkedComponent]
public sealed partial class BoneSetterComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "Костный фиксатор"; // Corvax-Localization
    public bool? Used { get; set; } = null;
    [DataField]
    public float Speed { get; set; } = 1f;
}
