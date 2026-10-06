using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml.Linq;

namespace MEP_Sheet_Manager
{
    [DataContract]
    public sealed class TabularData
    {
        [DataMember(IsRequired = true)] public int Version = 1;
        [DataMember(IsRequired = true)] public string Kind;
        [DataMember(IsRequired = true)] public string[] Headers;
        [DataMember(IsRequired = true)] public List<string[]> Rows;
    }
    public static class TabularFile
    {
        private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        public static string Pack(string[] values) { using (var stream = new MemoryStream()) { new DataContractJsonSerializer(typeof(string[])).WriteObject(stream, values); return Encoding.UTF8.GetString(stream.ToArray()); } }
        public static string[] Unpack(string value) { using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(value))) {
            var values = new DataContractJsonSerializer(typeof(string[])).ReadObject(stream) as string[];
            if (values == null || values.Any(v => v == null) || values.Distinct().Count() != values.Length) throw new InvalidDataException("Danh sách revision không hợp lệ."); return values;
        } }
        private static string ColumnName(int n) { string text = ""; for (n++; n > 0; n = (n - 1) / 26) text = (char)('A' + (n - 1) % 26) + text; return text; }
        private static void Validate(TabularData data, string kind, string[] headers) {
            if (data == null || data.Version != 1 || data.Kind != kind || data.Headers == null || !data.Headers.SequenceEqual(headers)
                || data.Rows == null || data.Rows.Any(r => r == null || r.Length != headers.Length || r.Any(v => v == null)))
                throw new InvalidDataException("File không đúng loại/cấu trúc " + kind + ". Dùng file xuất từ tab tương ứng.");
        }
        public static void Write(string path, TabularData data)
        {
            Validate(data, data.Kind, data.Headers);
            string temp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), Guid.NewGuid() + ".tmp");
            try {
                if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase)) {
                    using (var stream = File.Create(temp)) new DataContractJsonSerializer(typeof(TabularData)).WriteObject(stream, data);
                } else {
                    SheetWorkbook.Write(temp, new List<SheetInfo>());
                    using (var zip = ZipFile.Open(temp, ZipArchiveMode.Update)) {
                        var old = zip.GetEntry("xl/worksheets/sheet1.xml"); old.Delete();
                        var rows = new XElement(S + "sheetData"); int index = 0;
                        foreach (var row in new[] { data.Headers }.Concat(data.Rows)) {
                            int n = ++index;
                            rows.Add(new XElement(S + "row", new XAttribute("r", n), row.Select((value, col) => new XElement(S + "c",
                                new XAttribute("r", ColumnName(col) + n), new XAttribute("t", "inlineStr"), new XAttribute("s", n == 1 ? 1 : 0),
                                new XElement(S + "is", new XElement(S + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value))))));
                        }
                        var sheet = new XElement(S + "worksheet", new XElement(S + "cols", Enumerable.Range(1, data.Headers.Length).Select(n =>
                            new XElement(S + "col", new XAttribute("min", n), new XAttribute("max", n), new XAttribute("width", n == 1 ? 28 : 45), new XAttribute("customWidth", 1)))), rows);
                        using (var writer = new StreamWriter(zip.CreateEntry("xl/worksheets/sheet1.xml").Open())) writer.Write(sheet);
                    }
                }
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static TabularData Read(string path, string kind, string[] headers)
        {
            TabularData data;
            if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase)) {
                using (var stream = File.OpenRead(path)) data = new DataContractJsonSerializer(typeof(TabularData)).ReadObject(stream) as TabularData;
            } else using (var zip = ZipFile.OpenRead(path)) {
                var book = SheetWorkbook.Load(zip, "xl/workbook.xml");
                XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                var tab = book.Descendants(S + "sheet").FirstOrDefault();
                if (tab == null) throw new InvalidDataException("File không có worksheet.");
                var link = SheetWorkbook.Load(zip, "xl/_rels/workbook.xml.rels").Root.Elements().SingleOrDefault(e => (string)e.Attribute("Id") == (string)tab.Attribute(r + "id"));
                if (link == null || (string)link.Attribute("TargetMode") == "External") throw new InvalidDataException("Worksheet không hợp lệ.");
                var sheetPath = new Uri(new Uri("http://xlsx/xl/workbook.xml"), (string)link.Attribute("Target")).AbsolutePath.TrimStart('/');
                var shared = zip.GetEntry("xl/sharedStrings.xml") == null ? new List<string>() : SheetWorkbook.Load(zip, "xl/sharedStrings.xml")
                    .Descendants(S + "si").Select(e => string.Concat(e.Descendants(S + "t").Select(t => t.Value))).ToList();
                var xmlRows = SheetWorkbook.Load(zip, sheetPath).Descendants(S + "sheetData").Elements(S + "row").ToList();
                if (xmlRows.Count == 0) throw new InvalidDataException("Thiếu header.");
                Func<XElement, string[]> read = row => {
                    var cells = row.Elements(S + "c").ToDictionary(c => SheetWorkbook.Column(c));
                    return Enumerable.Range(0, headers.Length).Select(i => cells.ContainsKey(ColumnName(i)) ? SheetWorkbook.Value(cells[ColumnName(i)], shared) : "").ToArray();
                };
                data = new TabularData { Kind = kind, Headers = read(xmlRows[0]), Rows = xmlRows.Skip(1).Select(read).Where(a => a.Any(v => v != "")).ToList() };
            }
            Validate(data, kind, headers); return data;
        }
    }
}
