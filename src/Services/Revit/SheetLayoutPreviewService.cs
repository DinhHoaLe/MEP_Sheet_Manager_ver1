using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;

namespace MEP_Sheet_Manager
{
    internal static class SheetLayoutPreviewService
    {
        private static double Mm(double value) { return UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Millimeters); }
        public static List<SheetLayoutPreview> Read(Document document, IList<SheetViewAssignment> rows, string imageFolder)
        {
            if (document.IsReadOnly) throw new InvalidOperationException("Project đang chỉ đọc; không thể đo viewport tạm để preview.");
            var selected = rows.Where(r => r.SelectedFloorPlan != null && r.SelectedFloorPlan.Id > 0).ToList();
            if (selected.Count == 0) throw new InvalidOperationException("Chọn Floor Plan trước khi mở Preview.");
            if (selected.GroupBy(r => r.SelectedFloorPlan.Id).Any(g => g.Count() > 1))
                throw new InvalidOperationException("Một Floor Plan đang được chọn cho nhiều sheet. Sửa lựa chọn trước khi preview.");
            var result = new List<SheetLayoutPreview>();
            foreach (var row in selected)
            {
                var sheet = document.GetElement(new ElementId(row.SheetId)) as ViewSheet;
                var plan = document.GetElement(new ElementId(row.SelectedFloorPlan.Id)) as ViewPlan;
                if (sheet == null || sheet.IsPlaceholder || plan == null || plan.IsTemplate || plan.ViewType != ViewType.FloorPlan)
                    throw new InvalidOperationException("Sheet hoặc floor plan không còn hợp lệ: " + row.Number);
                var sheetBounds = sheet.Outline;
                if (Mm(sheetBounds.Max.U - sheetBounds.Min.U) <= SheetLayoutMargins.LeftMm + SheetLayoutMargins.RightMm || sheetBounds.Max.V <= sheetBounds.Min.V)
                    throw new InvalidOperationException("Không xác định được vùng giấy của sheet " + row.Number + ". Hãy thêm title block phù hợp trước khi preview.");
                var viewport = sheet.GetAllViewports().Select(id => document.GetElement(id) as Viewport)
                    .FirstOrDefault(v => v != null && v.ViewId == plan.Id);
                double viewWidth, viewHeight;
                double rotation = 0;
                Element scope = ScopeBoxAlignmentService.Scope(document, plan);
                XYZ scopeBase = null;
                XYZ center;
                if (viewport != null)
                {
                    var bounds = viewport.GetBoxOutline();
                    viewWidth = Mm(bounds.MaximumPoint.X - bounds.MinimumPoint.X);
                    viewHeight = Mm(bounds.MaximumPoint.Y - bounds.MinimumPoint.Y);
                    center = viewport.GetBoxCenter();
                    scopeBase = ScopeBoxAlignmentService.AlignedBoxCenter(document, plan, sheet, viewport);
                    rotation = viewport.Rotation == ViewportRotation.Clockwise ? 90 : viewport.Rotation == ViewportRotation.Counterclockwise ? -90 : 0;
                }
                else
                {
                    if (!Viewport.CanAddViewToSheet(document, sheet.Id, plan.Id))
                        throw new InvalidOperationException("Floor plan không thể đặt vào sheet " + row.Number + ". Cập nhật danh sách và chọn lại.");
                    center = new XYZ((sheetBounds.Min.U + UnitUtils.ConvertToInternalUnits(SheetLayoutMargins.LeftMm, UnitTypeId.Millimeters)
                        + sheetBounds.Max.U - UnitUtils.ConvertToInternalUnits(SheetLayoutMargins.RightMm, UnitTypeId.Millimeters)) / 2,
                        (sheetBounds.Min.V + sheetBounds.Max.V) / 2, 0);
                    // Measure a real viewport, then always roll back; preview leaves no new viewport in the model.
                    using (var tx = new Transaction(document, "Measure viewport for layout preview"))
                    {
                        if (tx.Start() != TransactionStatus.Started) throw new InvalidOperationException("Không thể bắt đầu đo viewport.");
                        try
                        {
                            viewport = Viewport.Create(document, sheet.Id, plan.Id, center);
                            document.Regenerate();
                            var bounds = viewport.GetBoxOutline();
                            viewWidth = Mm(bounds.MaximumPoint.X - bounds.MinimumPoint.X);
                            viewHeight = Mm(bounds.MaximumPoint.Y - bounds.MinimumPoint.Y);
                            center = viewport.GetBoxCenter();
                            scopeBase = ScopeBoxAlignmentService.AlignedBoxCenter(document, plan, sheet, viewport);
                        }
                        finally { if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack(); }
                    }
                }
                bool scopeCentered = !row.HasPosition || row.ScopeCentered;
                if (scopeCentered) center = scopeBase + new XYZ(
                    UnitUtils.ConvertToInternalUnits(row.OffsetXmm, UnitTypeId.Millimeters),
                    UnitUtils.ConvertToInternalUnits(row.OffsetYmm, UnitTypeId.Millimeters), 0);
                var preview = new SheetLayoutPreview {
                    Layout = new SheetLayoutInfo { SheetUniqueId = sheet.UniqueId, SheetNumber = sheet.SheetNumber,
                        SheetName = sheet.Name, ViewUniqueId = plan.UniqueId, ViewName = plan.Name,
                        CenterXmm = !scopeCentered && row.HasPosition ? row.CenterXmm.Value : Mm(center.X),
                        CenterYmm = !scopeCentered && row.HasPosition ? row.CenterYmm.Value : Mm(center.Y),
                        ScopeCentered = scopeCentered, OffsetXmm = row.OffsetXmm, OffsetYmm = row.OffsetYmm },
                    HasScope = scope != null, ScopeName = scope == null ? "<Không có Scope Box>" : scope.Name,
                    ScopeBaseXmm = scopeBase == null ? 0 : Mm(scopeBase.X), ScopeBaseYmm = scopeBase == null ? 0 : Mm(scopeBase.Y),
                    MinXmm = Mm(sheetBounds.Min.U), MinYmm = Mm(sheetBounds.Min.V),
                    MaxXmm = Mm(sheetBounds.Max.U), MaxYmm = Mm(sheetBounds.Max.V),
                    ViewWidthMm = viewWidth, ViewHeightMm = viewHeight, RotationDegrees = rotation,
                    TitleBlockLines = TitleBlockPreviewService.Read(document, sheet)
                };
                string folder = Path.Combine(imageFolder, plan.Id.IntegerValue.ToString());
                Directory.CreateDirectory(folder);
                try
                {
                    using (var options = new ImageExportOptions())
                    {
                        options.ExportRange = ExportRange.SetOfViews;
                        options.SetViewsAndSheets(new List<ElementId> { plan.Id });
                        options.FilePath = Path.Combine(folder, "Plan");
                        options.HLRandWFViewsFileType = ImageFileType.PNG;
                        options.ShadowViewsFileType = ImageFileType.PNG;
                        options.ZoomType = ZoomFitType.FitToPage;
                        options.PixelSize = 1200;
                        document.ExportImage(options);
                    }
                    preview.ImagePath = Directory.GetFiles(folder, "*.png").FirstOrDefault();
                    if (preview.ImagePath == null) preview.ImageWarning = "Không có ảnh export; preview chỉ hiển thị khung viewport.";
                }
                catch (Exception ex) { preview.ImageWarning = "Không export được ảnh; dùng khung viewport. " + ex.Message; }
                result.Add(preview);
            }
            return result;
        }
    }
}
