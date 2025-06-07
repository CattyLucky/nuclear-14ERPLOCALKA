using Content.Shared._NC.Trader;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;

namespace Content.Client._NC.Trade;

public sealed class TraderBoundUserInterface : BoundUserInterface
{
    private TraderMenu? _menu;

    [Dependency] private readonly IPlayerManager _playerManager = null!;
    [Dependency] private readonly IResourceCache _res = null!;

    public TraderBoundUserInterface(EntityUid owner, Enum key) : base(owner, key)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _menu = new TraderMenu(_res);
        _menu.OnClickItem += data =>
        {
            var split = data.Split('|');
            var id = split[0];
            var amount = split.Length > 1 && int.TryParse(split[1], out var amt) ? amt : 1;

            SendMessage(new BuyItemMessage(id, amount));
        };

        _menu.OnClose += Close;
        _menu.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_menu == null || state is not TraderUpdateState s)
            return;

        _menu.UpdateListings(s.Inventory, s.Balance, s.CurrencyAccepted);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _menu?.Close();

        _menu = null;
        base.Dispose(disposing);
    }
}
