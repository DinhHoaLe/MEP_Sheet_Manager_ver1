using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MEP_Sheet_Manager;

[Transaction(TransactionMode.Manual)]
public sealed class RevitTabToolsTests : IExternalCommand
{
    private static readonly Type Service = typeof(SheetInfo).Assembly.GetType("MEP_Sheet_Manager.SheetToolsService");
    private static object Call(string method, params object[] args) {
        try { return Service.GetMethod(method).Invoke(null, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
    }
    private static void Check(bool condition, string label, List<string> report) { if (!condition) throw new Exception(label); report.Add("PASS: " + label); }
    public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
    {
        var doc = data.Application.ActiveUIDocument.Document; var report = new List<string>();
        try {
            using (var group = new TransactionGroup(doc, "Tab tools tests - always rollback")) {
                group.Start();
                try {
                    ViewSheet a = null, b = null; Revision revision = null;
                    var prefix = "SMTEST-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    using (var tx = new Transaction(doc, "Create temporary fixtures")) {
                        tx.Start(); a = ViewSheet.Create(doc, ElementId.InvalidElementId); a.SheetNumber = prefix + "-1"; a.Name = "Điện tầng 1";
                        b = ViewSheet.Create(doc, ElementId.InvalidElementId); b.SheetNumber = prefix + "-2"; b.Name = "Điện tầng 2";
                        revision = Revision.Create(doc); revision.Description = "Test revision"; revision.RevisionDate = "06/10/2026";
                        if (tx.Commit() != TransactionStatus.Committed) throw new Exception("Fixture commit failed");
                    }
                    var rows = new List<SheetInfo> { new SheetInfo(a.SheetNumber, a.Name, false) { SheetId = a.Id.IntegerValue, SheetUniqueId = a.UniqueId },
                        new SheetInfo(b.SheetNumber, b.Name, false) { SheetId = b.Id.IntegerValue, SheetUniqueId = b.UniqueId } };
                    Call("Rename", doc, rows, new[] { prefix + "-2", prefix + "-1" }, true);
                    Check(a.SheetNumber == prefix + "-2" && b.SheetNumber == prefix + "-1", "swap sheet numbers without collision", report);
                    bool rejected = false; try { Call("Rename", doc, rows, new[] { "Duplicate", "Duplicate" }, true); } catch (InvalidOperationException) { rejected = true; }
                    Check(rejected && a.SheetNumber == prefix + "-2", "duplicate sheet numbers rejected before mutation", report);
                    Call("Rename", doc, rows, new[] { "Điện & nước 1", "Điện & nước 2" }, false);
                    Check(a.Name == "Điện & nước 1", "sheet names update through transaction", report);
                    var countBefore = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).GetElementCount();
                    Call("DuplicateEmpty", doc, rows);
                    Check(new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).GetElementCount() == countBefore + 2, "duplicate empty sheets creates unique sheet numbers", report);
                    var revisionRows = ((List<SheetRevisionRow>)Call("ReadSheetRevisions", doc)).Where(r => r.UniqueId == a.UniqueId).ToList();
                    revisionRows[0].Revisions.Single(r => r.Id == revision.Id.IntegerValue).Selected = true;
                    Call("ApplyRevisions", doc, revisionRows);
                    Check(a.GetAdditionalRevisionIds().Contains(revision.Id), "revision assignment saved on sheet", report);
                    var fresh = ((List<SheetRevisionRow>)Call("ReadSheetRevisions", doc)).Single(r => r.UniqueId == a.UniqueId);
                    Check(fresh.CurrentDescription == revision.Description && !fresh.Modified, "current revision read from sheet and clean after apply", report);
                    fresh.Revisions.Single(r => r.Id == revision.Id.IntegerValue).Selected = false;
                    Call("ApplyRevisions", doc, new[] { fresh });
                    Check(!a.GetAdditionalRevisionIds().Contains(revision.Id), "additional revision can be removed", report);
                    var parameters = (List<EditableParameterInfo>)Call("Parameters", doc, rows);
                    var drawnBy = parameters.FirstOrDefault(p => p.Id == (int)BuiltInParameter.SHEET_DRAWN_BY);
                    if (drawnBy != null) {
                        Call("SetParameter", doc, rows, drawnBy.Id, "SM Test");
                        Check(a.get_Parameter(BuiltInParameter.SHEET_DRAWN_BY).AsString() == "SM Test" && b.get_Parameter(BuiltInParameter.SHEET_DRAWN_BY).AsString() == "SM Test", "bulk sheet string parameter", report);
                    } else report.Add("SKIP: Drawn By parameter not writable in this project");
                    Call("SaveSet", doc, rows, prefix + "-SET");
                    var sets = (List<SheetSetInfo>)Call("ReadSets", doc);
                    Check(sets.Single(s => s.Name == prefix + "-SET").SheetIds.SetEquals(rows.Select(r => r.SheetId)), "native V/S Set contains selected sheets", report);
                } finally { if (group.GetStatus() == TransactionStatus.Started) group.RollBack(); }
            }
            TaskDialog.Show("Sheet tab tools tests", string.Join("\n", report) + "\nAll temporary project changes rolled back.");
            return Result.Succeeded;
        } catch (Exception ex) { message = ex.Message; TaskDialog.Show("Sheet tab tools tests", string.Join("\n", report) + "\nFAIL: " + ex.Message + "\nTemporary changes rolled back."); return Result.Failed; }
    }
}
