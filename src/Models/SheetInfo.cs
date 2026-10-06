namespace MEP_Sheet_Manager
{
    public sealed class SheetInfo
    {
        public bool IsChecked { get; set; }
        public string Number { get; set; }
        public string Name { get; set; }
        public bool IsPlaceholder { get; set; }
        public int ExcelRow { get; set; }
        public string Status { get; set; }
        public bool CanEdit { get; set; }
        public int SheetId { get; set; }
        public string SheetUniqueId { get; set; }
        public SheetInfo() { }
        public SheetInfo(string number, string name, bool placeholder) { Number = number; Name = name; IsPlaceholder = placeholder; }
    }
}

