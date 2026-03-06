using ArcGIS.Desktop.Framework.Contracts;

namespace GISChat;

internal class OpenSettingsButton : Button
{
    protected override void OnClick()
    {
        var dialog = new Views.SettingsWindow();
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        dialog.ShowDialog();
    }
}
