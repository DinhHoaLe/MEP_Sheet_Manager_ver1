namespace MEP_Sheet_Manager
{
    public sealed class SheetLayoutInfo
    {
        public string SheetUniqueId { get; set; }
        public string SheetNumber { get; set; }
        public string SheetName { get; set; }
        public string ViewUniqueId { get; set; }
        public string ViewName { get; set; }
        public double CenterXmm { get; set; }
        public double CenterYmm { get; set; }
        public bool ScopeCentered { get; set; }
        public double OffsetXmm { get; set; }
        public double OffsetYmm { get; set; }
    }
    public sealed class SheetLayoutPreview
    {
        public SheetLayoutInfo Layout { get; set; }
        public double MinXmm { get; set; }
        public double MinYmm { get; set; }
        public double MaxXmm { get; set; }
        public double MaxYmm { get; set; }
        public double ViewWidthMm { get; set; }
        public double ViewHeightMm { get; set; }
        public double RotationDegrees { get; set; }
        public bool HasScope { get; set; }
        public bool TitleBlockOnly { get; set; }
        public string ScopeName { get; set; }
        public double ScopeBaseXmm { get; set; }
        public double ScopeBaseYmm { get; set; }
        public string ImagePath { get; set; }
        public string ImageWarning { get; set; }
        public System.Collections.Generic.List<SheetLineInfo> TitleBlockLines { get; set; }
        public string Label { get { return Layout.SheetNumber + " — " + Layout.SheetName + " / " + Layout.ViewName; } }
    }
    public sealed class SheetLineInfo
    {
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
    }
}
