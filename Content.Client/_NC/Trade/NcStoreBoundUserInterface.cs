using Content.Shared._NC.Trade;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;


namespace Content.Client._NC.Trade
{
    public sealed class NcStoreStructuredBoundUi(EntityUid owner, Enum uiKey)
        : BoundUserInterface(owner, uiKey)
    {
        private NcStoreMenu? _menu;
        private List<StoreListingData> _cachedListings = new();
        private IGameTiming Timing => IoCManager.Resolve<IGameTiming>();
        private TimeSpan _nextRefreshTime = TimeSpan.Zero;
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

        public override void Update()
        {
            base.Update();

            if (_menu == null || !_menu.Visible)
                return;

            var now = Timing.CurTime;
            if (now >= _nextRefreshTime)
            {
                _nextRefreshTime = now + RefreshInterval;
                SendMessage(new RequestUiRefreshMessage());
            }
        }

        protected override void Open()
        {
            base.Open();

            _menu = this.CreateWindow<NcStoreMenu>();
            _menu.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;

            _menu.OnBuyPressed += OnBuy;
            _menu.OnSellPressed += OnSell;
            _menu.OnExchangePressed += OnExchange;
            _menu.OnSearchChanged += _ => { };
            _menu.OnBuyCategoryChanged += _ => { };
            _menu.OnSellCategoryChanged += _ => { };

            _menu.Populate(_cachedListings);
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (state is not StoreUiState newState)
                return;

            _cachedListings = newState.Listings;
            _menu?.Populate(_cachedListings);
            _menu?.SetBalance(newState.Balance);
        }

        private void OnBuy(StoreListingData data) =>
            SendMessage(new StoreBuyListingBoundUiMessage(data.Id, unchecked((uint)Owner.Id)));

        private void OnSell(StoreListingData data) =>
            SendMessage(new StoreSellListingBoundUiMessage(data.Id, unchecked((uint)Owner.Id)));

        private void OnExchange(StoreListingData data) =>
            SendMessage(
                new StoreExchangeListingBoundUiMessage(
                StoreExchangeType.CurrencyToItem,
                data.CurrencyId,
                null,
                data.Price,
                null,
                data.Id,
                1.0f,
                unchecked((uint)Owner.Id),
                data.Id));

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (_menu == null)
                return;
            _menu.OnBuyPressed -= OnBuy;
            _menu.OnSellPressed -= OnSell;
            _menu.OnExchangePressed -= OnExchange;
            _menu.OnBuyCategoryChanged -= _ => { };
            _menu.OnSellCategoryChanged -= _ => { };
            _menu.Orphan();
            _menu = null;
        }
    }
}

