using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Autodesk.Revit.DB;
using Microsoft.Win32;

namespace MEP_Sheet_Manager
{
    public partial class SheetManagerWindow
    {
        private List<SheetRevisionRow> sheetRevisionRows;
        private void RefreshToolData(bool preserveRevisionDraft = false)
        {
            var sets = new[] { new SheetSetInfo { Id = -1, Name = "V/S Sets: All" } }.Concat(SheetToolsService.ReadSets(document)).ToList();
            bindingFilters = true;
            foreach (var combo in new[] { SheetSetFilter, ViewSetFilter, RevisionSetFilter }) {
                int id = (combo.SelectedItem as SheetSetInfo)?.Id ?? -1;
                combo.ItemsSource = sets; combo.SelectedItem = sets.FirstOrDefault(s => s.Id == id) ?? sets[0];
            }
            bindingFilters = false;
            var previous = sheetRevisionRows;
            sheetRevisionRows = SheetToolsService.ReadSheetRevisions(document);
            if (preserveRevisionDraft && previous != null) foreach (var row in sheetRevisionRows) {
                var old = previous.FirstOrDefault(r => r.UniqueId == row.UniqueId);
                if (old == null || !old.Modified) continue;
                foreach (var revision in row.Revisions.Where(r => r.CanEdit)) {
                    var choice = old.Revisions.FirstOrDefault(r => r.Key == revision.Key && r.CanEdit);
                    if (choice != null) revision.Selected = choice.Selected;
                }
            }
            RevisionGrid.ItemsSource = new ListCollectionView(sheetRevisionRows);
            RevisionCountLabel.Text = sheetRevisionRows.Count + " sheet · Chọn Revisions, rồi Apply. Current Revision cập nhật sau khi lưu. Revision từ cloud không bỏ tick được tại đây.";
        }
        private List<SheetInfo> SelectedSheets()
        {
            var visible = SheetGrid.Items.Cast<SheetInfo>().ToList();
            var checkedRows = visible.Where(r => r.IsChecked).ToList();
            return checkedRows.Count > 0 ? checkedRows : SheetGrid.SelectedItems.Cast<SheetInfo>().ToList();
        }
        private List<SheetInfo> RequireProjectSelection()
        {
            if (imported != null) throw new InvalidOperationException("Đang xem bản nháp. Tạo sheet hoặc Cập nhật để thao tác sheet trong project.");
            var selected = SelectedSheets();
            if (selected.Count == 0) throw new InvalidOperationException("Tick hoặc chọn sheet cần thao tác trong Sheet List.");
            return selected;
        }
        private void ToolsMenu_Click(object sender, RoutedEventArgs e) { if (!pending) OpenMenu((Button)sender); }
        private void BatchSelect_Click(object sender, RoutedEventArgs e)
        {
            bool select = ((MenuItem)sender).Tag as string == "all";
            if (select) foreach (SheetInfo row in SheetGrid.Items) row.IsChecked = true;
            else { foreach (var row in imported ?? projectSheets ?? new List<SheetInfo>()) row.IsChecked = false; SheetGrid.UnselectAll(); }
            SheetGrid.Items.Refresh(); StatusLabel.Text = select ? "Đã chọn các dòng đang hiển thị." : "Đã bỏ chọn tất cả.";
        }
        private void RenameSheets_Click(object sender, RoutedEventArgs e)
        {
            if (pending) return;
            try {
                CommitSheetEdits(); var rows = imported != null ? SelectedSheets() : RequireProjectSelection();
                if (rows.Count == 0) throw new InvalidOperationException("Chọn các dòng cần đổi tên.");
                bool number = ((MenuItem)sender).Tag as string == "number";
                var dialog = new ToolDialog(this, "Rename " + (number ? "Sheet Number" : "Sheet Name"), "Xem trước kết quả trên " + rows.Count + " sheet. Áp dụng để lưu.");
                var find = dialog.Text("Tìm chuỗi"); var replace = dialog.Text("Thay bằng");
                var prefix = dialog.Text("Tiền tố"); var suffix = dialog.Text("Hậu tố");
                var table = dialog.Table(new object[0], "Before", "After");
                Func<List<string>> values = () => { var rule = new RenameRule { Find = find.Text, Replace = replace.Text, Prefix = prefix.Text, Suffix = suffix.Text };
                    return rows.Select(r => rule.Apply(number ? r.Number : r.Name)).ToList(); };
                Action preview = () => { var after = values(); table.ItemsSource = rows.Select((r, i) => new { Before = number ? r.Number : r.Name, After = after[i] }).ToList(); };
                foreach (var box in new[] { find, replace, prefix, suffix }) box.TextChanged += (s, a) => preview(); preview();
                if (!dialog.Confirm()) return;
                var result = values();
                QueueRequest(() => {
                    if (imported != null) { for (int i = 0; i < rows.Count; i++) { if (number) rows[i].Number = result[i]; else rows[i].Name = result[i]; } ValidateDraft(); }
                    else { SheetToolsService.Rename(document, rows, result, number); ShowCurrent(); StatusLabel.Text = "Đã đổi " + rows.Count + " sheet. Có thể Undo."; }
                });
            } catch (Exception ex) { ShowError(ex); }
        }
        private void DuplicateSheets_Click(object sender, RoutedEventArgs e)
        {
            if (pending) return;
            try { var rows = RequireProjectSelection();
                QueueRequest(() => { SheetToolsService.DuplicateEmpty(document, rows); ShowCurrent(); StatusLabel.Text = "Đã nhân bản " + rows.Count + " sheet trống cùng loại title block. Có thể Undo."; });
            } catch (Exception ex) { ShowError(ex); }
        }
        private void ChangeTitleBlock_Click(object sender, RoutedEventArgs e)
        {
            if (pending) return;
            try { var rows = RequireProjectSelection();
                var options = TitleBlocks.Items.Cast<TitleBlockOption>().Where(t => t.Id != ElementId.InvalidElementId).ToList();
                if (options.Count == 0) throw new InvalidOperationException("Project chưa có title block.");
                var dialog = new ToolDialog(this, "Đổi title block", "Đổi title block cho " + rows.Count + " sheet đã chọn.");
                var choice = dialog.Combo("Title block", options, "Label");
                if (!dialog.Confirm()) return;
                var id = ((TitleBlockOption)choice.SelectedItem).Id;
                QueueRequest(() => { SheetToolsService.ChangeTitleBlock(document, rows, id); ShowCurrent(); StatusLabel.Text = "Đã đổi title block. Có thể Undo."; });
            } catch (Exception ex) { ShowError(ex); }
        }
        private void Parameters_Click(object sender, RoutedEventArgs e)
        {
            if (pending) return;
            try { var rows = RequireProjectSelection();
                QueueRequest(() => {
                    var parameters = SheetToolsService.Parameters(document, rows);
                    if (parameters.Count == 0) throw new InvalidOperationException("Không có parameter có thể sửa chung cho các sheet này.");
                    var dialog = new ToolDialog(this, "Sheet Parameters", "Ghi một giá trị cho " + rows.Count + " sheet. Double: nhập theo đơn vị hiển thị trong Revit. Yes/No: 1 hoặc 0.");
                    var choice = dialog.Combo("Parameter", parameters, "Label");
                    var value = dialog.Text("Giá trị mới", parameters[0].Value);
                    choice.SelectionChanged += (s, a) => value.Text = (choice.SelectedItem as EditableParameterInfo)?.Value ?? "";
                    dialog.Table(rows, "Number", "Name");
                    if (!dialog.Confirm()) return;
                    SheetToolsService.SetParameter(document, rows, ((EditableParameterInfo)choice.SelectedItem).Id, value.Text);
                    ShowCurrent(); StatusLabel.Text = "Đã cập nhật parameter trên " + rows.Count + " sheet. Có thể Undo.";
                });
            } catch (Exception ex) { ShowError(ex); }
        }
        private void SaveSet_Click(object sender, RoutedEventArgs e)
        {
            if (pending) return;
            try { var rows = RequireProjectSelection(); var dialog = new ToolDialog(this, "Save V/S Set", "Lưu " + rows.Count + " sheet vào bộ in/xuất của Revit.");
                var name = dialog.Text("Tên V/S Set", "Sheet Set"); dialog.Table(rows, "Number", "Name");
                if (!dialog.Confirm("Lưu bộ mới")) return;
                QueueRequest(() => { SheetToolsService.SaveSet(document, rows, name.Text); RefreshToolData(true); ApplySheetFilters(); StatusLabel.Text = "Đã lưu V/S Set: " + name.Text + ". Có thể Undo."; });
            } catch (Exception ex) { ShowError(ex); }
        }
        private void SetFilter_Changed(object sender, SelectionChangedEventArgs e) { ApplySheetFilters(); }
        private void ExistingViews_Click(object sender, RoutedEventArgs e) { ApplySheetFilters(); }
        private void FilterPlanChoices()
        {
            if (floorPlanAssignments == null || ViewTemplateFilter == null) return;
            string template = ViewTemplateFilter.SelectedIndex > 0 ? ViewTemplateFilter.SelectedItem as string : null;
            foreach (var row in floorPlanAssignments) row.FilterTemplates(template);
        }
        private void TemplateFilter_Changed(object sender, SelectionChangedEventArgs e) { if (!bindingFilters) FilterPlanChoices(); }
        private void ViewManager_Click(object sender, RoutedEventArgs e)
        {
            if (pending) return;
            QueueRequest(() => {
                var plans = SheetToolsService.ReadPlans(document);
                if (plans.Count == 0) throw new InvalidOperationException("Project chưa có Floor Plan.");
                var dialog = new ToolDialog(this, "View Manager", "Quản lý Floor Plan: sửa tên / scale / template, hoặc duplicate view.", 850);
                var search = dialog.Text("Search view"); var grid = dialog.Table(plans, "Name", "Scale", "Template", "Sheets");
                search.TextChanged += (s, a) => grid.ItemsSource = plans.Where(p => p.Name.IndexOf(search.Text, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                var option = dialog.Combo("Kiểu duplicate", new[] { "Duplicate", "With Detailing", "As Dependent" });
                string operation = null; PlanManagerInfo selected = null; int duplicateOption = 0;
                dialog.Button("Đóng", () => dialog.DialogResult = false);
                dialog.Button("Sửa view", () => { selected = grid.SelectedItem as PlanManagerInfo; if (selected == null) return; operation = "edit"; dialog.DialogResult = true; });
                dialog.Button("Duplicate", () => { selected = grid.SelectedItem as PlanManagerInfo; if (selected == null) return; duplicateOption = option.SelectedIndex; operation = "duplicate"; dialog.DialogResult = true; });
                if (dialog.ShowDialog() != true) return;
                if (operation == "duplicate") SheetToolsService.DuplicatePlan(document, selected,
                    duplicateOption == 1 ? ViewDuplicateOption.WithDetailing : duplicateOption == 2 ? ViewDuplicateOption.AsDependent : ViewDuplicateOption.Duplicate);
                else {
                    var plan = document.GetElement(new ElementId(selected.Id)) as ViewPlan;
                    var templates = new List<FloorPlanChoice> { new FloorPlanChoice { Id = -1, Name = "<None>" } };
                    using (var c = new FilteredElementCollector(document)) templates.AddRange(c.OfClass(typeof(View)).Cast<View>()
                        .Where(v => v.IsTemplate && plan.IsValidViewTemplate(v.Id)).OrderBy(v => v.Name).Select(v => new FloorPlanChoice { Id = v.Id.IntegerValue, Name = v.Name }));
                    var edit = new ToolDialog(this, "Sửa Floor Plan", "Thay đổi ảnh hưởng cả view đã đặt trên sheet. View phụ thuộc có thể dùng chung scale với view gốc.");
                    var name = edit.Text("Tên view", selected.Name); var scale = edit.Text("Scale 1 :", selected.Scale.ToString(CultureInfo.InvariantCulture));
                    var template = edit.Combo("View Template", templates, "Name"); template.SelectedItem = templates.FirstOrDefault(t => t.Id == plan.ViewTemplateId.IntegerValue) ?? templates[0];
                    if (!edit.Confirm()) return;
                    int denominator; if (!int.TryParse(scale.Text, out denominator)) throw new InvalidOperationException("Scale phải là số nguyên.");
                    SheetToolsService.EditPlan(document, selected, name.Text, denominator, ((FloorPlanChoice)template.SelectedItem).Id);
                }
                var previous = floorPlanAssignments;
                var refreshed = FloorPlanPlacementService.Read(document);
                foreach (var row in refreshed) {
                    var old = previous?.FirstOrDefault(r => r.SheetUniqueId == row.SheetUniqueId);
                    var choice = old?.SelectedFloorPlan;
                    if (choice == null || choice.Id < 1) continue;
                    var match = row.FloorPlans.FirstOrDefault(p => p.Id == choice.Id);
                    if (match != null) { row.SelectedFloorPlan = match;
                        if (old.HasPosition) { if (old.ScopeCentered) row.SetScopePosition(old.CenterXmm.Value, old.CenterYmm.Value, old.OffsetXmm, old.OffsetYmm);
                            else row.SetPosition(old.CenterXmm.Value, old.CenterYmm.Value); }
                    }
                }
                BindFloorPlans(refreshed); StatusLabel.Text = "Đã cập nhật Floor Plan. Có thể Undo trong Revit.";
            });
        }
        private void ChooseRevisions_Click(object sender, RoutedEventArgs e)
        {
            if (pending) return;
            var row = (sender as Button)?.DataContext as SheetRevisionRow; if (row == null) return;
            var dialog = new ToolDialog(this, "Revisions · " + row.Number, "Tick revision thêm vào sheet. Revision từ cloud bị khóa. Bấm Apply ở tab để ghi vào Revit.");
            var boxes = new Dictionary<RevisionChoice, CheckBox>();
            foreach (var choice in row.Revisions) {
                var box = new CheckBox { Content = choice.Label, IsChecked = choice.Selected, IsEnabled = choice.CanEdit, Margin = new Thickness(0, 5, 0, 5) };
                boxes.Add(choice, box); dialog.Fields.Children.Add(box);
            }
            if (row.Revisions.Count == 0) dialog.Label("Project chưa có revision. Tạo trong nút Revisions.");
            if (!dialog.Confirm("Dùng lựa chọn")) return;
            foreach (var pair in boxes) pair.Key.Selected = pair.Value.IsChecked == true;
            row.NotifySelection(); StatusLabel.Text = "Đã sửa lựa chọn revision cho " + row.Number + ". Chưa ghi vào Revit; bấm Apply.";
        }
        private void RevisionsManager_Click(object sender, RoutedEventArgs e)
        {
            if (pending) return;
            QueueRequest(() => {
                List<Revision> revisions; using (var c = new FilteredElementCollector(document)) revisions = c.OfClass(typeof(Revision)).Cast<Revision>().OrderBy(r => r.SequenceNumber).ToList();
                var options = new[] { new { Id = -1, Label = "<Tạo revision mới>" } }.Concat(revisions.Select(r => new { Id = r.Id.IntegerValue, Label = SheetToolsService.RevisionKey(r) })).ToList();
                var dialog = new ToolDialog(this, "Revisions", "Tạo hoặc sửa revision trong project. Các sheet dùng revision này sẽ cập nhật theo.");
                var select = dialog.Combo("Revision", options, "Label"); var description = dialog.Text("Description"); var date = dialog.Text("Date", DateTime.Today.ToString("dd/MM/yyyy"));
                var issued = new CheckBox { Content = "Issued", Margin = new Thickness(0, 8, 0, 8) }; dialog.Fields.Children.Add(issued);
                select.SelectionChanged += (s, a) => { var r = revisions.FirstOrDefault(v => v.Id.IntegerValue == options[select.SelectedIndex].Id); description.Text = r?.Description ?? ""; date.Text = r?.RevisionDate ?? DateTime.Today.ToString("dd/MM/yyyy"); issued.IsChecked = r?.Issued ?? false; };
                if (!dialog.Confirm()) return;
                int id = options[select.SelectedIndex].Id;
                SheetToolsService.Change(document, "Create or edit revision", () => {
                    var revision = id < 0 ? Revision.Create(document) : document.GetElement(new ElementId(id)) as Revision;
                    if (revision == null) throw new InvalidOperationException("Revision không còn tồn tại.");
                    if (revision.Issued) revision.Issued = false;
                    revision.Description = description.Text; revision.RevisionDate = date.Text; revision.Issued = issued.IsChecked == true;
                });
                RefreshToolData(true); ApplySheetFilters(); StatusLabel.Text = "Đã lưu revision. Có thể Undo.";
            });
        }
        private void ApplyTab_Click(object sender, RoutedEventArgs e)
        {
            if (pending) return;
            if (MainTabs.SelectedIndex == 0) { if (CreateButton.IsEnabled) Create_Click(sender, e); else StatusLabel.Text = "Tạo bản nháp qua Nhập / Tạo sheet. Đổi tên và parameters dùng nút riêng."; }
            else if (MainTabs.SelectedIndex == 1) PlacePlans_Click(sender, e);
            else if (MainTabs.SelectedIndex == 2 && sheetRevisionRows != null) {
                var rows = sheetRevisionRows; QueueRequest(() => { SheetToolsService.ApplyRevisions(document, rows); RefreshToolData(); ApplySheetFilters(); StatusLabel.Text = "Đã cập nhật revision trên sheet. Có thể Undo."; });
            }
        }
        private void PreviousTab_Click(object sender, RoutedEventArgs e) { if (!pending) MainTabs.SelectedIndex = Math.Max(0, MainTabs.SelectedIndex - 1); }
        private void NextTab_Click(object sender, RoutedEventArgs e) { if (!pending) MainTabs.SelectedIndex = Math.Min(MainTabs.Items.Count - 1, MainTabs.SelectedIndex + 1); }
        private static readonly string[] ViewHeaders = { "SheetNumber", "SheetName", "FloorPlanName", "CurrentFloorPlans" };
        private static readonly string[] RevisionHeaders = { "SheetNumber", "SheetName", "AdditionalRevisionKeys", "CurrentRevision", "CurrentRevisionDate", "CurrentRevisionDescription" };
        private void ExportTab_Click(object sender, RoutedEventArgs e)
        {
            if (pending) return;
            bool views = MainTabs.SelectedIndex == 1; bool json = ((MenuItem)sender).Tag as string == "json";
            var picker = new SaveFileDialog { Filter = json ? "JSON (*.json)|*.json" : "Excel (*.xlsx)|*.xlsx", DefaultExt = json ? ".json" : ".xlsx", FileName = (views ? "SheetViews" : "SheetRevisions") + (json ? ".json" : ".xlsx"), OverwritePrompt = true };
            if (picker.ShowDialog(this) != true) return;
            try {
                var data = new TabularData { Kind = views ? "SheetViews" : "SheetRevisions", Headers = views ? ViewHeaders : RevisionHeaders,
                    Rows = views ? floorPlanAssignments.Select(r => new[] { r.Number, r.Name, r.SelectedFloorPlan?.Id > 0 ? r.SelectedFloorPlan.Name : "", r.CurrentFloorPlans ?? "" }).ToList()
                        : sheetRevisionRows.Select(r => new[] { r.Number, r.Name, TabularFile.Pack(r.Revisions.Where(v => v.Selected && v.CanEdit).Select(v => v.Key).ToArray()), r.CurrentNumber ?? "", r.CurrentDate ?? "", r.CurrentDescription ?? "" }).ToList() };
                TabularFile.Write(picker.FileName, data); StatusLabel.Text = "Đã xuất " + data.Kind + ": " + picker.FileName;
            } catch (Exception ex) { ShowError(ex); }
        }
        private void ImportTab_Click(object sender, RoutedEventArgs e)
        {
            if (pending) return;
            bool views = MainTabs.SelectedIndex == 1;
            var picker = new OpenFileDialog { Filter = "Excel / JSON (*.xlsx;*.json)|*.xlsx;*.json", CheckFileExists = true };
            if (picker.ShowDialog(this) != true) return;
            try {
                var data = TabularFile.Read(picker.FileName, views ? "SheetViews" : "SheetRevisions", views ? ViewHeaders : RevisionHeaders);
                if (views) { SheetTabImport.Views(data, floorPlanAssignments); FilterPlanChoices(); UpdateCreateButton(); }
                else SheetTabImport.Revisions(data, sheetRevisionRows);
                StatusLabel.Text = "Đã nạp " + data.Rows.Count + " dòng vào bảng. Chưa thay đổi Revit; kiểm tra rồi Apply.";
            } catch (Exception ex) { ShowError(ex); }
        }
    }
}
