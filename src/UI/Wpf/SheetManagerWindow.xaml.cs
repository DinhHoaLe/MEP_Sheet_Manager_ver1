using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;

namespace MEP_Sheet_Manager
{
    public partial class SheetManagerWindow : Window
    {
        private readonly Document document;
        private readonly string projectTitle;
        private readonly SheetExternalEventHandler handler;
        private readonly ExternalEvent externalEvent;
        private bool pending;
        private bool closed;
        private bool loading = true;
        private bool validationQueued;
        private List<SheetInfo> projectSheets;
        private List<SheetInfo> imported;
        private List<SheetViewAssignment> floorPlanAssignments;
        private Dictionary<int, SheetBrowserInfo> browserRows = new Dictionary<int, SheetBrowserInfo>();
        private bool bindingFilters;
        private sealed class TitleBlockOption
        {
            public string Label { get; set; }
            public ElementId Id { get; set; }
        }
        // Constructed by IExternalCommand, inside a valid Revit API context.
        public SheetManagerWindow(Document document)
        {
            InitializeComponent();
            this.document = document;
            projectTitle = document.Title;
            handler = new SheetExternalEventHandler();
            externalEvent = ExternalEvent.Create(handler);
            Closed += OnClosed;
            ProjectLabel.Text = "Project đích / nguồn: " + projectTitle;
            Loaded += (sender, args) => Dispatcher.BeginInvoke(new Action(BeginLoad), System.Windows.Threading.DispatcherPriority.ContextIdle);
        }
        private void BeginLoad()
        {
            if (closed || pending) return;
            loading = true;
            LoadingPanel.Visibility = System.Windows.Visibility.Visible;
            LoadingProgress.Visibility = System.Windows.Visibility.Visible;
            RetryLoadButton.Visibility = System.Windows.Visibility.Collapsed;
            LoadingLabel.Text = "Đang tải sheet, title block, floor plan và revision từ " + projectTitle + "…";
            LoadingProgress.IsIndeterminate = true;
            QueueRequest(() => {
                RefreshTitleBlocks(); ShowCurrent(); loading = false;
                LoadingPanel.Visibility = System.Windows.Visibility.Collapsed;
                HeaderPanel.Visibility = BodyGrid.Visibility = FooterGrid.Visibility = System.Windows.Visibility.Visible;
            });
        }
        private void RetryLoad_Click(object sender, RoutedEventArgs e) { BeginLoad(); }
        private void RefreshTitleBlocks()
        {
            var options = new List<TitleBlockOption> { new TitleBlockOption { Label = "Không có title block", Id = ElementId.InvalidElementId } };
            using (var collector = new FilteredElementCollector(document))
                options.AddRange(collector.OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsElementType()
                    .OfType<FamilySymbol>().OrderBy(s => s.FamilyName).ThenBy(s => s.Name)
                    .Select(s => new TitleBlockOption { Label = s.FamilyName + " : " + s.Name, Id = s.Id }));
            TitleBlocks.ItemsSource = options;
            TitleBlocks.SelectedIndex = 0;
        }
        // All document reads and writes after construction are dispatched through this event.
        private void QueueRequest(Action action) { QueueSteps(new[] { action }); }
        private void QueueSteps(IEnumerable<Action> actions)
        {
            if (closed || pending) return;
            pending = true; BodyGrid.IsEnabled = false; CreateButton.IsEnabled = false; PlacePlansButton.IsEnabled = false;
            var steps = actions.GetEnumerator();
            Action finish = () => {
                steps.Dispose(); handler.Request = null; pending = false;
                if (closed) Dispatcher.BeginInvoke(new Action(() => externalEvent.Dispose()));
                else { BodyGrid.IsEnabled = true; UpdateCreateButton(); }
            };
            Action raise = null;
            raise = () => {
                if (closed) { finish(); return; }
                handler.Request = application => {
                    try {
                        if (closed) { finish(); return; }
                        var active = application.ActiveUIDocument;
                        if (!document.IsValidObject || active == null || !document.Equals(active.Document))
                            throw new InvalidOperationException("Project đã đóng hoặc không còn active. Quay lại " + projectTitle + ".");
                        if (!steps.MoveNext()) { finish(); return; }
                        steps.Current();
                        Dispatcher.BeginInvoke(raise, System.Windows.Threading.DispatcherPriority.Background);
                    }
                    catch (Exception ex) { finish(); if (!closed) ShowError(ex); }
                };
                try {
                    var result = externalEvent.Raise();
                    if (result != ExternalEventRequest.Accepted) throw new InvalidOperationException("Revit chưa nhận yêu cầu: " + result);
                }
                catch (Exception ex) { finish(); if (!closed) ShowError(ex); }
            };
            StatusLabel.Text = "Waiting · Đang chờ Revit xử lý…";
            raise();
        }
        private void OnClosed(object sender, EventArgs e)
        {
            closed = true;
            // A queued request checks 'closed' before touching the document.
            if (!pending) externalEvent.Dispose();
        }
        private void UpdateCreateButton()
        {
            CreateButton.IsEnabled = MainTabs.SelectedIndex == 0 && !pending && imported != null && imported.Any(r => r.Status == "Sẵn sàng")
                && !imported.Any(r => r.Status.StartsWith("Lỗi:"));
            if (PlacePlansButton != null)
                PlacePlansButton.IsEnabled = MainTabs.SelectedIndex == 1 && !pending && floorPlanAssignments != null
                    && floorPlanAssignments.Any(r => r.SelectedFloorPlan != null && r.SelectedFloorPlan.Id > 0);
            if (ReviewTitleBlocksButton != null) ReviewTitleBlocksButton.IsEnabled = !pending && floorPlanAssignments != null && floorPlanAssignments.Count > 0;
            if (SaveLayoutButton != null) SaveLayoutButton.IsEnabled = PlacePlansButton.IsEnabled;
            if (LoadLayoutButton != null) LoadLayoutButton.IsEnabled = !pending;
        }
        private void Tabs_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (MainTabs == null || SheetActions == null || CreateButton == null || e.Source != MainTabs) return;
            bool sheetTab = MainTabs.SelectedIndex == 0;
            SheetActions.Visibility = sheetTab && imported != null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            ActionsColumn.Width = SpacerColumn.Width = new GridLength(0);
            UpdateCreateButton();
        }
        private void ShowCurrent()
        {
            imported = null;
            var sheets = SheetService.Read(document);
            projectSheets = sheets;
            RefreshBrowserFilters();
            SheetGrid.ItemsSource = new System.Windows.Data.ListCollectionView(sheets);
            SheetGrid.IsReadOnly = true;
            SheetActions.Visibility = ManualPanel.Visibility = System.Windows.Visibility.Collapsed;
            RowColumn.Visibility = StatusColumn.Visibility = System.Windows.Visibility.Collapsed;
            ListHeading.Text = "SHEET TRONG PROJECT";
            CountLabel.Text = sheets.Count + " sheet. " + sheets.Count(s => s.IsPlaceholder) + " placeholder.";
            var placeholders = sheets.Where(s => s.IsPlaceholder).ToList();
            PlaceholderGrid.ItemsSource = placeholders;
            PlaceholderCountLabel.Text = placeholders.Count + " placeholder sheet. Tạo mới qua tab Sheet List hoặc cột IsPlaceholder trong Excel.";
            RefreshFloorPlans();
            var revisions = ProjectListService.ReadRevisions(document);
            RevisionGrid.ItemsSource = revisions;
            RevisionCountLabel.Text = revisions.Count + " revision. Sequence là thứ tự trong project, không phải số revision trên từng sheet.";
            StatusLabel.Text = "Có thể thao tác Revit khi tool đang mở. Danh sách hiện tại để cập nhật sheet và title block.";
            CreateButton.IsEnabled = false;
            ApplySheetFilters();
        }
        private void RefreshFloorPlans()
        {
            BindFloorPlans(FloorPlanPlacementService.Read(document));
        }
        private void BindFloorPlans(List<SheetViewAssignment> assignments)
        {
            floorPlanAssignments = assignments;
            foreach (var row in floorPlanAssignments)
                row.PropertyChanged += (sender, args) => { if (args.PropertyName == "SelectedFloorPlan") UpdateCreateButton(); };
            ViewGrid.ItemsSource = new System.Windows.Data.ListCollectionView(floorPlanAssignments);
            if (floorPlanAssignments.Count > 0) ViewGrid.SelectedIndex = 0;
            int available = floorPlanAssignments.SelectMany(r => r.FloorPlans).Where(p => p.Id > 0).Select(p => p.Id).Distinct().Count();
            ViewCountLabel.Text = floorPlanAssignments.Count + " sheet thường. " + available
                + " floor plan có thể chọn. Có thể chọn plan chưa đặt hoặc plan đang có trên chính sheet này, rồi Preview bố trí.";
            UpdateCreateButton();
            ApplySheetFilters();
        }
        private void RefreshBrowserFilters()
        {
            browserRows = SheetBrowserService.Read(document);
            var options = new List<BrowserFilterOption> {
                new BrowserFilterOption { Label = "Tất cả sheet" },
                new BrowserFilterOption { Label = "Project Browser hiện tại", UseBrowser = true } };
            options.AddRange(browserRows.Values.Where(r => r.Visible).Select(r => r.Path).Distinct().OrderBy(p => p)
                .Select(p => new BrowserFilterOption { Label = string.IsNullOrEmpty(p) ? "Không có nhóm" : p, Path = p, UseBrowser = true }));
            bindingFilters = true;
            foreach (var combo in new[] { SheetBrowserGroup, ViewBrowserGroup }) {
                string path = (combo.SelectedItem as BrowserFilterOption)?.Path;
                bool use = (combo.SelectedItem as BrowserFilterOption)?.UseBrowser == true;
                combo.ItemsSource = options;
                combo.SelectedItem = options.FirstOrDefault(o => o.Path == path && o.UseBrowser == use) ?? options[0];
            }
            bindingFilters = false;
        }
        private void ApplySheetFilters()
        {
            if (bindingFilters || SheetSearch == null || ViewSearch == null) return;
            Action<System.Windows.Controls.DataGrid, string, BrowserFilterOption> apply = (grid, search, option) => {
                var view = grid.ItemsSource as System.ComponentModel.ICollectionView;
                if (view == null) return;
                view.Filter = item => {
                    var sheet = item as SheetInfo; var plan = item as SheetViewAssignment;
                    int id = sheet != null ? sheet.SheetId : plan.SheetId;
                    SheetBrowserInfo browser; browserRows.TryGetValue(id, out browser);
                    return SheetListFilter.Matches(sheet != null ? sheet.Number : plan.Number,
                        sheet != null ? sheet.Name : plan.Name, search, sheet != null && sheet.CanEdit ? null : option, browser);
                };
            };
            apply(SheetGrid, SheetSearch.Text, SheetBrowserGroup.SelectedItem as BrowserFilterOption);
            apply(ViewGrid, ViewSearch.Text, ViewBrowserGroup.SelectedItem as BrowserFilterOption);
        }
        private void Search_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e) { ApplySheetFilters(); }
        private void BrowserGroup_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { ApplySheetFilters(); }
        private void BrowserFilter_Click(object sender, RoutedEventArgs e)
        {
            var combo = MainTabs.SelectedIndex == 1 ? ViewBrowserGroup : SheetBrowserGroup;
            bool show = combo.Visibility != System.Windows.Visibility.Visible;
            combo.Visibility = show ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            combo.SelectedIndex = show ? 1 : 0;
        }
        private void DeleteSheets_Click(object sender, RoutedEventArgs e)
        {
            if (pending) return;
            if (MainTabs.SelectedIndex == 0 && imported != null) {
                var draft = SheetGrid.SelectedItems.Cast<SheetInfo>().ToList();
                foreach (var row in draft) imported.Remove(row);
                QueueRequest(ValidateDraft); return;
            }
            var ids = MainTabs.SelectedIndex == 1 ? ViewGrid.SelectedItems.Cast<SheetViewAssignment>().Select(r => r.SheetId).ToList()
                : SheetGrid.SelectedItems.Cast<SheetInfo>().Select(r => r.SheetId).ToList();
            var selected = (projectSheets ?? new List<SheetInfo>()).Where(r => ids.Contains(r.SheetId)).ToList();
            if (selected.Count == 0) { StatusLabel.Text = "Chọn sheet cần xóa trong bảng."; return; }
            string names = string.Join("\n", selected.Take(15).Select(r => r.Number + " · " + r.Name));
            if (MessageBox.Show(this, "Xóa " + selected.Count + " sheet khỏi " + projectTitle + "?\n" + names
                + "\nCác title block và nội dung trên sheet sẽ bị xóa cùng sheet. Có thể Undo trong Revit.",
                "Xóa sheet", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
            QueueRequest(() => { int count = SheetService.Delete(document, selected); ShowCurrent(); StatusLabel.Text = "Done · Đã xóa " + count + " sheet. Có thể Undo trong Revit."; });
        }
        private void PlacePlans_Click(object sender, RoutedEventArgs e)
        {
            if (pending || floorPlanAssignments == null) return;
            var rows = floorPlanAssignments.Where(r => r.SelectedFloorPlan != null && r.SelectedFloorPlan.Id > 0).ToList();
            if (rows.Count == 0) return;
            if (MessageBox.Show(this, "Đặt " + rows.Count + " floor plan lên các sheet đã chọn trong project: " + projectTitle
                + "?\nCanh giữa tự động vào vùng giấy chừa trái 44 mm, phải 94 mm và cộng độ dịch đã lưu. XML cũ giữ tọa độ tuyệt đối. Plan đã có sẽ được di chuyển.", "Áp dụng bố trí", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            QueueSteps(PlacePlansSteps(rows));
        }
        private IEnumerable<Action> PlacePlansSteps(List<SheetViewAssignment> rows)
        {
            int placed = 0, errors = 0, index = 0;
            bool completed = false;
            try
            {
                yield return () => {
                    foreach (var row in rows) row.ProcessingStatus = "Waiting";
                    try {
                        var ready = FloorPlanPlacementService.Validate(document, rows);
                        foreach (var row in rows.Where(r => !ready.Contains(r))) row.ProcessingStatus = "Skipped";
                    }
                    catch {
                        foreach (var row in rows) row.ProcessingStatus = row.Status != null && row.Status.StartsWith("Lỗi:") ? "Error" : "Cancelled";
                        throw;
                    }
                };
                foreach (var row in rows)
                {
                    if (row.ProcessingStatus != "Waiting") continue;
                    yield return () => {
                        row.ProcessingStatus = "Running"; ViewGrid.ScrollIntoView(row);
                        StatusLabel.Text = "Running · " + row.Number + " · " + (++index) + "/" + rows.Count;
                    };
                    yield return () => {
                        try {
                            int count = FloorPlanPlacementService.Place(document, new[] { row });
                            row.ProcessingStatus = count > 0 ? "Done" : "Skipped"; placed += count;
                        }
                        catch (Exception ex) { row.ProcessingStatus = "Error"; row.Status = ex.Message; errors++; }
                    };
                }
                yield return () => {
                    var previous = floorPlanAssignments.ToDictionary(r => r.SheetId);
                    var refreshed = FloorPlanPlacementService.Read(document);
                    foreach (var row in refreshed)
                    {
                        SheetViewAssignment old;
                        if (!previous.TryGetValue(row.SheetId, out old)) continue;
                        var choice = row.FloorPlans.FirstOrDefault(p => p.Id == old.SelectedFloorPlan?.Id);
                        if (choice != null) {
                            row.SelectedFloorPlan = choice;
                            if (old.HasPosition) {
                                if (old.ScopeCentered) row.SetScopePosition(old.CenterXmm.Value, old.CenterYmm.Value, old.OffsetXmm, old.OffsetYmm);
                                else row.SetPosition(old.CenterXmm.Value, old.CenterYmm.Value);
                            }
                        }
                        row.ProcessingStatus = old.ProcessingStatus; row.Status = old.Status;
                    }
                    BindFloorPlans(refreshed); completed = true;
                    StatusLabel.Text = "Đã xử lý: " + placed + " Done · " + errors + " Error · " + (rows.Count - placed - errors) + " Skipped. Undo riêng từng sheet trong Revit.";
                    if (errors > 0) MessageBox.Show(this, string.Join("\n", rows.Where(r => r.ProcessingStatus == "Error")
                        .Select(r => r.Number + ": " + r.Status)), "Kết quả đặt Floor Plan", MessageBoxButton.OK, MessageBoxImage.Warning);
                };
            }
            finally
            {
                if (!completed) foreach (var row in rows.Where(r => r.ProcessingStatus == "Waiting" || r.ProcessingStatus == "Running")) row.ProcessingStatus = "Cancelled";
            }
        }
        private void PreviewLayout_Click(object sender, RoutedEventArgs e)
        {
            if (pending || floorPlanAssignments == null) return;
            var rows = floorPlanAssignments.Where(r => r.SelectedFloorPlan != null && r.SelectedFloorPlan.Id > 0).ToList();
            string folder = Path.Combine(Path.GetTempPath(), "MEPSheetStudioPreview", Guid.NewGuid().ToString("N"));
            QueueRequest(() =>
            {
                try
                {
                    var previews = SheetLayoutPreviewService.Read(document, rows, folder);
                    var preview = new SheetLayoutPreviewWindow(previews, layouts => {
                        SheetLayoutFile.Apply(layouts, floorPlanAssignments);
                        StatusLabel.Text = "Đã lưu vị trí vào bảng. Bấm Áp dụng Floor Plan để ghi vào Revit.";
                    }) { Owner = this };
                    // Disable this tool while editing its snapshot; Revit itself stays available.
                    IsEnabled = false;
                    preview.Closed += (owner, args) => { IsEnabled = true; CleanupPreview(folder); UpdateCreateButton(); };
                    preview.Show();
                }
                catch { IsEnabled = true; CleanupPreview(folder); throw; }
            });
        }
        private void ReviewTitleBlocks_Click(object sender, RoutedEventArgs e)
        {
            if (pending || floorPlanAssignments == null) return;
            if (floorPlanAssignments.Any(r => r.SelectedFloorPlan != null && r.SelectedFloorPlan.Id > 0))
            { PreviewLayout_Click(sender, e); return; }
            var row = ViewGrid.SelectedItem as SheetViewAssignment;
            if (row == null) return;
            QueueRequest(() => {
                var sheet = document.GetElement(new ElementId(row.SheetId)) as ViewSheet;
                if (sheet == null) throw new InvalidOperationException("Sheet không còn tồn tại.");
                var center = ScopeBoxAlignmentService.PaperCenter(sheet);
                var bounds = sheet.Outline;
                Func<double, double> mm = value => UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Millimeters);
                var preview = new SheetLayoutPreview {
                    TitleBlockOnly = true, TitleBlockLines = TitleBlockPreviewService.Read(document, sheet),
                    Layout = new SheetLayoutInfo { SheetNumber = sheet.SheetNumber, SheetName = sheet.Name,
                        ViewName = "Khung tên", CenterXmm = mm(center.X), CenterYmm = mm(center.Y) },
                    MinXmm = mm(bounds.Min.U), MinYmm = mm(bounds.Min.V), MaxXmm = mm(bounds.Max.U), MaxYmm = mm(bounds.Max.V),
                    ScopeBaseXmm = mm(center.X), ScopeBaseYmm = mm(center.Y) };
                var window = new SheetLayoutPreviewWindow(new[] { preview }, layouts => { }) { Owner = this };
                IsEnabled = false;
                window.Closed += (owner, args) => { IsEnabled = true; UpdateCreateButton(); };
                try { window.Show(); }
                catch { IsEnabled = true; throw; }
            });
        }
        private static void CleanupPreview(string folder)
        {
            string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MEPSheetStudioPreview")) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(folder).StartsWith(root, StringComparison.OrdinalIgnoreCase)) return;
            try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        private void SaveLayout_Click(object sender, RoutedEventArgs e)
        {
            if (pending || floorPlanAssignments == null) return;
            try
            {
                var rows = floorPlanAssignments.Where(r => r.SelectedFloorPlan != null && r.SelectedFloorPlan.Id > 0).ToList();
                if (rows.Count == 0 || rows.Any(r => !r.HasPosition)) throw new InvalidOperationException("Mở Preview bố trí và dùng/lưu vị trí cho tất cả các dòng đã chọn trước.");
                var picker = new SaveFileDialog { Filter = "Sheet layout (*.xml)|*.xml", DefaultExt = ".xml", FileName = "SheetLayout.xml", OverwritePrompt = true };
                if (picker.ShowDialog(this) != true) return;
                SheetLayoutFile.Write(picker.FileName, rows.Select(r => new SheetLayoutInfo {
                    SheetUniqueId = r.SheetUniqueId, SheetNumber = r.Number, SheetName = r.Name,
                    ViewUniqueId = r.SelectedFloorPlan.UniqueId, ViewName = r.SelectedFloorPlan.Name,
                    CenterXmm = r.CenterXmm.Value, CenterYmm = r.CenterYmm.Value,
                    ScopeCentered = r.ScopeCentered, OffsetXmm = r.OffsetXmm, OffsetYmm = r.OffsetYmm }).ToList());
                StatusLabel.Text = "Đã lưu file bố trí: " + picker.FileName;
            }
            catch (Exception ex) { ShowError(ex); }
        }
        private void LoadLayout_Click(object sender, RoutedEventArgs e)
        {
            if (pending) return;
            var picker = new OpenFileDialog { Filter = "Sheet layout (*.xml)|*.xml", CheckFileExists = true };
            if (picker.ShowDialog(this) != true) return;
            try
            {
                var layouts = SheetLayoutFile.Read(picker.FileName);
                QueueRequest(() => {
                    var assignments = FloorPlanPlacementService.Read(document);
                    SheetLayoutFile.Apply(layouts, assignments);
                    BindFloorPlans(assignments);
                    MainTabs.SelectedIndex = 1;
                    StatusLabel.Text = "Đã nạp " + layouts.Count + " bố trí. Có thể Preview hoặc Áp dụng Floor Plan. Chưa thay đổi Revit.";
                });
            }
            catch (Exception ex) { ShowError(ex); }
        }
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (pending) return;
            CommitSheetEdits();
            bool json = (sender as System.Windows.Controls.MenuItem)?.Tag as string == "json";
            var picker = new SaveFileDialog { Filter = json ? "Sheet JSON (*.json)|*.json" : "Excel workbook (*.xlsx)|*.xlsx",
                DefaultExt = json ? ".json" : ".xlsx", FileName = json ? "Sheets.json" : "Sheets.xlsx", OverwritePrompt = true };
            if (picker.ShowDialog(this) != true) return;
            try
            {
                var sheets = imported ?? projectSheets;
                if (sheets == null) throw new InvalidOperationException("Chưa tải danh sách sheet.");
                if (json) SheetJsonFile.Write(picker.FileName, sheets); else SheetWorkbook.Write(picker.FileName, sheets);
                StatusLabel.Text = "Đã xuất " + sheets.Count + " dòng " + (imported != null ? "bản nháp" : "từ danh sách đang hiển thị") + ": " + picker.FileName;
            }
            catch (Exception ex) { ShowError(ex); }
        }
        private void Import_Click(object sender, RoutedEventArgs e)
        {
            if (pending) return;
            var picker = new OpenFileDialog { Filter = "Sheet data (*.xlsx;*.json)|*.xlsx;*.json|Excel workbook (*.xlsx)|*.xlsx|Sheet JSON (*.json)|*.json", CheckFileExists = true };
            if (picker.ShowDialog(this) != true) return;
            try
            {
                var rows = string.Equals(Path.GetExtension(picker.FileName), ".json", StringComparison.OrdinalIgnoreCase)
                    ? SheetJsonFile.Read(picker.FileName) : SheetWorkbook.Read(picker.FileName);
                QueueRequest(() => {
                    if (document.IsReadOnly) throw new InvalidOperationException("Project đang chỉ đọc.");
                    RefreshTitleBlocks(); ManualPanel.Visibility = System.Windows.Visibility.Collapsed;
                    ShowDraft(rows, "XEM TRƯỚC: " + Path.GetFileName(picker.FileName));
                });
            }
            catch (Exception ex) { ShowError(ex); }
        }
        private void Create_Click(object sender, RoutedEventArgs e)
        {
            if (pending || imported == null) return;
            CommitSheetEdits();
            var option = (TitleBlockOption)TitleBlocks.SelectedItem;
            var rows = imported;
            int count = rows.Count(r => r.Status == "Sẵn sàng");
            if (MessageBox.Show(this, "Tạo tối đa " + count + " sheet trong project: " + projectTitle + "?\nTitle block: " + option.Label
                + "\nSheet đã tồn tại được bỏ qua. Có thể Undo trong Revit.", "Tạo sheet", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            QueueRequest(() =>
            {
                // Recheck against the latest document, which may have changed while the window was open.
                int errors = SheetService.Validate(document, rows);
                SheetGrid.Items.Refresh();
                if (errors > 0) throw new InvalidOperationException("Dữ liệu có lỗi. Sửa số/tên sheet trong bảng rồi thử lại.");
                int created = SheetService.Create(document, rows, option.Id);
                int skipped = rows.Count - created;
                ShowCurrent();
                StatusLabel.Text = "Đã tạo " + created + " sheet. Bỏ qua " + skipped + " sheet đã tồn tại. Có thể Undo trong Revit.";
            });
        }
        private void ShowError(Exception ex)
        {
            StatusLabel.Text = ex.Message;
            if (loading)
            {
                LoadingLabel.Text = "Chưa tải được dữ liệu: " + ex.Message;
                LoadingProgress.Visibility = System.Windows.Visibility.Collapsed;
                RetryLoadButton.Visibility = System.Windows.Visibility.Visible;
            }
            MessageBox.Show(this, ex.Message, "Sheet Manager", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        private void ExportMenu_Click(object sender, RoutedEventArgs e) { OpenMenu(ExportButton); }
        private void NewSheetMenu_Click(object sender, RoutedEventArgs e) { OpenMenu(NewSheetButton); }
        private static void OpenMenu(System.Windows.Controls.Button button)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }
        private void ShowDraft(List<SheetInfo> rows, string heading)
        {
            imported = rows;
            foreach (var row in rows) row.CanEdit = true;
            MainTabs.SelectedIndex = 0;
            SheetGrid.ItemsSource = new System.Windows.Data.ListCollectionView(rows); SheetGrid.IsReadOnly = false;
            SheetActions.Visibility = System.Windows.Visibility.Visible;
            RowColumn.Visibility = StatusColumn.Visibility = System.Windows.Visibility.Visible;
            ListHeading.Text = heading;
            ValidateDraft();
            ApplySheetFilters();
        }
        private void ValidateDraft()
        {
            if (imported == null) return;
            int errors = SheetService.Validate(document, imported);
            SheetGrid.Items.Refresh();
            CountLabel.Text = imported.Count + " dòng. Sẵn sàng: " + imported.Count(r => r.Status == "Sẵn sàng")
                + ". Bỏ qua: " + imported.Count(r => r.Status.StartsWith("Bỏ qua:")) + ". Lỗi: " + errors + ".";
            StatusLabel.Text = errors > 0 ? "Sửa các dòng lỗi trong bảng. Chưa tạo sheet nào."
                : "Chọn title block rồi Tạo sheet, hoặc Xuất dữ liệu để lưu Excel/JSON trước.";
            UpdateCreateButton();
        }
        private void Manual_Click(object sender, RoutedEventArgs e)
        {
            if (pending) return;
            QueueRequest(() => {
                if (document.IsReadOnly) throw new InvalidOperationException("Project đang chỉ đọc.");
                projectSheets = SheetService.Read(document);
                if (imported == null) ShowDraft(new List<SheetInfo>(), "SHEET TẠO THỦ CÔNG");
                ManualPanel.Visibility = System.Windows.Visibility.Visible;
                ManualName.Text = "Unnamed"; ManualPlaceholder.IsChecked = false;
                SetNextManualNumber(); ManualNumber.Focus();
            });
        }
        private void SetNextManualNumber()
        {
            var numbers = new HashSet<string>((projectSheets ?? new List<SheetInfo>()).Concat(imported ?? new List<SheetInfo>())
                .Select(r => r.Number), StringComparer.OrdinalIgnoreCase);
            int index = 101; while (numbers.Contains("A" + index)) index++;
            ManualNumber.Text = "A" + index;
        }
        private void AddManual_Click(object sender, RoutedEventArgs e)
        {
            if (pending) return;
            CommitSheetEdits();
            var row = new SheetInfo(ManualNumber.Text.Trim(), ManualName.Text.Trim(), ManualPlaceholder.IsChecked == true)
                { CanEdit = true, ExcelRow = imported == null || imported.Count == 0 ? 1 : imported.Max(r => r.ExcelRow) + 1 };
            QueueRequest(() => {
                if (imported == null) ShowDraft(new List<SheetInfo>(), "SHEET TẠO THỦ CÔNG");
                imported.Add(row); ValidateDraft(); SetNextManualNumber();
            });
        }
        private void CommitSheetEdits()
        {
            SheetGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
            SheetGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        }
        private void SheetEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e) { ScheduleValidation(); }
        private void Placeholder_Click(object sender, RoutedEventArgs e) { ScheduleValidation(); }
        private void ScheduleValidation()
        {
            if (imported == null || pending || validationQueued) return;
            validationQueued = true;
            Dispatcher.BeginInvoke(new Action(() => {
                if (closed || pending || imported == null) { validationQueued = false; return; }
                CommitSheetEdits(); validationQueued = false; QueueRequest(ValidateDraft);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            BeginLoad();
        }
        private void Close_Click(object sender, RoutedEventArgs e) { Close(); }
        private void Minimize_Click(object sender, RoutedEventArgs e) { WindowState = WindowState.Minimized; }
        private void Maximize_Click(object sender, RoutedEventArgs e) { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
    }
}





