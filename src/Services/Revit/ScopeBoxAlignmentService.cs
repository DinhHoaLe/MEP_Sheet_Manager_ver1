using System;
using Autodesk.Revit.DB;

namespace MEP_Sheet_Manager
{
    internal static class ScopeBoxAlignmentService
    {
        public static Element Scope(Document document, ViewPlan plan)
        {
            var parameter = plan.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP);
            var scope = parameter == null ? null : document.GetElement(parameter.AsElementId());
            return scope != null && scope.Category != null && scope.Category.Id.IntegerValue == (int)BuiltInCategory.OST_VolumeOfInterest
                ? scope : null;
        }

        public static XYZ PaperCenter(ViewSheet sheet)
        {
            var bounds = sheet.Outline;
            double left = UnitUtils.ConvertToInternalUnits(SheetLayoutMargins.LeftMm, UnitTypeId.Millimeters);
            double right = UnitUtils.ConvertToInternalUnits(SheetLayoutMargins.RightMm, UnitTypeId.Millimeters);
            if (bounds.Max.U - bounds.Min.U <= left + right || bounds.Max.V <= bounds.Min.V)
                throw new InvalidOperationException("Không xác định được vùng giấy của sheet " + sheet.SheetNumber + ". Hãy thêm title block phù hợp.");
            return new XYZ((bounds.Min.U + left + bounds.Max.U - right) / 2, (bounds.Min.V + bounds.Max.V) / 2, 0);
        }

        public static XYZ AlignedBoxCenter(Document document, ViewPlan plan, ViewSheet sheet, Viewport viewport)
        {
            var scope = Scope(document, plan);
            var target = PaperCenter(sheet);
            var center = viewport.GetBoxCenter();
            if (scope == null)
                return new XYZ(center.X + target.X - center.X, center.Y + target.Y - center.Y, 0);
            var bounds = scope.get_BoundingBox(null);
            if (bounds == null) throw new InvalidOperationException("Không đọc được hình học Scope Box: " + scope.Name);
            var modelCenter = bounds.Transform.OfPoint((bounds.Min + bounds.Max) / 2);
            var transforms = plan.GetModelToProjectionTransforms();
            if (transforms.Count != 1)
                throw new InvalidOperationException("View '" + plan.Name + "' có split crop; chưa hỗ trợ canh một tâm Scope Box cho nhiều vùng crop.");
            var projection = transforms[0].GetModelToProjectionTransform().OfPoint(modelCenter);
            var onSheet = viewport.GetProjectionToSheetTransform().OfPoint(projection);
            return new XYZ(center.X + target.X - onSheet.X, center.Y + target.Y - onSheet.Y, 0);
        }
    }
}
