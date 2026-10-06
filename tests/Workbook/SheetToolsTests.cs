using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MEP_Sheet_Manager;

static class SheetToolsTests
{
    static void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS: " + label); }
    static void Reject(Action action, string label) { bool rejected = false; try { action(); } catch (InvalidDataException) { rejected = true; } Check(rejected, label); }
    public static void Run(string folder)
    {
        var headers = new[] { "SheetNumber", "SheetName", "FloorPlanName" };
        var data = new TabularData { Kind = "SheetViews", Headers = headers, Rows = new List<string[]> { new[] { "001.02", "Điện & nước", "=Tầng 1 <A>" } } };
        foreach (var extension in new[] { ".xlsx", ".json" }) {
            var path = Path.Combine(folder, "ViewTools" + extension);
            TabularFile.Write(path, data); TabularFile.Write(path, data);
            var back = TabularFile.Read(path, data.Kind, headers);
            Check(back.Rows.Single().SequenceEqual(data.Rows.Single()), "tab export preserves leading zeros, Vietnamese and literal text: " + extension);
            Reject(() => TabularFile.Read(path, "SheetRevisions", new[] { "SheetNumber", "SheetName", "AdditionalRevisionKeys" }), "wrong-tab import rejected: " + extension);
        }
        var key = "2 | 06/10/2026 | Chỉnh sửa, \"Điện\"; tầng 1";
        Check(TabularFile.Unpack(TabularFile.Pack(new[] { key })).Single() == key, "revision keys preserve delimiters and Unicode");
        var none = new FloorPlanChoice { Id = -1, Name = "<None>" }; var plan = new FloorPlanChoice { Id = 7, Name = "Floor 1", TemplateName = "MEP" };
        var a = new SheetViewAssignment { Number = "001", Name = "A", FloorPlans = new[] { none, plan }, SelectedFloorPlan = none };
        var b = new SheetViewAssignment { Number = "002", Name = "B", FloorPlans = new[] { none, plan }, SelectedFloorPlan = none };
        var rows = new[] { a, b };
        data.Rows = new List<string[]> { new[] { "001", "A", "Floor 1" }, new[] { "missing", "", "" } };
        Reject(() => SheetTabImport.Views(data, rows), "invalid view import leaves earlier rows unchanged");
        Check(a.SelectedFloorPlan.Id == -1, "view import is atomic in UI snapshot");
        data.Rows[1] = new[] { "002", "B", "Floor 1" };
        Reject(() => SheetTabImport.Views(data, rows), "duplicate plan across sheets rejected before update");
        data.Rows.RemoveAt(1); SheetTabImport.Views(data, rows);
        a.FilterTemplates("Other"); Check(a.SelectedFloorPlan == plan && a.FloorPlans.Contains(plan), "template filter retains selected plan and never clears assignment");
        a.SelectedFloorPlan = none; a.FilterTemplates("Other"); Check(!a.FloorPlans.Contains(plan), "template filter hides unselected views from other templates");
        a.FilterTemplates(null); Check(a.FloorPlans.Contains(plan), "All restores full cached plan list");
        var cloud = new RevisionChoice { Id = 1, Key = "cloud", Selected = true, CanEdit = false };
        var additional = new RevisionChoice { Id = 2, Key = key, CanEdit = true };
        var revisionRow = new SheetRevisionRow { Number = "001", Revisions = new List<RevisionChoice> { cloud, additional }, InitialAdditionalIds = new HashSet<int>() };
        var revisionData = new TabularData { Kind = "SheetRevisions", Headers = new[] { "SheetNumber", "SheetName", "AdditionalRevisionKeys" }, Rows = new List<string[]> { new[] { "001", "A", TabularFile.Pack(new[] { key }) }, new[] { "missing", "B", "[]" } } };
        Reject(() => SheetTabImport.Revisions(revisionData, new[] { revisionRow }), "invalid revision import leaves earlier selections unchanged");
        Check(!additional.Selected && cloud.Selected && !revisionRow.Modified, "revision import preserves locked clouds and unchanged state");
        revisionData.Rows.RemoveAt(1); SheetTabImport.Revisions(revisionData, new[] { revisionRow });
        Check(cloud.Selected && additional.Selected && revisionRow.Modified, "revision import updates additional selections and marks changes");
        var revisionPath = Path.Combine(folder, "RevisionTools.xlsx"); TabularFile.Write(revisionPath, revisionData);
        Check(TabularFile.Read(revisionPath, revisionData.Kind, revisionData.Headers).Rows[0][2] == revisionData.Rows[0][2], "revision Excel round trip preserves key arrays");
        var rule = new RenameRule { Find = "A", Replace = "E", Prefix = "MEP-", Suffix = "-01" };
        Check(rule.Apply("A101") == "MEP-E101-01", "rename find/replace/prefix/suffix preview rule");
    }
}
