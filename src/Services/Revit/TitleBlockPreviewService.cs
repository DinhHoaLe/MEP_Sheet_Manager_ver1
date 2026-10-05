using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace MEP_Sheet_Manager
{
    internal static class TitleBlockPreviewService
    {
        public static List<SheetLineInfo> Read(Document document, ViewSheet sheet)
        {
            var lines = new List<SheetLineInfo>();
            using (var collector = new FilteredElementCollector(document, sheet.Id))
            using (var options = new Options { View = sheet, IncludeNonVisibleObjects = false })
                foreach (var element in collector.OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsNotElementType())
                {
                    var geometry = element.get_Geometry(options);
                    if (geometry != null) ReadGeometry(geometry, Transform.Identity, lines);
                }
            return lines;
        }
        private static double Mm(double value) { return UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Millimeters); }
        private static void ReadGeometry(GeometryElement geometry, Transform transform, List<SheetLineInfo> lines)
        {
            foreach (var item in geometry)
            {
                var instance = item as GeometryInstance;
                if (instance != null)
                {
                    ReadGeometry(instance.GetSymbolGeometry(), transform.Multiply(instance.Transform), lines);
                    continue;
                }
                var curve = item as Curve;
                var polyline = item as PolyLine;
                IList<XYZ> points = curve != null ? curve.Tessellate() : polyline != null ? polyline.GetCoordinates() : null;
                if (points == null) continue;
                for (int i = 1; i < points.Count; i++)
                {
                    var a = transform.OfPoint(points[i - 1]); var b = transform.OfPoint(points[i]);
                    lines.Add(new SheetLineInfo { X1 = Mm(a.X), Y1 = Mm(a.Y), X2 = Mm(b.X), Y2 = Mm(b.Y) });
                }
            }
        }
    }
}
