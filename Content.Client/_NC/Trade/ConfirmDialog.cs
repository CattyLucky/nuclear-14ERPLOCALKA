using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.Graphics;
namespace Content.Client._NC.Trade;

public sealed class ConfirmDialog : DefaultWindow
{
    public event Action? OnConfirmed;
    public event Action? OnCancelled;

    public ConfirmDialog(string title, string message)
    {
        Title = title;
        MinSize = new Vector2i(300, 120);
        SetSize = new Vector2i(320, 140);

        var label = new Label
        {
            Text = message,
        };

        var confirm = new Button
        {
            Text = Loc.GetString("ui-yes"),
        };

        var cancel = new Button
        {
            Text = Loc.GetString("ui-no"),
        };

        confirm.OnPressed += _ =>
        {
            OnConfirmed?.Invoke();
            Close();
        };

        cancel.OnPressed += _ =>
        {
            OnCancelled?.Invoke();
            Close();
        };

        var buttonRow = new BoxContainer();
        buttonRow.Orientation = BoxContainer.LayoutOrientation.Horizontal;
        buttonRow.AddChild(confirm);
        buttonRow.AddChild(cancel);

        var layout = new BoxContainer();
        layout.Orientation = BoxContainer.LayoutOrientation.Vertical;
        layout.AddChild(label);
        layout.AddChild(buttonRow);

        var panel = new PanelContainer();
        panel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = new Color(32, 32, 32, 192),
            PaddingTop = 6,
            PaddingBottom = 6,
            PaddingLeft = 6,
            PaddingRight = 6,
        };

        panel.AddChild(layout);
        Contents.AddChild(panel);
    }
}
