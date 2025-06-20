using Content.Shared._NC.Trade;
using Content.Client.Stylesheets;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._NC.Trade;

/// <summary>
/// Карточка лота магазина, Nano‑UI.
/// Иконка 96 × 96 обрамлена «слотом»‑панелью,
/// чтобы ясно видеть её границы.
/// </summary>
public sealed class NcStoreListingControl : PanelContainer
{
    public event Action? OnBuyPressed;
    public event Action? OnSellPressed;
    public event Action? OnExchangePressed;

    public NcStoreListingControl(StoreListingData d, SpriteSystem sprites)
    {
        IoCManager.InjectDependencies(this);

        /* ───── фон карточки ───── */
        StyleClasses.Add(StyleNano.StyleClassBackgroundBaseDark);
        Margin = new Thickness(0, 0, 0, 4);

        /* ───── горизонтальный контейнер ───── */
        var pad = new BoxContainer
        {
            Orientation        = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 2,   // почти вплотную к слоту
            Margin             = new Thickness(4)
        };
        AddChild(pad);

        /* ───── данные прототипа ───── */
        var pm = IoCManager.Resolve<IPrototypeManager>();
        pm.TryIndex<EntityPrototype>(d.ProductEntity, out var proto);
        var name = proto?.Name ?? d.ProductEntity;
        var desc = proto?.Description ?? string.Empty;

        /* ───── иконка 96×96 + «слот»‑рамка ───── */
        if (pm.TryIndex<EntityPrototype>(d.ProductEntity, out var p) &&
            sprites.GetPrototypeIcon(p.ID).Default is { } tex)
        {
            var slot = new PanelContainer
            {
                StyleClasses = { StyleNano.StyleClassInventorySlotBackground },
                MinSize      = new Vector2i(96, 96)
            };
            slot.AddChild(new TextureRect
            {
                Texture = tex,
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                Margin  = new Thickness(2) // небольшое поле внутри рамки
            });
            pad.AddChild(slot);
        }

        /* ───── вертикальный блок текста ───── */
        var v = new BoxContainer
        {
            Orientation      = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true
        };
        pad.AddChild(v);

        // Заголовок
        v.AddChild(new Label
        {
            Text         = name,
            StyleClasses = { StyleNano.StyleClassLabelHeading },
            Margin       = new Thickness(0, 0, 0, 2)
        });

        // Описание (wrap под заголовком)
        if (!string.IsNullOrWhiteSpace(desc))
        {
            v.AddChild(new RichTextLabel
            {
                Text         = $"[wrap=true]{desc}",
                StyleClasses = { StyleNano.StyleClassLabelSubText },
                MaxWidth     = 440
            });
        }

        /* ───── кнопка цены ───── */
        pad.AddChild(MakePriceButton(d));
    }

    private Control MakePriceButton(StoreListingData d)
    {
        var priceClass = d.Mode switch
        {
            StoreMode.Buy  => StyleNano.StyleClassButtonColorGreen,
            StoreMode.Sell => StyleNano.StyleClassButtonColorRed,
            _              => ContainerButton.StyleClassButton
        };

        var btn = new Button
        {
            Text             = d.Price.ToString(),
            MinSize          = new Vector2i(80, 24),
            HorizontalExpand = false,
        };
        btn.StyleClasses.Add(priceClass);

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
