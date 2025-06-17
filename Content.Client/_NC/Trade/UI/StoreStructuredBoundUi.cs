using Content.Client._NC.Trade.UI.Windows;
using Robust.Shared.Prototypes;
using Content.Shared._NC.Trade.UiDto;

namespace Content.Client._NC.Trade.UI;

public sealed class StoreStructuredBoundUi(EntityUid owner, Enum uiKey, IPrototypeManager proto)
    : BoundUserInterface(owner, uiKey)
{
    private StoreStructuredMenu? _menu;

    protected override void Open()
    {
        _menu = new StoreStructuredMenu();
        _menu.OnClose += Close;
        _menu.OpenCentered();
        base.Open();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_menu == null || state is not StoreUiState s)
            return;

        _menu.SetBalance(s.Balance);
        _menu.PopulateBuyListings(s.Listings, proto);
    }

    public override void Close()
    {
        _menu?.Close();
        _menu = null;
        base.Close();
    }
}
