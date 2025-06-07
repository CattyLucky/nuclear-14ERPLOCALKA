using Content.Shared._NC.Trader;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Client.ResourceManagement;
using Robust.Shared.Utility;
using Robust.Client.Player;
using Content.Client.UserInterface.Controls;
using Content.Shared.Administration;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Hands.Components;
using System.Linq;


namespace Content.Client._NC.Trade;

public sealed class TraderMenu : DefaultWindow
{
    public event Action<string>? OnClickItem;

    private readonly IResourceCache _res;
    private readonly Dictionary<string, TraderListingData> _listings = new();
    private string _selectedCategory = string.Empty;
    private string _currencyAccepted = "CapCoin";
    private const string DefaultIconPath = "/Textures/Interface/Nano/item-default.png";
    private const string DefaultCurrencyIconPath = "/Textures/Interface/Nano/currency-default.png";

    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    public TraderMenu(IResourceCache res)
    {
        _res = res;
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);
        MinSize = new Vector2i(600, 500);
        Title = Loc.GetString("trader-title");
        StyleClasses.Add("window-border-thick");
    }

    public void UpdateListings(Dictionary<string, TraderListingData> listings, int balance, string currencyAccepted)
    {
        _currencyAccepted = currencyAccepted;
        _listings.Clear();
        foreach (var (id, data) in listings)
            _listings[id] = data;

        var balanceLabel = this.GetChild<Label>("BalanceLabel");
        var balanceIcon = this.GetChild<TextureRect>("BalanceIcon");

        balanceLabel.Text = Loc.GetString("trader-balance", ("amount", balance));

        if (!_res.TryGetResource(new ResPath($"/Textures/Objects/Economy/currency_{currencyAccepted.ToLower()}.png"), out TextureResource? currencyTexture))
            currencyTexture = _res.GetResource<TextureResource>(new ResPath(DefaultCurrencyIconPath));

        balanceIcon.Texture = currencyTexture.Texture;

        var categoryContainer = this.GetChild<BoxContainer>("CategoryContainer");
        categoryContainer.DisposeAllChildren();

        var categories = _listings.Values.Select(l => l.Category).Distinct().OrderBy(c => c).ToList();

        if (_selectedCategory == string.Empty || !categories.Contains(_selectedCategory))
            _selectedCategory = categories.FirstOrDefault() ?? "";

        foreach (var cat in categories)
        {
            var btn = new Button
            {
                Text = cat,
                ToggleMode = true,
                Pressed = cat == _selectedCategory,
                Margin = new Thickness(0, 0, 5, 5),
                HorizontalExpand = true
            };
            btn.OnPressed += _ => SwitchCategory(cat);
            categoryContainer.AddChild(btn);
        }

        SwitchCategory(_selectedCategory);
    }

    private void SwitchCategory(string cat)
    {
        _selectedCategory = cat;
        var listingsContainer = this.GetChild<BoxContainer>("ListingsContainer");
        listingsContainer.DisposeAllChildren();

        var player = _player.LocalPlayer?.ControlledEntity;

        foreach (var listing in _listings.Values.Where(l => l.Category == cat))
        {
            var texturePath = listing.Icon ?? DefaultIconPath;
            if (!_res.TryGetResource(new ResPath(texturePath), out TextureResource? textureRes))
                textureRes = _res.GetResource<TextureResource>(new ResPath(DefaultIconPath));

            int count = 0;
            if (listing.Category == TraderCategory.Sell && player != null)
            {
                var protoId = listing.ProtoId;
                count = CountAllMatchingItems(player.Value, protoId);
            }

            var control = new TraderListingControl(listing, textureRes.Texture, _currencyAccepted)
            {
                Margin = new Thickness(0, 0, 0, 6)
            };

            if (listing.Category == TraderCategory.Sell)
            {
                if (count == 0)
                {
                    control.ToolTip = Loc.GetString("trader-no-items-available");
                    control.Modulate = Color.Gray;
                    control.StoreItemBuyButton.Disabled = true;
                    control.SetCountSuffix(0);
                }
                else
                {
                    control.SetCountSuffix(count);
                }
            }

            control.OnClick += () => HandleClick(listing);
            listingsContainer.AddChild(control);
        }
    }

    private void HandleClick(TraderListingData listing)
    {
        var player = _player.LocalPlayer?.ControlledEntity;
        if (player == null)
            return;

        var protoId = listing.ProtoId;
        var max = listing.Category == TraderCategory.Sell ? CountAllMatchingItems(player.Value, protoId) : 999;

        if (listing.Category == TraderCategory.Sell && max <= 0)
            return;

        var title = listing.Category == TraderCategory.Sell
            ? Loc.GetString("trader-sell")
            : Loc.GetString("trader-buy");

        var prompt = listing.Category == TraderCategory.Sell
            ? Loc.GetString("trader-sell-prompt", ("item", listing.Name), ("max", max))
            : Loc.GetString("trader-buy-prompt", ("item", listing.Name), ("max", max));

        ShowAmountDialog(title, prompt, max, amount =>
        {
            OnClickItem?.Invoke($"{listing.Id}|{amount}");
        });
    }

    private void ShowAmountDialog(string title, string prompt, int max, Action<int> onConfirm)
    {
        var field = "amount";
        var entry = new QuickDialogEntry(field, QuickDialogEntryType.Integer, prompt);
        var dialog = new DialogWindow(title, new List<QuickDialogEntry> { entry });

        dialog.OnConfirmed += responses =>
        {
            if (responses.TryGetValue(field, out var text) &&
                int.TryParse(text.Trim(), out var amount) &&
                amount > 0 && amount <= max)
            {
                var confirm = new ConfirmDialog(
                    Loc.GetString("trader-confirm-title"),
                    Loc.GetString("trader-confirm-message", ("name", title), ("amount", amount))
                );

                confirm.OnConfirmed += () => onConfirm(amount);
                confirm.OpenCentered();
            }
        };

        dialog.OpenCentered();
    }

    private int CountAllMatchingItems(EntityUid uid, string protoId)
    {
        var count = 0;

        if (_entMan.TryGetComponent(uid, out HandsComponent? hands))
        {
            foreach (var ent in _hands.EnumerateHeld(uid, hands))
            {
                if (_entMan.TryGetComponent(ent, out MetaDataComponent? meta) &&
                    meta.EntityPrototype?.ID == protoId)
                {
                    count++;
                }
            }
        }

        if (_entMan.TryGetComponent(uid, out InventoryComponent? inv))
        {
            foreach (var slot in _inventory.EnumerateAllSlots(uid, inv))
            {
                if (!_inventory.TryGetSlotEntity(uid, slot.Id, out var item))
                    continue;

                if (_entMan.TryGetComponent(item.Value, out MetaDataComponent? meta) &&
                    meta.EntityPrototype?.ID == protoId)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
