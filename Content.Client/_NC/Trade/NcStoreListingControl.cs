using Content.Client.Stylesheets;
using Content.Shared._NC.Trade;
using Content.Shared.Stacks;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;


namespace Content.Client._NC.Trade;


public sealed class NcStoreListingControl : PanelContainer
{
    private const int SlotPx = 96;
    private const int PriceW = 96;
    private const int PriceH = 32;
    private const int TextMax = 420;

    public NcStoreListingControl(StoreListingData data, SpriteSystem sprites)
    {
        Margin = new(6, 6, 6, 6);
        HorizontalExpand = true;

        // Карточка
        var card = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = new(0.08f, 0.08f, 0.09f, 0.9f),
                BorderColor = Color.FromHex("#B08D3B"),
                BorderThickness = new(1),
                PaddingLeft = 10,
                PaddingRight = 10,
                PaddingTop = 8,
                PaddingBottom = 8
            }
        };
        AddChild(card);

        var mainCol = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true
        };
        card.AddChild(mainCol);

        // Заголовок
        var pm = IoCManager.Resolve<IPrototypeManager>();
        pm.TryIndex<EntityPrototype>(data.ProductEntity, out var proto);
        var name = (proto?.Name ?? data.ProductEntity).ToUpperInvariant();

        var nameLbl = new Label
        {
            Text = name,
            HorizontalExpand = true,
            ClipText = true
        };
        nameLbl.StyleClasses.Add(StyleNano.StyleClassLabelHeading);
        mainCol.AddChild(nameLbl);

        mainCol.AddChild(new PanelContainer { StyleClasses = { StyleNano.ClassLowDivider, }, });

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true
        };
        mainCol.AddChild(row);

        if (MakeSlot(data, sprites) is { } slot)
            row.AddChild(slot);

        row.AddChild(MakeDescription(proto));

        var actionCol = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = false,
            MinSize = new Vector2i(PriceW, PriceH)
        };

        if (data.Remaining != 0)
            actionCol.AddChild(MakePriceButton(data));
        else
        {
            actionCol.AddChild(
                new Label
                {
                    Text = data.Mode == StoreMode.Buy
                        ? "Нет в наличии"
                        : "Закупка завершена",
                    HorizontalAlignment = HAlignment.Center,
                    Modulate = Color.FromHex("#C0C0C0"),
                    Margin = new(0, 8, 0, 0)
                });
        }

        var remainingLbl = new Label
        {
            Text = data.Mode == StoreMode.Buy
                ? $"Осталось: {(data.Remaining < 0 ? "∞" : data.Remaining)}"
                : $"Скупим: {(data.Remaining < 0 ? "∞" : data.Remaining)}",
            HorizontalAlignment = HAlignment.Center,
            Modulate = Color.FromHex("#C0C0C0"),
            Margin = new(0, 2, 0, 0)
        };
        actionCol.AddChild(remainingLbl);

        var ownedLbl = new Label
        {
            Text = $"У вас: {data.Owned}",
            HorizontalAlignment = HAlignment.Center,
            Modulate = Color.FromHex("#C0C0C0"),
            Margin = new(0, 2, 0, 0)
        };
        actionCol.AddChild(ownedLbl);

        row.AddChild(actionCol);
    }

    public event Action? OnBuyPressed;
    public event Action? OnSellPressed;
    public event Action? OnExchangePressed;

    private static Texture? TryGetCurrencyIcon(string currencyId, SpriteSystem sprites)
    {
        var pm = IoCManager.Resolve<IPrototypeManager>();
        if (!pm.TryIndex<StackPrototype>(currencyId, out var stack))
            return null;
        if (!pm.TryIndex<EntityPrototype>(stack.Spawn, out var ent))
            return null;
        return sprites.GetPrototypeIcon(ent.ID).Default;
    }

    private Control? MakeSlot(StoreListingData data, SpriteSystem sprites)
    {
        var pm = IoCManager.Resolve<IPrototypeManager>();
        if (!pm.TryIndex<EntityPrototype>(data.ProductEntity, out var proto))
            return null;
        if (sprites.GetPrototypeIcon(proto.ID).Default is not { } tex)
            return null;

        var slot = new PanelContainer
        {
            StyleClasses = { StyleNano.StyleClassInventorySlotBackground, },
            MinSize = new Vector2i(SlotPx, SlotPx)
        };
        slot.AddChild(
            new TextureRect
            {
                Texture = tex,
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                Margin = new(2)
            });
        return slot;
    }

    private Control MakeDescription(EntityPrototype? proto)
    {
        var desc = proto?.Description ?? string.Empty;
        var r = new RichTextLabel
        {
            MaxWidth = TextMax,
            HorizontalExpand = true
        };
        r.SetMessage(desc);
        return r;
    }

    private Control MakePriceButton(StoreListingData data)
    {
        var color = data.Mode switch
        {
            StoreMode.Buy => Color.FromHex("#4CAF50"),
            StoreMode.Sell => Color.FromHex("#D9534F"),
            StoreMode.Exchange => Color.FromHex("#388EE5"),
            _ => Color.Gray
        };

        var btn = new Button
        {
            Text = string.Empty,
            MinSize = new Vector2i(PriceW, PriceH),
            MaxSize = new Vector2i(PriceW, PriceH),
            ClipText = true,
            Margin = new(8, 0, 0, 0),
            StyleClasses = { StyleNano.StyleClassButtonBig, },
            Disabled = data.Remaining == 0
        };
        btn.StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = color,
            BorderColor = Color.Black,
            BorderThickness = new(1)
        };

        var inner = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            HorizontalExpand = true,
            VerticalExpand = true
        };

        if (!string.IsNullOrEmpty(data.CurrencyId))
        {
            // Иконка валюты
            if (TryGetCurrencyIcon(
                    data.CurrencyId,
                    IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SpriteSystem>()) is { } tex)
            {
                inner.AddChild(
                    new TextureRect
                    {
                        Texture = tex,
                        Stretch = TextureRect.StretchMode.KeepAspectCentered,
                        MinSize = new Vector2i(PriceH - 6, PriceH - 6),
                        MaxSize = new Vector2i(PriceH - 6, PriceH - 6),
                        Margin = new(2, 2, 0, 2)
                    });
            }
        }

        inner.AddChild(
            new Label
            {
                Text = data.Price.ToString(),
                HorizontalExpand = true,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center
            });

        btn.AddChild(inner);

        btn.OnPressed += _ =>
        {
            switch (data.Mode)
            {
                case StoreMode.Buy:
                    OnBuyPressed?.Invoke();
                    break;
                case StoreMode.Sell:
                    OnSellPressed?.Invoke();
                    break;
                case StoreMode.Exchange:
                    OnExchangePressed?.Invoke();
                    break;
            }
        };

        return btn;
    }
}
