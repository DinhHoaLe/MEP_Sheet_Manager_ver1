using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
namespace MEP_Sheet_Manager
{
    internal static class SheetService
    {
        public static List<SheetInfo> Read(Document doc) {
            using (var c = new FilteredElementCollector(doc)) return c.OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                .Select(s => new SheetInfo(s.SheetNumber,s.Name,s.IsPlaceholder) { SheetId=s.Id.IntegerValue, SheetUniqueId=s.UniqueId }).OrderBy(s=>s.Number,StringComparer.OrdinalIgnoreCase).ToList();
        }
        public static int Validate(Document doc, IList<SheetInfo> rows) {
            var existing = new HashSet<string>(Read(doc).Select(s=>s.Number),StringComparer.OrdinalIgnoreCase);
            var counts = rows.GroupBy(s=>s.Number ?? "",StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.Count(),StringComparer.OrdinalIgnoreCase);
            int errors=0;
            foreach (var r in rows) {
                ValidateRow(r, existing, counts);
                if (r.Status.StartsWith("Lỗi:")) errors++;
            }
            return errors;
        }
        public static void ValidateRow(SheetInfo row, HashSet<string> existing, Dictionary<string, int> counts)
        {
            if (string.IsNullOrWhiteSpace(row.Number) || string.IsNullOrWhiteSpace(row.Name)) row.Status="Lỗi: thiếu số sheet hoặc tên sheet";
            else if (counts[row.Number]>1) row.Status="Lỗi: số sheet trùng trong dữ liệu";
            else if (!NamingUtils.IsValidName(row.Number) || !NamingUtils.IsValidName(row.Name)) row.Status="Lỗi: ký tự không hợp lệ trong Revit";
            else if (existing.Contains(row.Number)) row.Status="Bỏ qua: đã có trong project";
            else row.Status="Sẵn sàng";
        }
        public static int Create(Document doc, IList<SheetInfo> rows, ElementId titleBlock) {
            if (doc.IsReadOnly) throw new InvalidOperationException("Project đang ở chế độ chỉ đọc.");
            if (Validate(doc,rows)!=0) throw new InvalidOperationException("Sửa các dòng lỗi trong danh sách rồi thử lại.");
            var pending=rows.Where(r=>r.Status=="Sẵn sàng").ToList();
            if (pending.Count==0) return 0;
            using (var tx=new Transaction(doc,"Create sheets from sheet data")) {
                if(tx.Start()!=TransactionStatus.Started) throw new InvalidOperationException("Không thể bắt đầu transaction.");
                try {
                    foreach(var r in pending) {
                        var s=r.IsPlaceholder ? ViewSheet.CreatePlaceholder(doc) : ViewSheet.Create(doc,titleBlock);
                        s.SheetNumber=r.Number; s.Name=r.Name;
                    }
                    if(tx.Commit()!=TransactionStatus.Committed) throw new InvalidOperationException("Revit không commit được dữ liệu.");
                } catch { if(tx.GetStatus()==TransactionStatus.Started) tx.RollBack(); throw; }
            }
            return pending.Count;
        }
        public static int Delete(Document doc, IList<SheetInfo> rows)
        {
            if (doc.IsReadOnly) throw new InvalidOperationException("Project đang chỉ đọc.");
            if (rows.Count == 0) return 0;
            var sheets = rows.Select(r => doc.GetElement(new ElementId(r.SheetId)) as ViewSheet).ToList();
            if (sheets.Any(s => s == null) || rows.Where((r, i) => sheets[i].UniqueId != r.SheetUniqueId).Any())
                throw new InvalidOperationException("Danh sách sheet đã thay đổi. Cập nhật và chọn lại.");
            if (doc.ActiveView != null && sheets.Any(s => s.Id == doc.ActiveView.Id))
                throw new InvalidOperationException("Có sheet đang mở trong Revit. Chuyển sang view khác trước khi xóa.");
            using (var tx = new Transaction(doc, "Delete selected sheets"))
            {
                if (tx.Start() != TransactionStatus.Started) throw new InvalidOperationException("Không bắt đầu được transaction xóa sheet.");
                try { doc.Delete(sheets.Select(s => s.Id).Distinct().ToList());
                    if (tx.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Không commit được thao tác xóa sheet."); }
                catch { if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack(); throw; }
            }
            return sheets.Count;
        }
    }
}


