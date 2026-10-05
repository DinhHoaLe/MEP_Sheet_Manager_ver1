using System.Collections.Generic;
using System.ComponentModel;

namespace MEP_Sheet_Manager
{
    public sealed class FloorPlanChoice
    {
        public int Id { get; set; }
        public string UniqueId { get; set; }
        public string Name { get; set; }
        public string ScopeName { get; set; }
    }
    public sealed class SheetViewAssignment : INotifyPropertyChanged
    {
        public int SheetId { get; set; }
        public string SheetUniqueId { get; set; }
        public string Number { get; set; }
        public string Name { get; set; }
        public string CurrentFloorPlans { get; set; }
        public IList<FloorPlanChoice> FloorPlans { get; set; }
        private FloorPlanChoice selectedFloorPlan;
        public FloorPlanChoice SelectedFloorPlan
        {
            get { return selectedFloorPlan; }
            set
            {
                if (ReferenceEquals(selectedFloorPlan, value)) return;
                selectedFloorPlan = value;
                CenterXmm = CenterYmm = null;
                ProcessingStatus = "";
                ScopeCentered = false; OffsetXmm = OffsetYmm = 0;
                Notify("PositionLabel");
                Status = value != null && value.Id > 0 ? "Đã chọn, chưa đặt" : "";
                Notify("SelectedFloorPlan");
            }
        }
        public double? CenterXmm { get; private set; }
        public double? CenterYmm { get; private set; }
        public bool HasPosition { get { return CenterXmm.HasValue && CenterYmm.HasValue; } }
        public bool ScopeCentered { get; private set; }
        public double OffsetXmm { get; private set; }
        public double OffsetYmm { get; private set; }
        public string PositionLabel
        {
            get { return ScopeCentered ? string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Độ dịch X: {0:0.##}; Y: {1:0.##} mm", OffsetXmm, OffsetYmm) : HasPosition ? string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "X: {0:0.##}; Y: {1:0.##} mm", CenterXmm.Value, CenterYmm.Value) : "Tự canh giữa · Trái 44 / phải 94 mm"; }
        }
        public void SetPosition(double x, double y)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y))
                throw new System.ArgumentException("Tọa độ phải là số hữu hạn.");
            CenterXmm = x; CenterYmm = y;
            ProcessingStatus = "";
            ScopeCentered = false; OffsetXmm = OffsetYmm = 0;
            Status = "Đã lưu vị trí, chưa áp dụng";
            Notify("PositionLabel");
        }
        public void SetScopePosition(double x, double y, double offsetX, double offsetY)
        {
            if (double.IsNaN(offsetX) || double.IsInfinity(offsetX) || double.IsNaN(offsetY) || double.IsInfinity(offsetY))
                throw new System.ArgumentException("Độ dịch scope phải là số hữu hạn.");
            SetPosition(x, y);
            ScopeCentered = true; OffsetXmm = offsetX; OffsetYmm = offsetY;
            Notify("PositionLabel");
        }
        private string status;
        private string processingStatus = "";
        public string ProcessingStatus { get { return processingStatus; } set { processingStatus = value; Notify("ProcessingStatus"); } }
        public string Status
        {
            get { return status; }
            set { status = value; Notify("Status"); }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify(string property)
        {
            var callback = PropertyChanged;
            if (callback != null) callback(this, new PropertyChangedEventArgs(property));
        }
    }
}

