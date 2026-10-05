using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using MEP_Sheet_Manager;

[Transaction(TransactionMode.Manual)]
public class RevitScopeAlignmentTests : IExternalCommand
{
    public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
    {
        var document = data.Application.ActiveUIDocument.Document;
        var lines = new List<string>();
        try
        {
            var candidates = new FilteredElementCollector(document).OfClass(typeof(Viewport)).Cast<Viewport>()
                .Where(v => !v.Pinned && document.GetElement(v.ViewId) is ViewPlan)
                .Where(v => {
                    var plan = (ViewPlan)document.GetElement(v.ViewId);
                    var p = plan.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP);
                    bool scoped = p != null && document.GetElement(p.AsElementId()) != null;
                    var sheet = (ViewSheet)document.GetElement(v.SheetId);
                    return !plan.IsTemplate && plan.ViewType == ViewType.FloorPlan
                        && (!scoped || plan.GetModelToProjectionTransforms().Count == 1)
                        && UnitUtils.ConvertFromInternalUnits(sheet.Outline.Max.U - sheet.Outline.Min.U, UnitTypeId.Millimeters) > 138;
                }).GroupBy(v => {
                    var p = document.GetElement(v.ViewId).get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP);
                    return p != null && document.GetElement(p.AsElementId()) != null;
                }).Select(g => g.First()).ToList();
            if (candidates.Count == 0) throw new InvalidOperationException("Cần Floor Plan có viewport unpinned trên sheet có title block. Để test cả hai nhánh, chuẩn bị một view có scope và một view không có scope.");
            var service = typeof(SheetInfo).Assembly.GetType("MEP_Sheet_Manager.FloorPlanPlacementService");
            var rows = (List<SheetViewAssignment>)service.GetMethod("Read").Invoke(null, new object[] { document });
            using (var group = new TransactionGroup(document, "Test scope alignment - always rollback"))
            {
                group.Start();
                try
                {
                    foreach (var viewport in candidates)
                    {
                        var plan = (ViewPlan)document.GetElement(viewport.ViewId);
                        var sheet = (ViewSheet)document.GetElement(viewport.SheetId);
                        var row = rows.Single(r => r.SheetId == sheet.Id.IntegerValue);
                        row.SelectedFloorPlan = row.FloorPlans.Single(p => p.Id == plan.Id.IntegerValue);
                        using (var tx = new Transaction(document, "Perturb position and rotation"))
                        {
                            tx.Start(); viewport.SetBoxCenter(viewport.GetBoxCenter() + new XYZ(1, -1, 0));
                            viewport.Rotation = viewport.Rotation == ViewportRotation.None ? ViewportRotation.Clockwise : ViewportRotation.None;
                            tx.Commit();
                        }
                        foreach (double offset in new[] { 0.0, 12.5 })
                        {
                            // Deliberately identical stale absolute centers: scope mode must recompute each view.
                            row.SetScopePosition(1, 2, offset, -offset);
                            service.GetMethod("Place").Invoke(null, new object[] { document, new List<SheetViewAssignment> { row } });
                            var parameter = plan.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP);
                            var scope = parameter == null ? null : document.GetElement(parameter.AsElementId());
                            XYZ onSheet = viewport.GetBoxCenter();
                            if (scope != null)
                            {
                                var bounds = scope.get_BoundingBox(null);
                                var model = bounds.Transform.OfPoint((bounds.Min + bounds.Max) / 2);
                                onSheet = viewport.GetProjectionToSheetTransform().OfPoint(plan.GetModelToProjectionTransforms()[0].GetModelToProjectionTransform().OfPoint(model));
                            }
                            var paper = sheet.Outline;
                            double targetX = (UnitUtils.ConvertFromInternalUnits(paper.Min.U + paper.Max.U, UnitTypeId.Millimeters) + 44 - 94) / 2 + offset;
                            double targetY = UnitUtils.ConvertFromInternalUnits(paper.Min.V + paper.Max.V, UnitTypeId.Millimeters) / 2 - offset;
                            if (Math.Abs(UnitUtils.ConvertFromInternalUnits(onSheet.X, UnitTypeId.Millimeters) - targetX) > 0.01
                                || Math.Abs(UnitUtils.ConvertFromInternalUnits(onSheet.Y, UnitTypeId.Millimeters) - targetY) > 0.01)
                                throw new Exception("Scope center does not match paper target: " + plan.Name);
                            lines.Add("PASS: " + plan.Name + " / " + (scope == null ? "viewport fallback" : "scope") + " / rotation + offset " + offset + " mm");
                        }
                    }
                }
                finally { if (group.GetStatus() == TransactionStatus.Started) group.RollBack(); }
            }
            TaskDialog.Show("Scope alignment tests", string.Join("\n", lines) + "\nAll model changes rolled back.");
            return Result.Succeeded;
        }
        catch (Exception ex) { message = ex.ToString(); return Result.Failed; }
    }
}
