using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace MEP_Sheet_Manager
{
    internal static class FloorPlanPlacementService
    {
        public static List<SheetViewAssignment> Read(Document document)
        {
            List<ViewPlan> plans;
            List<ViewSheet> sheets;
            List<Viewport> viewports;
            using (var collector = new FilteredElementCollector(document))
                plans = collector.OfClass(typeof(ViewPlan)).Cast<ViewPlan>()
                    .Where(v => !v.IsTemplate && v.ViewType == ViewType.FloorPlan)
                    .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
            using (var collector = new FilteredElementCollector(document))
                sheets = collector.OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Where(s => !s.IsPlaceholder)
                    .OrderBy(s => s.SheetNumber, StringComparer.OrdinalIgnoreCase).ToList();
            using (var collector = new FilteredElementCollector(document))
                viewports = collector.OfClass(typeof(Viewport)).Cast<Viewport>().ToList();
            var placed = new HashSet<ElementId>(viewports.Select(v => v.ViewId));
            var names = plans.ToDictionary(p => p.Id, p => p.Name);
            var assignments = sheets.Select(sheet => new SheetViewAssignment {
                SheetId = sheet.Id.IntegerValue, SheetUniqueId = sheet.UniqueId, Number = sheet.SheetNumber, Name = sheet.Name,
                CurrentFloorPlans = string.Join(", ", viewports.Where(v => v.SheetId == sheet.Id && names.ContainsKey(v.ViewId))
                    .Select(v => names[v.ViewId]).OrderBy(n => n, StringComparer.OrdinalIgnoreCase)),
                FloorPlans = new[] { new FloorPlanChoice { Id = -1, Name = "<Không chọn>" } }.Concat(
                    plans.Where(p => !placed.Contains(p.Id) || viewports.Any(v => v.SheetId == sheet.Id && v.ViewId == p.Id))
                        .Select(p => new FloorPlanChoice { Id = p.Id.IntegerValue, UniqueId = p.UniqueId, Name = p.Name,
                            TemplateName = (document.GetElement(p.ViewTemplateId) as View)?.Name ?? "<None>" })).ToList()
            }).ToList();
            foreach (var row in assignments) row.SelectedFloorPlan = row.FloorPlans[0];
            return assignments;
        }
        private static Viewport ExistingViewport(Document document, ViewSheet sheet, ElementId planId)
        {
            return sheet.GetAllViewports().Select(id => document.GetElement(id) as Viewport)
                .FirstOrDefault(v => v != null && v.ViewId == planId);
        }
        private static XYZ Center(ViewSheet sheet, SheetViewAssignment row)
        {
            if (row.HasPosition) return new XYZ(UnitUtils.ConvertToInternalUnits(row.CenterXmm.Value, UnitTypeId.Millimeters),
                UnitUtils.ConvertToInternalUnits(row.CenterYmm.Value, UnitTypeId.Millimeters), 0);
            return ScopeBoxAlignmentService.PaperCenter(sheet);
        }

        public static List<SheetViewAssignment> Validate(Document document, IList<SheetViewAssignment> rows)
        {
            if (document.IsReadOnly) throw new InvalidOperationException("Project đang ở chế độ chỉ đọc.");
            var selected = rows.Where(r => r.SelectedFloorPlan != null && r.SelectedFloorPlan.Id > 0).ToList();
            var duplicates = new HashSet<int>(selected.GroupBy(r => r.SelectedFloorPlan.Id).Where(g => g.Count() > 1).Select(g => g.Key));
            var pending = new List<SheetViewAssignment>();
            int errors = 0;
            // Recheck every assignment before starting a transaction: Revit may have changed since preview.
            foreach (var row in selected)
            {
                var sheet = document.GetElement(new ElementId(row.SheetId)) as ViewSheet;
                var plan = document.GetElement(new ElementId(row.SelectedFloorPlan.Id)) as ViewPlan;
                if (duplicates.Contains(row.SelectedFloorPlan.Id)) row.Status = "Lỗi: cùng plan được chọn cho nhiều sheet";
                else if (sheet == null || sheet.IsPlaceholder) row.Status = "Lỗi: sheet đã xóa hoặc là placeholder";
                else if (plan == null || plan.IsTemplate || plan.ViewType != ViewType.FloorPlan) row.Status = "Lỗi: floor plan không còn hợp lệ";
                else if ((!row.HasPosition || row.ScopeCentered) && !CanAlignScope(document, plan, sheet, row)) { }
                else if (sheet.GetAllPlacedViews().Contains(plan.Id))
                {
                    var existing = ExistingViewport(document, sheet, plan.Id);
                    if (existing != null)
                    {
                        if (existing.Pinned) row.Status = "Lỗi: viewport đang pinned, hãy unpin trong Revit";
                        else { row.Status = "Sẵn sàng di chuyển"; pending.Add(row); }
                    }
                    else row.Status = "Bỏ qua: plan đã có trên sheet này";
                }
                else if (!Viewport.CanAddViewToSheet(document, sheet.Id, plan.Id)) row.Status = "Lỗi: plan đã đặt hoặc không thể thêm vào sheet";
                else { row.Status = "Sẵn sàng"; pending.Add(row); }
                if (row.Status.StartsWith("Lỗi:")) errors++;
            }
            if (errors > 0) throw new InvalidOperationException("Chưa đặt view nào.\n" + string.Join("\n",
                selected.Where(r => r.Status.StartsWith("Lỗi:")).Select(r => r.Number + ": " + r.Status)));
            return pending;
        }
        public static int Place(Document document, IList<SheetViewAssignment> rows)
        {
            var pending = Validate(document, rows);
            if (pending.Count == 0) return 0;
            using (var transaction = new Transaction(document, "Place floor plans on sheets"))
            {
                if (transaction.Start() != TransactionStatus.Started) throw new InvalidOperationException("Không thể bắt đầu transaction.");
                try
                {
                    foreach (var row in pending)
                    {
                        var sheet = (ViewSheet)document.GetElement(new ElementId(row.SheetId));
                        var planId = new ElementId(row.SelectedFloorPlan.Id);
                        var center = Center(sheet, row);
                        var viewport = ExistingViewport(document, sheet, planId);
                        if (viewport == null)
                        {
                            viewport = Viewport.Create(document, sheet.Id, planId, center);
                            document.Regenerate();
                        }
                        if (!row.HasPosition || row.ScopeCentered)
                        {
                            document.Regenerate();
                            center = ScopeBoxAlignmentService.AlignedBoxCenter(document, (ViewPlan)document.GetElement(planId), sheet, viewport);
                            center += new XYZ(UnitUtils.ConvertToInternalUnits(row.OffsetXmm, UnitTypeId.Millimeters),
                                UnitUtils.ConvertToInternalUnits(row.OffsetYmm, UnitTypeId.Millimeters), 0);
                        }
                        viewport.SetBoxCenter(center);
                    }
                    if (transaction.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Revit không commit được viewport.");
                    foreach (var row in pending) row.Status = "Đã đặt";
                }
                catch
                {
                    if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                    foreach (var row in pending) row.Status = "Lỗi: đã rollback, chưa đặt view";
                    throw;
                }
            }
            return pending.Count;
        }
        private static bool CanAlignScope(Document document, ViewPlan plan, ViewSheet sheet, SheetViewAssignment row)
        {
            try
            {
                var scope = ScopeBoxAlignmentService.Scope(document, plan);
                ScopeBoxAlignmentService.PaperCenter(sheet);
                if (scope != null && plan.GetModelToProjectionTransforms().Count != 1)
                    throw new InvalidOperationException("View có split crop; chưa hỗ trợ canh tâm Scope Box.");
                return true;
            }
            catch (Exception ex) { row.Status = "Lỗi: " + ex.Message; return false; }
        }
    }
}

