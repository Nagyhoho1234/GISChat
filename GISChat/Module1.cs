using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace GISChat;

internal class Module1 : Module
{
    private static Module1? _this;

    public static Module1 Current =>
        _this ??= (Module1)FrameworkApplication.FindModule("GISChat_Module");

    protected override bool CanUnload() => true;
}
