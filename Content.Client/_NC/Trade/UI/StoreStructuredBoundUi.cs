using Content.Client._NC.Trade.UI.Windows;
using Content.Shared._NC.Trade.UiDto;
using Robust.Shared.Prototypes;
using Content.Shared._NC.Trade.Messages;
namespace Content.Client._NC.Trade.UI;

public sealed class NcStoreStructuredBoundUi(EntityUid owner, Enum uiKey, IPrototypeManager proto)
    : BoundUserInterface(owner, uiKey)
{
    private NcStoreStructuredMenu? _menu;

    protected override void Open()
    {
        _menu = new NcStoreStructuredMenu();
        _menu.OnClose += Close;

        _menu.OnBuyClicked += id => SendMessage(new StoreBuyListingMessage(id));
        _menu.OnSellClicked += id => SendMessage(new StoreSellListingMessage(id));
        _menu.OnExchangeClicked += id => SendMessage(new StoreExchangeListingMessage(id));

        _menu.OpenCentered();
        base.Open();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_menu == null || state is not StoreUiState s)
            return;

        _menu.SetBalance(s.Balance);
        _menu.PopulateBuyListings(s.BuyListings, proto);
        _menu.PopulateSellListings(s.SellListings, proto);
        _menu.PopulateExchangeListings(s.ExchangeListings, proto);
    }

    private new void Close()
    {
        _menu?.Close();
        _menu = null;
        base.Close();
    }
}
