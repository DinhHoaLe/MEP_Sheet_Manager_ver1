using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace MEP_Sheet_Manager
{
    internal static class ProjectListService
    {
        public static List<ViewInfo> ReadViews(Document document)
        {
            var placements = new Dictionary<ElementId, List<string>>();
            using (var collector = new FilteredElementCollector(document))
                foreach (var viewport in collector.OfClass(typeof(Viewport)).Cast<Viewport>())
                {
                    var sheet = document.GetElement(viewport.SheetId) as ViewSheet;
                    if (sheet == null) continue;
                    List<string> numbers;
                    if (!placements.TryGetValue(viewport.ViewId, out numbers))
                        placements[viewport.ViewId] = numbers = new List<string>();
                    numbers.Add(sheet.SheetNumber);
                }
            using (var collector = new FilteredElementCollector(document))
                foreach (var instance in collector.OfClass(typeof(ScheduleSheetInstance)).Cast<ScheduleSheetInstance>())
                {
                    var sheet = document.GetElement(instance.OwnerViewId) as ViewSheet;
                    if (sheet == null || instance.IsTitleblockRevisionSchedule) continue;
                    List<string> numbers;
                    if (!placements.TryGetValue(instance.ScheduleId, out numbers))
                        placements[instance.ScheduleId] = numbers = new List<string>();
                    numbers.Add(sheet.SheetNumber);
                }
            using (var collector = new FilteredElementCollector(document))
                return collector.OfClass(typeof(View)).Cast<View>()
                    .Where(v => !v.IsTemplate && !(v is ViewSheet)
                        && v.ViewType != ViewType.Internal && v.ViewType != ViewType.Undefined
                        && v.ViewType != ViewType.ProjectBrowser && v.ViewType != ViewType.SystemBrowser)
                    .Select(v => new ViewInfo { Name = v.Name, Type = v.ViewType.ToString(), Scale = v.Scale,
                        SheetNumbers = placements.ContainsKey(v.Id)
                            ? string.Join(", ", placements[v.Id].Distinct().OrderBy(n => n, StringComparer.OrdinalIgnoreCase)) : "" })
                    .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        public static List<RevisionInfo> ReadRevisions(Document document)
        {
            using (var collector = new FilteredElementCollector(document))
                return collector.OfClass(typeof(Revision)).Cast<Revision>()
                    .OrderBy(r => r.SequenceNumber)
                    .Select(r => new RevisionInfo { Sequence = r.SequenceNumber, Description = r.Description,
                        Date = r.RevisionDate, Issued = r.Issued }).ToList();
        }
    }
}
