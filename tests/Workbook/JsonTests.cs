using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MEP_Sheet_Manager;

static class JsonTests
{
    private static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS: " + label); }
    private static bool Reject(string path, string text)
    {
        File.WriteAllText(path, text);
        try { SheetJsonFile.Read(path); return false; } catch { return true; }
    }
    public static void Run(string folder)
    {
        string path = Path.Combine(folder, "SheetsRoundTrip.json");
        var rows = new List<SheetInfo> { new SheetInfo("001.01", "Điện & nước \"Tầng 1\"\nZone A", false),
            new SheetInfo("A-002", "Tên sheet trùng", true), new SheetInfo("A-003", "Tên sheet trùng", false) };
        SheetJsonFile.Write(path, rows);
        var back = SheetJsonFile.Read(path);
        Check(back.Count == 3 && back.Select(r => r.Number).SequenceEqual(rows.Select(r => r.Number)), "JSON preserves leading zeros and exact sheet numbers as strings");
        Check(back.Select(r => r.Name).SequenceEqual(rows.Select(r => r.Name)) && back[1].IsPlaceholder && !back[0].IsPlaceholder,
            "JSON preserves Vietnamese, quotes, newlines, duplicate names and editable placeholder flags");
        SheetJsonFile.Write(path, rows.Take(1).ToList());
        Check(SheetJsonFile.Read(path).Count == 1, "JSON atomically overwrites an existing export");
        SheetJsonFile.Write(path, new List<SheetInfo>());
        Check(SheetJsonFile.Read(path).Count == 0, "JSON supports empty project sheet list");
        string bad = Path.Combine(folder, "InvalidSheets.json");
        Check(Reject(bad, "{\"version\":2,\"sheets\":[]}"), "JSON rejects unsupported versions");
        Check(Reject(bad, "{\"version\":1,\"sheets\":[{\"sheetNumber\":\"A1\",\"sheetName\":\"Test\"}]}"), "JSON requires explicit placeholder flag");
        Check(Reject(bad, "{\"version\":1,\"sheets\":[null]}"), "JSON rejects null sheet rows");
        SheetJsonFile.Write(path, rows);
        string excel = Path.Combine(folder, "JsonToExcel.xlsx"); SheetWorkbook.Write(excel, SheetJsonFile.Read(path));
        var converted = SheetWorkbook.Read(excel);
        Check(converted.Count == rows.Count && converted[0].Number == "001.01" && converted[1].IsPlaceholder,
            "manual/imported JSON sheet data exports to Excel with equivalent numbers and placeholders");
    }
}
