using Content.Client._NC.Trade.UI.Windows;
using Content.Shared._NC.Trade.UiDto;
using Robust.Shared.Prototypes;
using Content.Shared._NC.Trade.Messages;
using Content.Shared.Hands.Components;
using Robust.Client.Player;


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
        _menu.OnSellClicked += id =>
        {
            var playerManager = IoCManager.Resolve<IPlayerManager>();
            var entityManager = IoCManager.Resolve<IEntityManager>();
            var localPlayer = playerManager.LocalPlayer;

            if (localPlayer == null || localPlayer.ControlledEntity is not { } player)
                return;

            if (!entityManager.TryGetComponent(player, out HandsComponent? hands))
                return;

            var itemUid = hands.ActiveHand?.HeldEntity ?? EntityUid.Invalid;
            if (itemUid == EntityUid.Invalid)
            {
                // Покажи ошибку: "Нет предмета в руке для продажи!"
                return;
            }

            SendMessage(new StoreSellListingMessage(id, itemUid));
        };

        _menu.OnExchangeClicked += id =>
        {
            var playerManager = IoCManager.Resolve<IPlayerManager>();
            var entityManager = IoCManager.Resolve<IEntityManager>();
            var localPlayer = playerManager.LocalPlayer;

            if (localPlayer == null || localPlayer.ControlledEntity is not { } player)
                return;

            if (!entityManager.TryGetComponent(player, out HandsComponent? hands))
                return;

            var itemUid = hands.ActiveHand?.HeldEntity ?? EntityUid.Invalid;
            if (itemUid == EntityUid.Invalid)
            {
                // Можно показать ошибку пользователю
                return;
            }

            SendMessage(new StoreExchangeListingMessage(id, itemUid));
        };


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
