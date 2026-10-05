using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using MEP_Sheet_Manager;

static class LayoutTests
{
    private static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS: " + label); }
    public static void Run(string folder)
    {
        string path = Path.Combine(folder, "LayoutRoundTrip.xml");
        var layout = new SheetLayoutInfo { SheetNumber = "001.01", SheetName = "Điện & nước <Tầng 1>",
            SheetUniqueId = "sheet-uid", ViewName = "Mặt bằng tầng 1", ViewUniqueId = "view-uid", CenterXmm = -12.125, CenterYmm = 210.75 };
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
            SheetLayoutFile.Write(path, new[] { layout });
            var back = SheetLayoutFile.Read(path).Single();
            Check(back.SheetNumber == "001.01" && back.SheetName == layout.SheetName && back.ViewName == layout.ViewName
                && back.CenterXmm == -12.125 && back.CenterYmm == 210.75, "layout XML preserves Vietnamese, leading zeros, negative and decimal millimeters across cultures");
            SheetLayoutFile.Write(path, new[] { layout });
            Check(SheetLayoutFile.Read(path).Count == 1, "layout atomic overwrite");
            var none = new FloorPlanChoice { Id = -1, Name = "<None>" };
            var view = new FloorPlanChoice { Id = 20, UniqueId = "view-uid", Name = "Renamed view" };
            var row = new SheetViewAssignment { SheetId = 10, SheetUniqueId = "sheet-uid", Number = "Renamed number",
                FloorPlans = new List<FloorPlanChoice> { none, view }, SelectedFloorPlan = none };
            SheetLayoutFile.Apply(new[] { layout }, new[] { row });
            Check(row.SelectedFloorPlan == view && row.HasPosition && row.CenterXmm == layout.CenterXmm, "layout matches stable IDs after sheet and view renaming");
            row.SelectedFloorPlan = none;
            Check(!row.HasPosition, "changing selected floor plan clears previous coordinates");
            var other = new SheetViewAssignment { SheetId = 11, SheetUniqueId = "new-sheet", Number = "001.01",
                FloorPlans = new List<FloorPlanChoice> { none, new FloorPlanChoice { Id = 21, UniqueId = "new-view", Name = layout.ViewName } }, SelectedFloorPlan = none };
            SheetLayoutFile.Apply(new[] { layout }, new[] { other });
            Check(other.HasPosition && other.SelectedFloorPlan.Id == 21, "layout can match exact sheet number and view name in another project");
            row.SelectedFloorPlan = none;
            bool rejected = false;
            try { SheetLayoutFile.Apply(new[] { layout, new SheetLayoutInfo { SheetNumber = "MISSING", ViewName = "Missing", CenterXmm = 1, CenterYmm = 1 } }, new[] { row }); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected && row.SelectedFloorPlan == none && !row.HasPosition, "missing layout target does not partially change earlier assignments");
            var xml = XDocument.Load(path); xml.Root.SetAttributeValue("units", "feet"); xml.Save(path);
            rejected = false; try { SheetLayoutFile.Read(path); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "reject layout with wrong coordinate units");
            layout.CenterXmm = double.NaN;
            rejected = false; try { SheetLayoutFile.Write(path, new[] { layout }); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "reject nonfinite coordinates before saving");
            layout.CenterXmm = -12.125;
            SheetLayoutFile.Write(path, new[] { layout });
            layout.ScopeCentered = true; layout.OffsetXmm = -7.25; layout.OffsetYmm = 9.5;
            SheetLayoutFile.Write(path, new[] { layout });
            var scoped = SheetLayoutFile.Read(path).Single();
            SheetLayoutFile.Apply(new[] { scoped }, new[] { other });
            Check(scoped.ScopeCentered && scoped.OffsetXmm == -7.25 && scoped.OffsetYmm == 9.5
                && other.ScopeCentered && other.OffsetXmm == -7.25 && other.OffsetYmm == 9.5,
                "scope alignment and offsets survive XML and reuse in another project");
            var legacy = XDocument.Load(path);
            legacy.Root.Element("Layout").Attribute("scopeCentered").Remove();
            legacy.Root.Element("Layout").Attribute("offsetX").Remove();
            legacy.Root.Element("Layout").Attribute("offsetY").Remove(); legacy.Save(path);
            Check(!SheetLayoutFile.Read(path).Single().ScopeCentered, "legacy layout remains absolute rather than silently changing its coordinates");
            other.SelectedFloorPlan = none;
            Check(!other.HasPosition && !other.ScopeCentered && other.OffsetXmm == 0 && other.OffsetYmm == 0,
                "switching view clears scope alignment offsets");
            layout.OffsetXmm = double.PositiveInfinity;
            rejected = false; try { SheetLayoutFile.Write(path, new[] { layout }); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "reject nonfinite scope offsets");
            layout.OffsetXmm = -7.25;
            SheetLayoutFile.Write(path, new[] { layout });

        }
        finally { CultureInfo.CurrentCulture = culture; }
    }
}

