using System;
using System.Collections.Generic;
using MEP_Sheet_Manager;
static class SheetUiTests
{
    private static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS: " + label); }
    public static void Run()
    {
        var all = new BrowserFilterOption();
        var browser = new BrowserFilterOption { UseBrowser = true };
        var group = new BrowserFilterOption { UseBrowser = true, Path = "MEP / Electrical" };
        var info = new SheetBrowserInfo { Visible = true, Path = "MEP / Electrical" };
        Check(SheetListFilter.Matches("A101", "Điện tầng 1", " a101 ", all, null)
            && SheetListFilter.Matches("A101", "Điện tầng 1", "ĐIỆN", all, null), "search matches sheet number/name ignoring case and surrounding spaces");
        Check(!SheetListFilter.Matches("A101", "Điện tầng 1", "A102", all, info), "search rejects unmatched sheets");
        Check(SheetListFilter.Matches("A101", "Điện tầng 1", "", browser, info)
            && !SheetListFilter.Matches("A101", "Điện tầng 1", "", browser, new SheetBrowserInfo { Visible = false }), "Project Browser filter respects active organization visibility");
        Check(SheetListFilter.Matches("A101", "Điện tầng 1", "tầng", group, info)
            && !SheetListFilter.Matches("A101", "Điện tầng 1", "", group, new SheetBrowserInfo { Visible = true, Path = "MEP / Plumbing" }), "browser group and text search combine without mixing groups");
        var row = new SheetViewAssignment(); var events = new List<string>();
        Check(row.ProcessingStatus == "", "Step 2 status is blank before applying Floor Plans");
        row.PropertyChanged += (s, e) => { if (e.PropertyName == "ProcessingStatus") events.Add(row.ProcessingStatus); };
        row.ProcessingStatus = "Running"; row.ProcessingStatus = "Done";
        Check(events.Count == 2 && events[0] == "Running" && events[1] == "Done", "Running and Done notify bound row status immediately");
        row.SelectedFloorPlan = new FloorPlanChoice { Id = 10, Name = "Floor 1" };
        Check(row.ProcessingStatus == "", "changing Floor Plan clears previous placement result");
    }
}
