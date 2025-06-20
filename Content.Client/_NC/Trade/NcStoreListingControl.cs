using Content.Shared._NC.Trade;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._NC.Trade;

public sealed class NcStoreListingControl : PanelContainer
{
    public event Action? OnBuyPressed;
    public event Action? OnSellPressed;
    public event Action? OnExchangePressed;

    public NcStoreListingControl(StoreListingData d, SpriteSystem sprites)
    {
        IoCManager.InjectDependencies(this);

        /* ───── рамка карточки (без padding хаком) ───── */
        PanelOverride = new StyleBoxFlat
        {
            BorderColor     = new Color(0x44,0x44,0x44,0xaa),
            BorderThickness = new Thickness(1)
        };
        Margin = new Thickness(0,0,0,4);             // зазор между лотами

        /* ───── контейнер с ручным внутренним отступом 3 px ───── */
        var pad = new BoxContainer
        {
            Orientation        = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 2,
            Margin             = new Thickness(3)     // padding
        };
        AddChild(pad);

        /* ───── данные прототипа ───── */
        var pm = IoCManager.Resolve<IPrototypeManager>();
        pm.TryIndex<EntityPrototype>(d.ProductEntity, out var proto);
        var name = proto?.Name ?? d.ProductEntity;
        var desc = proto?.Description ?? string.Empty;

        /* ───── иконка ───── */
        if (pm.TryIndex<EntityPrototype>(d.ProductEntity, out var p) &&
            sprites.GetPrototypeIcon(p.ID).Default is {} tex)
        {
            pad.AddChild(new TextureRect
            {
                Texture = tex,
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                MinSize = new Vector2i(96,96)
            });
        }

        /* ───── текстовый блок ───── */
        var v = new BoxContainer
        {
            Orientation      = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true
        };
        pad.AddChild(v);

        v.AddChild(new RichTextLabel
        {
            Text     = $"[color=yellow]{name}[/color]",
            MaxWidth = 440,
            Margin   = new Thickness(0,0,0,1)
        });
        if (!string.IsNullOrWhiteSpace(desc))
            v.AddChild(new RichTextLabel { Text = $"[wrap=true]{desc}", MaxWidth = 440 });

        /* ───── кнопка цены ───── */
        pad.AddChild(MakePriceButton(d));
    }

    /* ───────── helpers ───────── */

    private Control MakePriceButton(StoreListingData d)
    {
        var tint = d.Mode switch
        {
            StoreMode.Buy      => new Color(0x32,0xa8,0x3a),
            StoreMode.Sell     => new Color(0xd4,0x45,0x45),
            StoreMode.Exchange => new Color(0x45,0x6a,0xd4),
            _                  => Color.Gray
        };

        var btn = new Button
        {
            Text                 = d.Price.ToString(),   // только число
            MinSize              = new Vector2i(80,24),
            HorizontalExpand     = false,
            ModulateSelfOverride = tint
        };

        /* чёрная рамка 1 px */
        btn.StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = tint,
            BorderColor     = Color.Black,
            BorderThickness = new Thickness(1),
        };

        btn.OnPressed += _ =>
        {
            switch (d.Mode)
            {
                case StoreMode.Buy:      OnBuyPressed?.Invoke();      break;
                case StoreMode.Sell:     OnSellPressed?.Invoke();     break;
                case StoreMode.Exchange: OnExchangePressed?.Invoke(); break;
            }
        };
        return btn;
    }
}

