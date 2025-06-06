using Content.Shared._NC.Trader;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Client.ResourceManagement;
using Robust.Client.Graphics;
using System.Linq;
using Robust.Shared.Utility;

namespace Content.Client._NC.Trade;

public sealed class TraderMenu : DefaultWindow
{
    public event Action<string>? OnClickItem;

    private readonly IResourceCache _res;
    private readonly Dictionary<string, TraderListingData> _listings = new();
    private string _selectedCategory = string.Empty;

    public TraderMenu(IResourceCache res)
    {
        _res = res;
        RobustXamlLoader.Load(this);
    }

    public void UpdateListings(Dictionary<string, TraderListingData> listings, int balance)
    {
        _listings.Clear();
        foreach (var (id, data) in listings)
            _listings[id] = data;

        this.GetChild<Label>("BalanceLabel").Text = $"Баланс: {balance}";
        var categoryContainer = this.GetChild<BoxContainer>("CategoryContainer");

        categoryContainer.DisposeAllChildren();

        var categories = _listings.Values
            .Select(l => l.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        if (_selectedCategory == string.Empty || !categories.Contains(_selectedCategory))
            _selectedCategory = categories.FirstOrDefault() ?? "";

        foreach (var cat in categories)
        {
            var btn = new Button
            {
                Text = cat,
                ToggleMode = true,
                Pressed = cat == _selectedCategory
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

        foreach (var listing in _listings.Values.Where(l => l.Category == cat))
        {
            var texturePath = listing.Icon ?? "/Textures/Interface/Nano/item-default.png";
            var texture = (Texture)_res.GetResource(typeof(Texture), new ResPath(texturePath));

            var control = new TraderListingControl(listing, texture);
            control.OnClick += () => OnClickItem?.Invoke(listing.Id);
            listingsContainer.AddChild(control);
        }
    }
}
