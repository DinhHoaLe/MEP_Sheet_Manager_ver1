using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace MEP_Sheet_Manager
{
    public sealed class SheetSetInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public HashSet<int> SheetIds { get; set; }
    }
    public sealed class RevisionChoice : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Key { get; set; }
        public string Label { get; set; }
        public bool CanEdit { get; set; }
        private bool selected;
        public bool Selected { get { return selected; } set { selected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Selected")); } }
        public event PropertyChangedEventHandler PropertyChanged;
    }
    public sealed class SheetRevisionRow : INotifyPropertyChanged
    {
        public int SheetId { get; set; }
        public string UniqueId { get; set; }
        public string Number { get; set; }
        public string Name { get; set; }
        public List<RevisionChoice> Revisions { get; set; }
        public HashSet<int> InitialAdditionalIds { get; set; }
        public HashSet<int> InitialCloudIds { get; set; }
        public bool Modified { get { return InitialAdditionalIds != null && !InitialAdditionalIds.SetEquals(Revisions.Where(r => r.Selected && r.CanEdit).Select(r => r.Id)); } }
        public string CurrentNumber { get; set; }
        public string CurrentDate { get; set; }
        public string CurrentDescription { get; set; }
        public string Summary { get { var text = string.Join(", ", Revisions.Where(r => r.Selected).Select(r => r.Label)); return text.Length == 0 ? "<None> ▾" : text; } }
        public void NotifySelection() { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Summary")); }
        public event PropertyChangedEventHandler PropertyChanged;
    }
    public sealed class EditableParameterInfo
    {
        public int Id { get; set; }
        public string Label { get; set; }
        public string Value { get; set; }
    }
    public sealed class PlanManagerInfo
    {
        public int Id { get; set; }
        public string UniqueId { get; set; }
        public string Name { get; set; }
        public int Scale { get; set; }
        public string Template { get; set; }
        public string Sheets { get; set; }
    }
    public sealed class RenameRule
    {
        public string Find { get; set; }
        public string Replace { get; set; }
        public string Prefix { get; set; }
        public string Suffix { get; set; }
        public string Apply(string value) { return (Prefix ?? "") + (string.IsNullOrEmpty(Find) ? value : value.Replace(Find, Replace ?? "")) + (Suffix ?? ""); }
    }
}
