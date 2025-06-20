using Content.Shared._NC.Trade;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._NC.Trade;

public sealed class NcStoreStructuredBoundUi(EntityUid owner, Enum uiKey)
    : BoundUserInterface(owner, uiKey)
{
    private NcStoreMenu? _menu;
    private readonly List<StoreListingData> _cachedListings = new();

    private readonly IGameTiming _timing   = IoCManager.Resolve<IGameTiming>();
    private readonly IPlayerManager _player = IoCManager.Resolve<IPlayerManager>();

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);
    private TimeSpan _nextRefreshTime = TimeSpan.Zero;

    /* ───────────────────────────── helpers ───────────────────────────── */
    private static uint Net(EntityUid uid) => unchecked((uint) uid.Id);
    private EntityUid?  Actor => _player.LocalSession?.AttachedEntity;

    private void RequestRefresh()
    {
        var now = _timing.CurTime;
        if (now < _nextRefreshTime)
            return; // рано

        _nextRefreshTime = now + RefreshInterval;
        SendMessage(new RequestUiRefreshMessage());
    }

    /* ───────────────────────────── UI lifecycle ──────────────────────── */
    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<NcStoreMenu>();
        _menu.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;

        _menu.OnBuyPressed  += OnBuy;
        _menu.OnSellPressed += OnSell;
        _menu.OnExchangePressed += OnExchange;

        _menu.Populate(_cachedListings);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not StoreUiState st)
            return;

        _cachedListings.Clear();
        _cachedListings.AddRange(st.Listings);

        _menu?.Populate(_cachedListings);
        _menu?.SetBalance(st.Balance);
    }

    /* ──────────────────────────── callbacks ──────────────────────────── */
    private void OnBuy(StoreListingData data)
    {
        if (Actor is not { } actor)
            return;

        SendMessage(new StoreBuyListingBoundUiMessage(data.Id, Net(actor)));
        RequestRefresh();
    }

    private void OnSell(StoreListingData data)
    {
        if (Actor is not { } actor)
            return;

        SendMessage(new StoreSellListingBoundUiMessage(data.Id, Net(actor)));
        RequestRefresh();
    }

    private void OnExchange(StoreListingData data)
    {
        if (Actor is not { } actor)
            return;

        SendMessage(new StoreExchangeListingBoundUiMessage(
            StoreExchangeType.CurrencyToItem,
            data.CurrencyId,
            null,
            data.Price,
            null,
            data.Id,
            1.0f,
            Net(actor),
            data.Id));
        RequestRefresh();
    }

    /* ───────────────────────── Dispose ───────────────────────── */
    protected override void Dispose(bool disposing)
    {
        if (_menu != null)
        {
            _menu.OnBuyPressed       -= OnBuy;
            _menu.OnSellPressed      -= OnSell;
            _menu.OnExchangePressed  -= OnExchange;
            _menu.Orphan();
            _menu = null;
        }

        base.Dispose(disposing);
    }
}
