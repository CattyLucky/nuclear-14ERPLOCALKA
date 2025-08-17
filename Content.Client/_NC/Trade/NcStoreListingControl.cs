using System.Linq;
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
    private const int QtyMaxDigits = 6;
    private const int MaxTotalDisplay = 999_999;
    private readonly int _maxQty;
    private readonly LineEdit _qtyEdit;

    private Label? _priceLbl;

    private int _qty;

    public NcStoreListingControl(
        StoreListingData data,
        SpriteSystem sprites,
        int balanceHint = int.MaxValue,
        int initialQty = 1
    )
    {
        Margin = new(6, 6, 6, 6);
        HorizontalExpand = true;

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

        var remainingCap = data.Remaining >= 0 ? data.Remaining : int.MaxValue;
        var ownedCap = data.Mode == StoreMode.Sell ? data.Owned : int.MaxValue;
        var moneyCap = data.Mode == StoreMode.Buy && data.Price > 0
            ? balanceHint / data.Price
            : int.MaxValue;

        _maxQty = Math.Min(remainingCap, Math.Min(ownedCap, moneyCap));
        _qty = Math.Clamp(initialQty, MinAllowed, Math.Max(MinAllowed, _maxQty));

        var qtyRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            HorizontalExpand = false
        };

        var minusBtn = new Button { Text = "−", MinSize = new Vector2i(24, 24), };
        var qtyLbl = new Label
        {
            Text = _qty.ToString(),
            MinSize = new Vector2i(28, 24),
            HorizontalAlignment = HAlignment.Center
        };
        var qtyEdit = new LineEdit
        {
            Text = _qty.ToString(),
            MinSize = new Vector2i(40, 24),
            HorizontalExpand = false
        };
        _qtyEdit = qtyEdit;
        var plusBtn = new Button { Text = "+", MinSize = new Vector2i(24, 24), };

        var noQty = _maxQty <= 0;
        minusBtn.Disabled = noQty;
        plusBtn.Disabled = noQty;
        _qtyEdit.Editable = !noQty;

        minusBtn.OnPressed += _ =>
        {
            if (_qty > MinAllowed)
                SetQty(_qty - 1, data, qtyLbl);
        };

        plusBtn.OnPressed += _ =>
        {
            if (_qty < _maxQty)
                SetQty(_qty + 1, data, qtyLbl);
        };

        _qtyEdit.OnTextChanged += _ =>
        {
            var digits = new string(_qtyEdit.Text.Where(char.IsDigit).Take(QtyMaxDigits).ToArray());
            if (digits.Length == 0)
            {
                _qtyEdit.Text = _qty.ToString();
                _qtyEdit.CursorPosition = _qtyEdit.Text.Length;
                return;
            }

            if (!int.TryParse(digits, out var v))
                v = _qty;

            var clamped = Math.Clamp(v, MinAllowed, Math.Max(MinAllowed, _maxQty));
            var newText = clamped.ToString();
            if (_qtyEdit.Text != newText)
            {
                _qtyEdit.Text = newText;
                _qtyEdit.CursorPosition = _qtyEdit.Text.Length;
            }

            SetQty(clamped, data, qtyLbl);
        };

        _qtyEdit.OnTextEntered += _ =>
        {
            if (_maxQty <= 0 || _qty <= 0)
                return;

            switch (data.Mode)
            {
                case StoreMode.Buy:
                    OnBuyPressed?.Invoke(_qty);
                    break;
                case StoreMode.Sell:
                    OnSellPressed?.Invoke(_qty);
                    break;
                case StoreMode.Exchange:
                    OnExchangePressed?.Invoke(_qty);
                    break;
            }
        };

        qtyRow.AddChild(minusBtn);
        qtyRow.AddChild(qtyLbl);
        qtyRow.AddChild(qtyEdit);
        qtyRow.AddChild(plusBtn);
        actionCol.AddChild(qtyRow);

        if (data.Remaining != 0)
        {
            actionCol.AddChild(MakePriceButton(data));
            UpdateTotal(data);
        }
        else
        {
            actionCol.AddChild(
                new Label
                {
                    Text = data.Mode == StoreMode.Buy ? "Нет в наличии" : "Закупка завершена",
                    HorizontalAlignment = HAlignment.Center,
                    Modulate = Color.FromHex("#C0C0C0"),
                    Margin = new(0, 8, 0, 0)
                });
        }

        var showRemaining = data.Remaining >= 0;
        var showOwned = data.Owned > 0;

        if (showRemaining)
        {
            var remainingLbl = new Label
            {
                Text = data.Mode == StoreMode.Buy ? $"Осталось: {data.Remaining}" : $"Скупим: {data.Remaining}",
                HorizontalAlignment = HAlignment.Center,
                Modulate = Color.FromHex("#C0C0C0"),
                Margin = new(0, 2, 0, 0)
            };
            actionCol.AddChild(remainingLbl);
        }

        if (showOwned)
        {
            var ownedLbl = new Label
            {
                Text = $"У вас: {data.Owned}",
                HorizontalAlignment = HAlignment.Center,
                Modulate = Color.FromHex("#C0C0C0"),
                Margin = new(0, 2, 0, 0)
            };
            actionCol.AddChild(ownedLbl);
        }

        row.AddChild(actionCol);
    }

    private int MinAllowed => _maxQty <= 0 ? 0 : 1;

    public event Action<int>? OnBuyPressed;
    public event Action<int>? OnSellPressed;
    public event Action<int>? OnExchangePressed;
    public event Action<int>? OnQtyChanged;

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
                || data.Mode == StoreMode.Sell && data.Owned <= 0
                || _maxQty <= 0
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
            var spriteSys = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SpriteSystem>();
            if (TryGetCurrencyIcon(data.CurrencyId, spriteSys) is { } tex)
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

        _priceLbl = new()
        {
            Text = data.Price.ToString(),
            HorizontalExpand = true,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center
        };
        inner.AddChild(_priceLbl);

        btn.AddChild(inner);

        btn.OnPressed += _ =>
        {
            if (_maxQty <= 0 || _qty <= 0)
                return;

            switch (data.Mode)
            {
                case StoreMode.Buy:
                    OnBuyPressed?.Invoke(_qty);
                    break;
                case StoreMode.Sell:
                    OnSellPressed?.Invoke(_qty);
                    break;
                case StoreMode.Exchange:
                    OnExchangePressed?.Invoke(_qty);
                    break;
            }
        };

        return btn;
    }

    private void SetQty(int v, StoreListingData data, Label qtyLbl)
    {
        var newQty = Math.Clamp(v, MinAllowed, Math.Max(MinAllowed, _maxQty));
        if (newQty == _qty)
            return;

        _qty = newQty;
        qtyLbl.Text = _qty.ToString();
        _qtyEdit.Text = _qty.ToString();
        _qtyEdit.CursorPosition = _qtyEdit.Text.Length;
        UpdateTotal(data);
        OnQtyChanged?.Invoke(_qty);
    }

    private void UpdateTotal(StoreListingData data)
    {
        if (_priceLbl is null)
            return;

        var value = _qty <= 0 ? data.Price : (long) data.Price * _qty;
        _priceLbl.Text = value > MaxTotalDisplay ? $"{MaxTotalDisplay}+" : value.ToString();
    }
}
