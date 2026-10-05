using System;
namespace MEP_Sheet_Manager
{
    public sealed class SheetBrowserInfo
    {
        public bool Visible { get; set; }
        public string Path { get; set; }
    }
    public sealed class BrowserFilterOption
    {
        public string Label { get; set; }
        public string Path { get; set; }
        public bool UseBrowser { get; set; }
    }
    public static class SheetListFilter
    {
        public static bool Matches(string number, string name, string search, BrowserFilterOption filter, SheetBrowserInfo browser)
        {
            search = (search ?? "").Trim();
            if (search.Length > 0 && (number ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                && (name ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) return false;
            return filter == null || !filter.UseBrowser || (browser != null && browser.Visible
                && (filter.Path == null || string.Equals(filter.Path, browser.Path, StringComparison.Ordinal)));
        }
    }
}
