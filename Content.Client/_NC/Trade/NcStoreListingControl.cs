using Content.Shared._NC.Trade;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._NC.Trade
{
    public sealed class NcStoreListingControl : PanelContainer
    {
        public event Action? OnBuyPressed;
        public event Action? OnSellPressed;
        public event Action? OnExchangePressed;

        public NcStoreListingControl(StoreListingData data, SpriteSystem spriteSystem)
        {
            var box = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                VerticalExpand = false
            };

            var protoManager = IoCManager.Resolve<IPrototypeManager>();
            string name = data.ProductEntity;
            string description = "";
            Texture? iconTexture = null;

            if (protoManager.TryIndex<EntityPrototype>(data.ProductEntity, out var proto))
            {
                name = proto.Name;
                description = proto.Description ?? string.Empty;
                iconTexture = spriteSystem.GetPrototypeIcon(proto.ID).Default;
            }

            if (iconTexture != null)
            {
                var iconRect = new TextureRect
                {
                    Texture = iconTexture,
                    Stretch = TextureRect.StretchMode.KeepAspectCentered,
                    MinSize = new(96, 96),
                    Margin = new Thickness(0, 0, 8, 0)
                };
                box.AddChild(iconRect);
            }

            var vbox = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
                Margin = new Thickness(0, 0, 1, 0)
            };

            var nameLabel = new Label
            {
                Text                = name,
                StyleClasses        = { "BoldLabel" },
                FontColorOverride   = Color.FromHex("#FFD700"),
                HorizontalAlignment = HAlignment.Left,
                Margin              = new Thickness(0, 0, 0, 1)
            };
            vbox.AddChild(nameLabel);

            if (!string.IsNullOrWhiteSpace(description))
            {
                vbox.AddChild(new Label
                {
                    Text                = description,
                    FontColorOverride   = Color.FromHex("#B0B0B0"),
                    HorizontalAlignment = HAlignment.Left,
                    MaxWidth            = 320,
                    Margin              = new Thickness(0, 0, 0, 0)
                });
            }

            box.AddChild(vbox);

            var buttonBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, MinWidth = 70, };

            if (data.Mode == StoreMode.Buy)
            {
                var buyBtn = new Button { Text = $"Купить [{data.Price}]", HorizontalExpand = true, };
                buyBtn.OnPressed += _ => OnBuyPressed?.Invoke();
                buttonBox.AddChild(buyBtn);
            }
            else if (data.Mode == StoreMode.Sell)
            {
                var sellBtn = new Button { Text = $"Продать [{data.Price}]", HorizontalExpand = true, };
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
