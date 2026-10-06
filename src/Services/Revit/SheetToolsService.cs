using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace MEP_Sheet_Manager
{
    internal static class SheetToolsService
    {
        public static void Change(Document doc, string name, Action action)
        {
            if (doc.IsReadOnly) throw new InvalidOperationException("Project đang chỉ đọc.");
            using (var tx = new Transaction(doc, name)) {
                if (tx.Start() != TransactionStatus.Started) throw new InvalidOperationException("Không bắt đầu được thao tác.");
                var options = tx.GetFailureHandlingOptions();
                options.SetFailuresPreprocessor(new RollbackErrors()); options.SetClearAfterRollback(true);
                tx.SetFailureHandlingOptions(options);
                try { action(); if (tx.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Revit không lưu được thao tác; đã hoàn tác."); }
                catch { if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack(); throw; }
            }
        }
        private sealed class RollbackErrors : IFailuresPreprocessor
        {
            public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor) {
                return accessor.GetFailureMessages().Any(f => f.GetSeverity() == FailureSeverity.Error)
                    ? FailureProcessingResult.ProceedWithRollBack : FailureProcessingResult.Continue;
            }
        }
        public static List<ViewSheet> Resolve(Document doc, IList<SheetInfo> rows)
        {
            var sheets = rows.Select(r => doc.GetElement(new ElementId(r.SheetId)) as ViewSheet).ToList();
            if (sheets.Any(s => s == null) || rows.Where((r, i) => r.SheetUniqueId != sheets[i].UniqueId).Any())
                throw new InvalidOperationException("Sheet đã thay đổi. Cập nhật và chọn lại.");
            return sheets;
        }
        public static void Rename(Document doc, IList<SheetInfo> rows, IList<string> values, bool number)
        {
            var sheets = Resolve(doc, rows);
            if (values.Count != rows.Count || values.Any(v => string.IsNullOrWhiteSpace(v) || !NamingUtils.IsValidName(v)))
                throw new InvalidOperationException("Tên/số sheet không hợp lệ.");
            if (number) {
                var targetIds = new HashSet<int>(rows.Select(r => r.SheetId));
                var all = SheetService.Read(doc).Where(r => !targetIds.Contains(r.SheetId)).Select(r => r.Number).Concat(values);
                if (all.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                    throw new InvalidOperationException("Sheet Number sau đổi bị trùng. Chưa thay đổi project.");
            }
            Change(doc, "Rename selected sheets", () => {
                if (number) foreach (var sheet in sheets) sheet.SheetNumber = "TMP-" + Guid.NewGuid().ToString("N");
                for (int i = 0; i < sheets.Count; i++) { if (number) sheets[i].SheetNumber = values[i]; else sheets[i].Name = values[i]; }
            });
        }
        public static void DuplicateEmpty(Document doc, IList<SheetInfo> rows)
        {
            var sheets = Resolve(doc, rows);
            var used = new HashSet<string>(SheetService.Read(doc).Select(r => r.Number), StringComparer.OrdinalIgnoreCase);
            Change(doc, "Duplicate empty sheets", () => {
                foreach (var source in sheets) {
                    ElementId type = ElementId.InvalidElementId;
                    if (!source.IsPlaceholder) using (var c = new FilteredElementCollector(doc, source.Id))
                        type = c.OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsNotElementType().Select(e => e.GetTypeId()).FirstOrDefault() ?? type;
                    var copy = source.IsPlaceholder ? ViewSheet.CreatePlaceholder(doc) : ViewSheet.Create(doc, type);
                    int n = 1; string number; do { number = source.SheetNumber + "-COPY" + n++; } while (!used.Add(number));
                    copy.SheetNumber = number; copy.Name = source.Name;
                }
            });
        }
        public static void ChangeTitleBlock(Document doc, IList<SheetInfo> rows, ElementId type)
        {
            var sheets = Resolve(doc, rows);
            if (!(doc.GetElement(type) is FamilySymbol)) throw new InvalidOperationException("Chọn title block hợp lệ.");
            if (sheets.Any(s => s.IsPlaceholder)) throw new InvalidOperationException("Placeholder không có title block; bỏ khỏi lựa chọn.");
            Change(doc, "Change sheet title blocks", () => {
                foreach (var sheet in sheets) using (var c = new FilteredElementCollector(doc, sheet.Id)) {
                    var blocks = c.OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsNotElementType().ToList();
                    if (blocks.Count == 0) throw new InvalidOperationException(sheet.SheetNumber + " chưa có title block.");
                    foreach (var block in blocks) { if (block.Pinned) throw new InvalidOperationException(sheet.SheetNumber + ": title block đang pin."); block.ChangeTypeId(type); }
                }
            });
        }
        public static List<EditableParameterInfo> Parameters(Document doc, IList<SheetInfo> rows)
        {
            var sheets = Resolve(doc, rows);
            Func<Parameter, bool> valid = p => !p.IsReadOnly && p.StorageType != StorageType.ElementId && p.StorageType != StorageType.None
                && p.Id.IntegerValue != (int)BuiltInParameter.SHEET_NUMBER && p.Id.IntegerValue != (int)BuiltInParameter.SHEET_NAME;
            return sheets[0].Parameters.Cast<Parameter>().Where(valid).Where(p => sheets.All(s => s.Parameters.Cast<Parameter>()
                .Any(q => q.Id == p.Id && valid(q) && q.StorageType == p.StorageType)))
                .OrderBy(p => p.Definition.Name).Select(p => new EditableParameterInfo { Id = p.Id.IntegerValue,
                    Label = p.Definition.Name + " [" + p.StorageType + "]", Value = p.StorageType == StorageType.String ? p.AsString() ?? "" :
                        p.StorageType == StorageType.Integer ? p.AsInteger().ToString(System.Globalization.CultureInfo.InvariantCulture) : p.AsValueString() ?? "" }).ToList();
        }
        public static void SetParameter(Document doc, IList<SheetInfo> rows, int id, string value)
        {
            var sheets = Resolve(doc, rows);
            Change(doc, "Edit sheet parameters", () => {
                foreach (var s in sheets) {
                    var p = s.Parameters.Cast<Parameter>().SingleOrDefault(q => q.Id.IntegerValue == id);
                    if (p == null || p.IsReadOnly) throw new InvalidOperationException(s.SheetNumber + ": parameter không thể sửa.");
                    bool ok;
                    if (p.StorageType == StorageType.String) ok = p.Set(value);
                    else if (p.StorageType == StorageType.Integer) { int n; if (!int.TryParse(value, out n)) throw new InvalidOperationException("Nhập số nguyên (Yes/No: 1/0)."); ok = p.Set(n); }
                    else if (p.StorageType == StorageType.Double) ok = p.SetValueString(value);
                    else throw new InvalidOperationException("Kiểu parameter không hỗ trợ.");
                    if (!ok) throw new InvalidOperationException(s.SheetNumber + ": giá trị parameter không hợp lệ.");
                }
            });
        }
        public static List<SheetSetInfo> ReadSets(Document doc)
        {
            using (var c = new FilteredElementCollector(doc)) return c.OfClass(typeof(ViewSheetSet)).Cast<ViewSheetSet>()
                .OrderBy(s => s.Name).Select(s => new SheetSetInfo { Id = s.Id.IntegerValue, Name = s.Name,
                    SheetIds = new HashSet<int>(s.Views.Cast<View>().Select(v => v.Id.IntegerValue)) }).ToList();
        }
        public static void SaveSet(Document doc, IList<SheetInfo> rows, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || !NamingUtils.IsValidName(name)) throw new InvalidOperationException("Tên V/S Set không hợp lệ.");
            if (ReadSets(doc).Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Tên V/S Set đã tồn tại; chọn tên mới.");
            var sheets = Resolve(doc, rows);
            if (sheets.Any(s => s.IsPlaceholder)) throw new InvalidOperationException("V/S Set chỉ nhận sheet thường.");
            var manager = doc.PrintManager; var oldRange = manager.PrintRange;
            try {
                manager.PrintRange = PrintRange.Select;
                var setting = manager.ViewSheetSetting; var previous = setting.CurrentViewSheetSet; var previousViews = setting.InSession.Views;
                try { Change(doc, "Save view sheet set", () => {
                    var views = new ViewSet(); foreach (var s in sheets) views.Insert(s);
                    setting.CurrentViewSheetSet = setting.InSession; setting.CurrentViewSheetSet.Views = views;
                    if (!setting.SaveAs(name)) throw new InvalidOperationException("Không lưu được V/S Set.");
                }); } finally { setting.InSession.Views = previousViews; setting.CurrentViewSheetSet = previous; }
            } finally { manager.PrintRange = oldRange; }
        }
        public static List<SheetRevisionRow> ReadSheetRevisions(Document doc)
        {
            List<Revision> revisions;
            using (var c = new FilteredElementCollector(doc)) revisions = c.OfClass(typeof(Revision)).Cast<Revision>().OrderBy(r => r.SequenceNumber).ToList();
            using (var c = new FilteredElementCollector(doc)) return c.OfClass(typeof(ViewSheet)).Cast<ViewSheet>().OrderBy(s => s.SheetNumber)
                .Select(s => {
                    var additional = new HashSet<ElementId>(s.GetAdditionalRevisionIds());
                    var all = new HashSet<ElementId>(s.GetAllRevisionIds());
                    var current = doc.GetElement(s.GetCurrentRevision()) as Revision;
                    return new SheetRevisionRow { SheetId = s.Id.IntegerValue, UniqueId = s.UniqueId, Number = s.SheetNumber, Name = s.Name,
                        InitialAdditionalIds = new HashSet<int>(additional.Select(id => id.IntegerValue)),
                        InitialCloudIds = new HashSet<int>(all.Except(additional).Select(id => id.IntegerValue)),
                        CurrentNumber = current == null ? "" : s.GetRevisionNumberOnSheet(current.Id),
                        CurrentDate = current?.RevisionDate ?? "", CurrentDescription = current?.Description ?? "",
                        Revisions = revisions.Select(r => new RevisionChoice { Id = r.Id.IntegerValue,
                            Key = RevisionKey(r), Label = r.SequenceNumber + " · " + r.Description + (all.Contains(r.Id) && !additional.Contains(r.Id) ? " (cloud)" : ""),
                            Selected = all.Contains(r.Id), CanEdit = !all.Contains(r.Id) || additional.Contains(r.Id) }).ToList() };
                }).ToList();
        }
        public static string RevisionKey(Revision r) { return r.SequenceNumber + " | " + r.RevisionDate + " | " + r.Description; }
        public static void ApplyRevisions(Document doc, IList<SheetRevisionRow> rows)
        {
            var modified = rows.Where(r => r.Modified).ToList();
            if (modified.Count == 0) return;
            foreach (var row in modified) {
                var sheet = doc.GetElement(new ElementId(row.SheetId)) as ViewSheet;
                if (sheet == null || sheet.UniqueId != row.UniqueId || !row.InitialAdditionalIds.SetEquals(sheet.GetAdditionalRevisionIds().Select(id => id.IntegerValue))
                    || !row.InitialCloudIds.SetEquals(sheet.GetAllRevisionIds().Except(sheet.GetAdditionalRevisionIds()).Select(id => id.IntegerValue)))
                    throw new InvalidOperationException("Revision trên sheet đã thay đổi bên ngoài tool: " + row.Number + ". Cập nhật rồi chọn lại.");
                foreach (var r in row.Revisions.Where(r => r.Selected && r.CanEdit)) {
                    var revision = doc.GetElement(new ElementId(r.Id)) as Revision;
                    if (revision == null || RevisionKey(revision) != r.Key) throw new InvalidOperationException("Revision đã đổi hoặc bị xóa. Cập nhật rồi chọn lại.");
                }
            }
            Change(doc, "Apply sheet revisions", () => {
                foreach (var row in modified) {
                    var sheet = doc.GetElement(new ElementId(row.SheetId)) as ViewSheet;
                    if (sheet == null || sheet.UniqueId != row.UniqueId) throw new InvalidOperationException("Sheet đã thay đổi: " + row.Number);
                    var cloudIds = sheet.GetAllRevisionIds().Except(sheet.GetAdditionalRevisionIds());
                    sheet.SetAdditionalRevisionIds(row.Revisions.Where(r => r.Selected && r.CanEdit).Select(r => new ElementId(r.Id)).Except(cloudIds).ToList());
                }
            });
        }
        public static List<PlanManagerInfo> ReadPlans(Document doc)
        {
            var views = ProjectListService.ReadViews(doc).ToDictionary(v => v.Name, v => v);
            using (var c = new FilteredElementCollector(doc)) return c.OfClass(typeof(ViewPlan)).Cast<ViewPlan>()
                .Where(v => !v.IsTemplate && v.ViewType == ViewType.FloorPlan).OrderBy(v => v.Name)
                .Select(v => new PlanManagerInfo { Id = v.Id.IntegerValue, UniqueId = v.UniqueId, Name = v.Name, Scale = v.Scale,
                    Template = (doc.GetElement(v.ViewTemplateId) as View)?.Name ?? "<None>", Sheets = views[v.Name].SheetNumbers }).ToList();
        }
        public static void EditPlan(Document doc, PlanManagerInfo row, string name, int scale, int templateId)
        {
            var plan = doc.GetElement(new ElementId(row.Id)) as ViewPlan;
            if (plan == null || plan.UniqueId != row.UniqueId) throw new InvalidOperationException("View đã thay đổi.");
            if (scale < 1 || !NamingUtils.IsValidName(name) || string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Tên hoặc scale không hợp lệ.");
            Change(doc, "Edit floor plan", () => {
                plan.Name = name;
                plan.ViewTemplateId = new ElementId(templateId);
                var template = doc.GetElement(plan.ViewTemplateId) as View;
                if (template != null && template.GetTemplateParameterIds().Except(template.GetNonControlledTemplateParameterIds())
                    .Contains(new ElementId(BuiltInParameter.VIEW_SCALE)) && plan.Scale != scale)
                    throw new InvalidOperationException("Scale được điều khiển bởi template. Sửa template hoặc nhập đúng scale của template.");
                if (plan.Scale != scale) plan.Scale = scale;
            });
        }
        public static void DuplicatePlan(Document doc, PlanManagerInfo row, ViewDuplicateOption option)
        {
            var plan = doc.GetElement(new ElementId(row.Id)) as ViewPlan;
            if (plan == null || plan.UniqueId != row.UniqueId || !plan.CanViewBeDuplicated(option)) throw new InvalidOperationException("View không thể duplicate theo kiểu đã chọn.");
            Change(doc, "Duplicate floor plan", () => plan.Duplicate(option));
        }
    }
}
