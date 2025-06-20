using Robust.Shared.Serialization;

namespace Content.Shared._NC.Trade
{
    [Serializable, NetSerializable]
    public sealed partial class RequestUiRefreshMessage : BoundUserInterfaceMessage
    {
        // Можно ничего не передавать
    }
}
