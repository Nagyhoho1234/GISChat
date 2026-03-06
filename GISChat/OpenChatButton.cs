using ArcGIS.Desktop.Framework.Contracts;

namespace GISChat;

internal class OpenChatButton : Button
{
    protected override void OnClick()
    {
        Views.ChatDockPaneViewModel.Show();
    }
}
