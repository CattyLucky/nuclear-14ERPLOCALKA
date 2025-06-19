using Content.Shared._NC.Trade;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._NC.Trade
{
    public sealed class NcStoreListingControl : PanelContainer
    {
        public event Action? OnBuyPressed;
        public event Action? OnSellPressed;
        public event Action? OnExchangePressed;

        public NcStoreListingControl(StoreListingData data, SpriteSystem spriteSystem)
        {
            // Основной layout карточки (можешь дооформить XAML)
            var box = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                VerticalExpand = false
            };

            // Иконка товара
            var iconRect = new TextureRect
            {
                Texture = spriteSystem.Frame0(data.Icon),
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                MinSize = new(32, 32)
            };
            box.AddChild(iconRect);

            // Описание и название
            var vbox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, };
            vbox.AddChild(new Label { Text = data.Name, StyleClasses = { "BoldLabel", }, });
            if (!string.IsNullOrEmpty(data.Description))
                vbox.AddChild(new Label { Text = data.Description, });
            box.AddChild(vbox);

            // Кнопки действия
            var buttonBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, MinWidth = 70, };

            if (data.Mode == StoreMode.Buy)
            {
                var buyBtn = new Button { Text = $"Купить [{data.Cost}]", HorizontalExpand = true, };
                buyBtn.OnPressed += _ => OnBuyPressed?.Invoke();
                buttonBox.AddChild(buyBtn);
            }
            else if (data.Mode == StoreMode.Sell)
            {
                var sellBtn = new Button { Text = $"Продать [{data.Cost}]", HorizontalExpand = true, };
                sellBtn.OnPressed += _ => OnSellPressed?.Invoke();
                buttonBox.AddChild(sellBtn);
            }
            else if (data.Mode == StoreMode.Exchange)
            {
                var exchangeBtn = new Button { Text = "Обменять", HorizontalExpand = true, };
                exchangeBtn.OnPressed += _ => OnExchangePressed?.Invoke();
                buttonBox.AddChild(exchangeBtn);
            }

            box.AddChild(buttonBox);
            AddChild(box);
        }
    }
}
