namespace MEP_Sheet_Manager
{
    public sealed class ViewInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public int Scale { get; set; }
        public string SheetNumbers { get; set; }
    }
    public sealed class RevisionInfo
    {
        public int Sequence { get; set; }
        public string Description { get; set; }
        public string Date { get; set; }
        public bool Issued { get; set; }
    }
}
