using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MEP_Sheet_Manager
{
    public static class SheetJsonFile
    {
        [DataContract]
        private sealed class FileData
        {
            [DataMember(Name = "version", IsRequired = true)] public int Version { get; set; }
            [DataMember(Name = "sheets", IsRequired = true)] public List<RowData> Sheets { get; set; }
        }
        [DataContract]
        private sealed class RowData
        {
            [DataMember(Name = "sheetNumber", IsRequired = true)] public string Number { get; set; }
            [DataMember(Name = "sheetName", IsRequired = true)] public string Name { get; set; }
            [DataMember(Name = "isPlaceholder", IsRequired = true)] public bool Placeholder { get; set; }
        }
        public static void Write(string path, IList<SheetInfo> sheets)
        {
            var data = new FileData { Version = 1, Sheets = sheets.Select(s => new RowData {
                Number = s.Number, Name = s.Name, Placeholder = s.IsPlaceholder }).ToList() };
            string temp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), Guid.NewGuid() + ".tmp");
            try
            {
                using (var stream = File.Create(temp))
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, false, true, "  "))
                    new DataContractJsonSerializer(typeof(FileData)).WriteObject(writer, data);
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static List<SheetInfo> Read(string path)
        {
            if (new FileInfo(path).Length > 4000000) throw new InvalidDataException("File JSON quá lớn (tối đa 4 MB).");
            FileData data;
            using (var stream = File.OpenRead(path))
                data = new DataContractJsonSerializer(typeof(FileData)).ReadObject(stream) as FileData;
            if (data == null || data.Version != 1 || data.Sheets == null || data.Sheets.Any(s => s == null))
                throw new InvalidDataException("File sheet JSON không đúng định dạng/version 1.");
            return data.Sheets.Select((s, i) => new SheetInfo(s.Number, s.Name, s.Placeholder) { ExcelRow = i + 1 }).ToList();
        }
    }
}
