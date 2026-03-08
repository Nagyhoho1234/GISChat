using System.Text;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace GISChat.Services;

public static class MapContextService
{
    /// <summary>
    /// Gather current ArcGIS Pro map state to send as context to the LLM.
    /// </summary>
    public static async Task<string> GetMapContextAsync()
    {
        return await QueuedTask.Run(() =>
        {
            var sb = new StringBuilder();

            var mapView = MapView.Active;
            if (mapView == null)
            {
                sb.AppendLine("No active map view.");
                return sb.ToString();
            }

            var map = mapView.Map;
            sb.AppendLine($"Project: {ArcGIS.Desktop.Core.Project.Current.Name}");
            sb.AppendLine($"Active Map: {map.Name}");
            sb.AppendLine($"Spatial Reference: {map.SpatialReference?.Name ?? "Unknown"}");
            sb.AppendLine($"WKID: {map.SpatialReference?.Wkid}");

            var extent = mapView.Extent;
            sb.AppendLine($"Current Extent: ({extent.XMin:F2}, {extent.YMin:F2}) - ({extent.XMax:F2}, {extent.YMax:F2})");

            // Default geodatabase
            sb.AppendLine($"Default GDB: {ArcGIS.Desktop.Core.Project.Current.DefaultGeodatabasePath}");

            sb.AppendLine();
            sb.AppendLine("Layers:");

            var layers = map.GetLayersAsFlattenedList();
            foreach (var layer in layers)
            {
                var visibility = layer.IsVisible ? "visible" : "hidden";
                var layerType = layer switch
                {
                    FeatureLayer fl => $"FeatureLayer ({fl.ShapeType})",
                    RasterLayer => "RasterLayer",
                    GroupLayer => "GroupLayer",
                    _ => layer.GetType().Name
                };

                sb.AppendLine($"  - \"{layer.Name}\" [{layerType}, {visibility}]");

                // For feature layers, add field info and record count
                if (layer is FeatureLayer featureLayer)
                {
                    try
                    {
                        var fc = featureLayer.GetFeatureClass();
                        var count = fc.GetCount();
                        sb.AppendLine($"    Records: {count}");

                        // List key fields (skip shape/OID for brevity)
                        var fields = featureLayer.GetFieldDescriptions();
                        var fieldNames = fields
                            .Where(f => f.Type != ArcGIS.Core.Data.FieldType.Geometry
                                     && f.Type != ArcGIS.Core.Data.FieldType.OID)
                            .Take(15)
                            .Select(f => $"{f.Name} ({f.Type})");
                        sb.AppendLine($"    Fields: {string.Join(", ", fieldNames)}");

                        // Selected features
                        var selection = featureLayer.GetSelection();
                        if (selection.GetCount() > 0)
                            sb.AppendLine($"    Selected: {selection.GetCount()} features");
                    }
                    catch { }
                }

                // For raster layers, add band/resolution/stats info
                if (layer is RasterLayer rasterLayer)
                {
                    try
                    {
                        var raster = rasterLayer.GetRaster();
                        var bandCount = raster.GetBandCount();
                        var cellSize = raster.GetMeanCellSize();
                        sb.AppendLine($"    Cell size: {cellSize.Item1:F2} x {cellSize.Item2:F2}");
                        sb.AppendLine($"    Bands: {bandCount}");

                        for (int b = 0; b < Math.Min(bandCount, 20); b++)
                        {
                            try
                            {
                                var band = raster.GetBand(b);
                                var def = band.GetDefinition();
                                var name = def.GetName();
                                var pixelType = def.GetPixelType();
                                sb.AppendLine($"      [{b}] {name} ({pixelType})");
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }

            // List tables
            var tables = map.StandaloneTables;
            if (tables.Any())
            {
                sb.AppendLine();
                sb.AppendLine("Standalone Tables:");
                foreach (var table in tables)
                {
                    sb.AppendLine($"  - \"{table.Name}\"");
                }
            }

            return sb.ToString();
        });
    }
}
