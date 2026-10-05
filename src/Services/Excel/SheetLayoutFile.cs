using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace MEP_Sheet_Manager
{
    public static class SheetLayoutFile
    {
        private static void Validate(IList<SheetLayoutInfo> rows)
        {
            if (rows.Count == 0) throw new InvalidDataException("Chưa có bố trí để lưu hoặc nạp.");
            if (rows.Any(r => string.IsNullOrWhiteSpace(r.SheetNumber) || string.IsNullOrWhiteSpace(r.ViewName)
                || double.IsNaN(r.CenterXmm) || double.IsInfinity(r.CenterXmm)
                || double.IsNaN(r.CenterYmm) || double.IsInfinity(r.CenterYmm)
                || double.IsNaN(r.OffsetXmm) || double.IsInfinity(r.OffsetXmm)
                || double.IsNaN(r.OffsetYmm) || double.IsInfinity(r.OffsetYmm)))
                throw new InvalidDataException("Bố trí thiếu sheet/view hoặc tọa độ không hợp lệ.");
            if (rows.GroupBy(r => r.SheetNumber, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                throw new InvalidDataException("Một sheet chỉ được có một dòng bố trí trong file này.");
        }
        public static void Write(string path, IList<SheetLayoutInfo> rows)
        {
            Validate(rows);
            var root = new XElement("SheetLayouts", new XAttribute("version", 1), new XAttribute("units", "mm"),
                rows.Select(r => new XElement("Layout", new XAttribute("sheetUniqueId", r.SheetUniqueId ?? ""),
                    new XAttribute("sheetNumber", r.SheetNumber), new XAttribute("sheetName", r.SheetName ?? ""),
                    new XAttribute("viewUniqueId", r.ViewUniqueId ?? ""), new XAttribute("viewName", r.ViewName),
                    new XAttribute("scopeCentered", r.ScopeCentered),
                    new XAttribute("offsetX", r.OffsetXmm.ToString("R", CultureInfo.InvariantCulture)),
                    new XAttribute("offsetY", r.OffsetYmm.ToString("R", CultureInfo.InvariantCulture)),
                    new XAttribute("centerX", r.CenterXmm.ToString("R", CultureInfo.InvariantCulture)),
                    new XAttribute("centerY", r.CenterYmm.ToString("R", CultureInfo.InvariantCulture)))));
            string temp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), Guid.NewGuid() + ".tmp");
            try
            {
                new XDocument(root).Save(temp);
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static List<SheetLayoutInfo> Read(string path)
        {
            XDocument xml;
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null, MaxCharactersInDocument = 4000000 })) xml = XDocument.Load(reader);
            var root = xml.Root;
            if (root == null || root.Name != "SheetLayouts" || (string)root.Attribute("version") != "1" || (string)root.Attribute("units") != "mm")
                throw new InvalidDataException("File bố trí không đúng định dạng hoặc đơn vị mm.");
            var rows = root.Elements("Layout").Select(r => new SheetLayoutInfo {
                SheetUniqueId = (string)r.Attribute("sheetUniqueId"), SheetNumber = (string)r.Attribute("sheetNumber"),
                SheetName = (string)r.Attribute("sheetName"), ViewUniqueId = (string)r.Attribute("viewUniqueId"),
                ViewName = (string)r.Attribute("viewName"),
                ScopeCentered = (bool?)r.Attribute("scopeCentered") ?? false,
                OffsetXmm = double.Parse((string)r.Attribute("offsetX") ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture),
                OffsetYmm = double.Parse((string)r.Attribute("offsetY") ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture),
                CenterXmm = double.Parse((string)r.Attribute("centerX") ?? "", NumberStyles.Float, CultureInfo.InvariantCulture),
                CenterYmm = double.Parse((string)r.Attribute("centerY") ?? "", NumberStyles.Float, CultureInfo.InvariantCulture)
            }).ToList();
            Validate(rows); return rows;
        }
        public static void Apply(IList<SheetLayoutInfo> layouts, IList<SheetViewAssignment> assignments)
        {
            Validate(layouts);
            var matches = new List<Tuple<SheetLayoutInfo, SheetViewAssignment, FloorPlanChoice>>();
            foreach (var layout in layouts)
            {
                var sheet = assignments.FirstOrDefault(r => !string.IsNullOrEmpty(layout.SheetUniqueId) && r.SheetUniqueId == layout.SheetUniqueId)
                    ?? assignments.SingleOrDefault(r => string.Equals(r.Number, layout.SheetNumber, StringComparison.OrdinalIgnoreCase));
                if (sheet == null) throw new InvalidDataException("Không tìm thấy sheet: " + layout.SheetNumber);
                var view = sheet.FloorPlans.FirstOrDefault(p => p.Id > 0 && !string.IsNullOrEmpty(layout.ViewUniqueId) && p.UniqueId == layout.ViewUniqueId);
                if (view == null)
                {
                    var byName = sheet.FloorPlans.Where(p => p.Id > 0 && p.Name == layout.ViewName).ToList();
                    if (byName.Count != 1) throw new InvalidDataException("Không tìm thấy floor plan có thể đặt: " + layout.ViewName + " (sheet " + layout.SheetNumber + ")");
                    view = byName[0];
                }
                matches.Add(Tuple.Create(layout, sheet, view));
            }
            if (matches.GroupBy(m => m.Item2.SheetId).Any(g => g.Count() > 1) || matches.GroupBy(m => m.Item3.Id).Any(g => g.Count() > 1))
                throw new InvalidDataException("File bố trí chọn trùng sheet hoặc floor plan.");
            // Only change UI assignments after the entire file has been matched successfully.
            foreach (var match in matches)
            {
                match.Item2.SelectedFloorPlan = match.Item3;
                if (match.Item1.ScopeCentered)
                    match.Item2.SetScopePosition(match.Item1.CenterXmm, match.Item1.CenterYmm, match.Item1.OffsetXmm, match.Item1.OffsetYmm);
                else match.Item2.SetPosition(match.Item1.CenterXmm, match.Item1.CenterYmm);
            }
        }
    }
}
