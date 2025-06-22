using Content.Client.Stylesheets;
using Content.Shared._NC.Trade;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;


namespace Content.Client._NC.Trade;


/// <summary>
///     Карточка товара для NcStore.
///     Разметка упрощена: текст (заголовок + описание) теперь рендерится одним RichTextLabel,
///     поэтому никаких скрытых отступов или отрицательных margin больше не нужно.
///     Структура строки: [ слот‑иконка | RichText | кнопка цены ].
/// </summary>
public sealed class NcStoreListingControl : PanelContainer
{
    private const int SlotPx = 96; // размер квадрата с иконкой
    private const int Gap = 4; // зазор между рамкой слота и текстом
    private const int PriceW = 96; // ширина кнопки цены
    private const int PriceH = 32; // высота кнопки цены
    private const int TextMax = 420; // макс. ширина текстового блока

    public NcStoreListingControl(StoreListingData data, SpriteSystem sprites)
    {
        // фон и разделитель
        StyleClasses.Add(StyleNano.StyleClassBackgroundBaseDark);
        StyleClasses.Add(StyleNano.ClassLowDivider);

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = Gap,
            Margin = new(0)
        };
        AddChild(row);

        // ─── Слот‑иконка ───
        if (MakeSlot(data, sprites) is { } slot)
            row.AddChild(slot);

        // ─── Текст (RichTextLabel) ───
        row.AddChild(MakeRichText(data));

        // ─── Кнопка цены ───
        row.AddChild(MakePriceButton(data));
    }

    public event Action? OnBuyPressed;
    public event Action? OnSellPressed;
    public event Action? OnExchangePressed;

    // ------------------------------------------------------------------
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

    // ------------------------------------------------------------------
    private Control MakeRichText(StoreListingData data)
    {
        var pm = IoCManager.Resolve<IPrototypeManager>();
        pm.TryIndex<EntityPrototype>(data.ProductEntity, out var proto);
        var name = (proto?.Name ?? data.ProductEntity).ToUpperInvariant();
        var desc = proto?.Description ?? string.Empty;

        var rtxt = new RichTextLabel
        {
            MaxWidth = TextMax,
            HorizontalExpand = true,
            Margin = new(0) // больше никаких отрицательных margin
        };

        // жёлтый, жирный заголовок + перенос строки + описание
        rtxt.Text =
            $"[wrap=true][color=#FFD84C][size=14][bold]{name}[/bold][/size][/color]\n{desc}";

        return rtxt;
    }

    // ------------------------------------------------------------------
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
            Text = data.Price.ToString(),
            MinSize = new Vector2i(PriceW, PriceH),
            MaxSize = new Vector2i(PriceW, PriceH),
            ClipText = true,
            Margin = new(8, 0, 0, 0),
            StyleClasses = { StyleNano.StyleClassButtonBig, }
        };

        btn.StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = color,
            BorderColor = Color.Black,
            BorderThickness = new(1)
        };

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
