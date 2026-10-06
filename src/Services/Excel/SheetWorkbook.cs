using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace MEP_Sheet_Manager
{
    // Open XML workbook, no Excel installation or third-party DLL required.
    public static class SheetWorkbook
    {
        private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static XElement Cell(string address, string value, int style = 0)
        {
            return new XElement(S + "c", new XAttribute("r", address), new XAttribute("t", "inlineStr"),
                new XAttribute("s", style), new XElement(S + "is", new XElement(S + "t",
                new XAttribute(XNamespace.Xml + "space", "preserve"), value ?? "")));
        }
        private static void Put(ZipArchive zip, string path, string xml)
        {
            using (var writer = new StreamWriter(zip.CreateEntry(path).Open())) writer.Write(xml);
        }
        public static void Write(string path, IList<SheetInfo> sheets)
        {
            // Write to a temporary file first so a failed export cannot truncate an existing workbook.
            string temp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), Guid.NewGuid() + ".tmp");
            try
            {
                using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
                {
                    Put(zip, "[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>");
                    Put(zip, "_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                    Put(zip, "xl/workbook.xml", new XElement(S + "workbook", new XAttribute(XNamespace.Xmlns + "r", R),
                        new XElement(S + "sheets", new XElement(S + "sheet", new XAttribute("name", "Sheets"), new XAttribute("sheetId", 1), new XAttribute(R + "id", "rId1")))).ToString());
                    Put(zip, "xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
                    Put(zip, "xl/styles.xml", "<styleSheet xmlns=\"" + S + "\"><fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font></fonts><fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF62543C\"/><bgColor indexed=\"64\"/></patternFill></fill></fills><borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf/></cellStyleXfs><cellXfs count=\"2\"><xf numFmtId=\"49\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/><xf numFmtId=\"49\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/></cellXfs><cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>");
                    var data = new XElement(S + "sheetData", new XElement(S + "row", new XAttribute("r", 1), new XAttribute("ht", 26), new XAttribute("customHeight", 1),
                        Cell("A1", "SheetNumber", 1), Cell("B1", "SheetName", 1), Cell("C1", "IsPlaceholder", 1)));
                    for (int i = 0; i < sheets.Count; i++)
                    {
                        int n = i + 2;
                        data.Add(new XElement(S + "row", new XAttribute("r", n), Cell("A" + n, sheets[i].Number), Cell("B" + n, sheets[i].Name), Cell("C" + n, sheets[i].IsPlaceholder ? "TRUE" : "FALSE")));
                    }
                    var sheet = new XElement(S + "worksheet",
                        new XElement(S + "sheetViews", new XElement(S + "sheetView", new XAttribute("workbookViewId", 0), new XElement(S + "pane", new XAttribute("ySplit", 1), new XAttribute("topLeftCell", "A2"), new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")))),
                        new XElement(S + "cols", new XElement(S + "col", new XAttribute("min", 1), new XAttribute("max", 1), new XAttribute("width", 42), new XAttribute("customWidth", 1)),
                            new XElement(S + "col", new XAttribute("min", 2), new XAttribute("max", 2), new XAttribute("width", 95), new XAttribute("customWidth", 1)),
                            new XElement(S + "col", new XAttribute("min", 3), new XAttribute("max", 3), new XAttribute("width", 18), new XAttribute("customWidth", 1))), data,
                        new XElement(S + "autoFilter", new XAttribute("ref", "A1:C" + (sheets.Count + 1))));
                    Put(zip, "xl/worksheets/sheet1.xml", sheet.ToString());
                }
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        internal static XDocument Load(ZipArchive zip, string path)
        {
            var entry = zip.GetEntry(path);
            if (entry == null) throw new InvalidDataException("Thiếu thành phần Excel: " + path);
            using (var stream = entry.Open())
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 20000000 }))
                return XDocument.Load(reader);
        }
        public static List<SheetInfo> Read(string path)
        {
            using (var zip = ZipFile.OpenRead(path))
            {
                var book = Load(zip, "xl/workbook.xml");
                var tabs = book.Descendants(S + "sheet").ToList();
                var tab = tabs.FirstOrDefault(s => (string)s.Attribute("name") == "Sheets") ?? tabs.FirstOrDefault();
                if (tab == null) throw new InvalidDataException("Excel không có worksheet.");
                var rel = Load(zip, "xl/_rels/workbook.xml.rels").Root.Elements().FirstOrDefault(e => (string)e.Attribute("Id") == (string)tab.Attribute(R + "id"));
                if (rel == null || (string)rel.Attribute("TargetMode") == "External") throw new InvalidDataException("Worksheet không hợp lệ.");
                var target = (string)rel.Attribute("Target");
                var sheetPath = new Uri(new Uri("http://xlsx/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
                var shared = zip.GetEntry("xl/sharedStrings.xml") == null ? new List<string>() : Load(zip, "xl/sharedStrings.xml").Descendants(S + "si").Select(e => string.Concat(e.Descendants(S + "t").Select(t => t.Value))).ToList();
                var xmlRows = Load(zip, sheetPath).Descendants(S + "sheetData").Elements(S + "row").ToList();
                if (xmlRows.Count == 0) throw new InvalidDataException("Excel không có header.");
                var headers = xmlRows[0].Elements(S + "c").ToDictionary(c => Column(c), c => Value(c, shared).Trim(), StringComparer.OrdinalIgnoreCase);
                string number = headers.FirstOrDefault(h => h.Value.Equals("SheetNumber", StringComparison.OrdinalIgnoreCase)).Key;
                string name = headers.FirstOrDefault(h => h.Value.Equals("SheetName", StringComparison.OrdinalIgnoreCase)).Key;
                string placeholder = headers.FirstOrDefault(h => h.Value.Equals("IsPlaceholder", StringComparison.OrdinalIgnoreCase)).Key;
                if (number == null || name == null) throw new InvalidDataException("Header bắt buộc: SheetNumber và SheetName (dòng đầu tiên).");
                var result = new List<SheetInfo>();
                foreach (var row in xmlRows.Skip(1))
                {
                    var cells = row.Elements(S + "c").ToDictionary(c => Column(c));
                    Func<string, string> get = col => col != null && cells.ContainsKey(col) ? Value(cells[col], shared) : "";
                    string n = get(number), title = get(name), p = get(placeholder).Trim();
                    if (string.IsNullOrWhiteSpace(n) && string.IsNullOrWhiteSpace(title) && p == "") continue;
                    int rowIndex = (int?)row.Attribute("r") ?? result.Count + 2;
                    bool isPlaceholder;
                    if (p == "" || p == "0") isPlaceholder = false;
                    else if (p == "1") isPlaceholder = true;
                    else if (!bool.TryParse(p, out isPlaceholder)) throw new InvalidDataException("IsPlaceholder phải là TRUE/FALSE tại dòng " + rowIndex);
                    result.Add(new SheetInfo(n, title, isPlaceholder) { ExcelRow = rowIndex });
                }
                return result;
            }
        }
        internal static string Column(XElement cell)
        { return new string(((string)cell.Attribute("r") ?? "").TakeWhile(char.IsLetter).ToArray()); }
        internal static string Value(XElement cell, List<string> shared)
        {
            if (cell.Element(S + "f") != null) throw new InvalidDataException("Dùng giá trị text, không dùng công thức tại ô " + (string)cell.Attribute("r"));
            string type = (string)cell.Attribute("t"), value = (string)cell.Element(S + "v") ?? "";
            if (type == "inlineStr") return string.Concat(cell.Descendants(S + "t").Select(t => t.Value));
            if (type == "s") return shared[int.Parse(value)];
            if (type == "b") return value == "1" ? "TRUE" : "FALSE";
            if (type == "e") throw new InvalidDataException("Ô Excel có lỗi: " + (string)cell.Attribute("r"));
            return value;
        }
    }
}
