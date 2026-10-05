using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
namespace MEP_Sheet_Manager
{
    internal static class SheetBrowserService
    {
        public static Dictionary<int, SheetBrowserInfo> Read(Document document)
        {
            var rows = new Dictionary<int, SheetBrowserInfo>();
            var organization = BrowserOrganization.GetCurrentBrowserOrganizationForSheets(document);
            using (var collector = new FilteredElementCollector(document))
                foreach (var sheet in collector.OfClass(typeof(ViewSheet)).Cast<ViewSheet>())
                {
                    bool visible = organization == null || organization.AreFiltersSatisfied(sheet.Id);
                    string path = "";
                    if (visible && organization != null)
                    {
                        var folders = organization.GetFolderItems(sheet.Id);
                        path = string.Join(" / ", folders.Select(f => f.Name));
                        foreach (var folder in folders) folder.Dispose();
                    }
                    rows.Add(sheet.Id.IntegerValue, new SheetBrowserInfo { Visible = visible, Path = path });
                }
            return rows;
        }
    }
}
