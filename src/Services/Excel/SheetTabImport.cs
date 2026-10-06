using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MEP_Sheet_Manager
{
    public static class SheetTabImport
    {
        private static void ValidateRows(TabularData data) {
            if (data.Rows.Any(r => string.IsNullOrWhiteSpace(r[0])) || data.Rows.GroupBy(r => r[0], StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                throw new InvalidDataException("Sheet Number thiếu hoặc trùng trong file.");
        }
        public static void Views(TabularData data, IList<SheetViewAssignment> assignments)
        {
            ValidateRows(data);
            var choices = new Dictionary<SheetViewAssignment, FloorPlanChoice>();
            foreach (var record in data.Rows) {
                var row = assignments.SingleOrDefault(r => string.Equals(r.Number, record[0], StringComparison.OrdinalIgnoreCase));
                if (row == null) throw new InvalidDataException("Không tìm thấy sheet: " + record[0]);
                var options = row.AllFloorPlans ?? row.FloorPlans;
                var plan = string.IsNullOrEmpty(record[2]) ? options.First(p => p.Id < 1) : options.SingleOrDefault(p => p.Name == record[2]);
                if (plan == null) throw new InvalidDataException("Floor Plan không khả dụng cho " + record[0] + ": " + record[2]);
                choices.Add(row, plan);
            }
            if (assignments.Select(r => choices.ContainsKey(r) ? choices[r] : r.SelectedFloorPlan).Where(p => p != null && p.Id > 0)
                .GroupBy(p => p.Id).Any(g => g.Count() > 1)) throw new InvalidDataException("Một Floor Plan được chọn cho nhiều sheet.");
            foreach (var pair in choices) pair.Key.SelectedFloorPlan = pair.Value;
        }
        public static void Revisions(TabularData data, IList<SheetRevisionRow> assignments)
        {
            ValidateRows(data);
            var choices = new List<Tuple<SheetRevisionRow, string[]>>();
            foreach (var record in data.Rows) {
                var row = assignments.SingleOrDefault(r => string.Equals(r.Number, record[0], StringComparison.OrdinalIgnoreCase));
                if (row == null) throw new InvalidDataException("Không tìm thấy sheet: " + record[0]);
                var keys = TabularFile.Unpack(record[2]);
                if (keys.Any(k => !row.Revisions.Any(r => r.Key == k))) throw new InvalidDataException("Revision không khớp Sequence / Date / Description trên sheet " + record[0]);
                choices.Add(Tuple.Create(row, keys));
            }
            foreach (var pair in choices) { foreach (var revision in pair.Item1.Revisions.Where(r => r.CanEdit)) revision.Selected = pair.Item2.Contains(revision.Key); pair.Item1.NotifySelection(); }
        }
    }
}
