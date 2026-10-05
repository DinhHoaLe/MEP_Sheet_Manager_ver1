using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace MEP_Sheet_Manager
{
    public partial class SheetLayoutPreviewWindow : Window
    {
        private readonly IList<SheetLayoutPreview> previews;
        private readonly Action<IList<SheetLayoutInfo>> useLayouts;
        private SheetLayoutPreview current;
        private Border viewportBorder;
        private double pixelsPerMm, left, top;
        private bool dragging;
        private Point mouseStart;
        private double startX, startY;
        private readonly Dictionary<string, ImageSource> images = new Dictionary<string, ImageSource>();
        public SheetLayoutPreviewWindow(IList<SheetLayoutPreview> previews, Action<IList<SheetLayoutInfo>> useLayouts)
        {
            InitializeComponent();
            this.previews = previews; this.useLayouts = useLayouts;
            Sheets.ItemsSource = previews; Sheets.SelectedIndex = 0;
        }
        private void Sheet_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (Sheets == null || PreviewCanvas == null || XInput == null) return;
            current = Sheets.SelectedItem as SheetLayoutPreview;
            if (current != null)
            {
                SaveFileButton.IsEnabled = UseLayoutButton.IsEnabled = !current.TitleBlockOnly;
                XInput.IsReadOnly = YInput.IsReadOnly = current.TitleBlockOnly;
            }
            UpdateInputs(); Draw();
        }
        private void UpdateInputs()
        {
            if (current == null) return;
            XInput.Text = current.Layout.CenterXmm.ToString("0.###", CultureInfo.InvariantCulture);
            YInput.Text = current.Layout.CenterYmm.ToString("0.###", CultureInfo.InvariantCulture);
        }
        private void Add(UIElement element, double x, double y)
        {
            PreviewCanvas.Children.Add(element); Canvas.SetLeft(element, x); Canvas.SetTop(element, y);
        }
        private void Draw()
        {
            if (current == null || PreviewCanvas.ActualWidth < 80 || PreviewCanvas.ActualHeight < 80) return;
            PreviewCanvas.Children.Clear();
            double width = current.MaxXmm - current.MinXmm, height = current.MaxYmm - current.MinYmm;
            pixelsPerMm = Math.Min((PreviewCanvas.ActualWidth - 70) / width, (PreviewCanvas.ActualHeight - 70) / height);
            left = (PreviewCanvas.ActualWidth - width * pixelsPerMm) / 2;
            top = (PreviewCanvas.ActualHeight - height * pixelsPerMm) / 2;
            Add(new Rectangle { Width = width * pixelsPerMm, Height = height * pixelsPerMm, Fill = Brushes.White,
                Stroke = Brushes.Gray, StrokeThickness = 1 }, left, top);
            Add(new Rectangle { Width = SheetLayoutMargins.LeftMm * pixelsPerMm, Height = height * pixelsPerMm,
                Fill = new SolidColorBrush(Color.FromRgb(235, 224, 201)), Stroke = Brushes.Tan, StrokeThickness = 1 }, left, top);
            Add(new TextBlock { Text = "44 mm", Foreground = Brushes.SaddleBrown, FontSize = 12 }, left + 3, top + 8);
            Add(new Rectangle { Width = SheetLayoutMargins.RightMm * pixelsPerMm, Height = height * pixelsPerMm, Fill = new SolidColorBrush(Color.FromRgb(235, 224, 201)),
                Stroke = new SolidColorBrush(Color.FromRgb(144, 128, 92)), StrokeThickness = 1 }, left + (width - SheetLayoutMargins.RightMm) * pixelsPerMm, top);
            Add(new TextBlock { Text = "94 mm", Foreground = Brushes.SaddleBrown, FontSize = 12 }, left + (width - SheetLayoutMargins.RightMm) * pixelsPerMm + 5, top + 8);
            if (current.TitleBlockLines != null)
                foreach (var line in current.TitleBlockLines)
                    Add(new Line { X1 = (line.X1 - current.MinXmm) * pixelsPerMm, X2 = (line.X2 - current.MinXmm) * pixelsPerMm,
                        Y1 = (current.MaxYmm - line.Y1) * pixelsPerMm, Y2 = (current.MaxYmm - line.Y2) * pixelsPerMm,
                        Stroke = Brushes.DimGray, StrokeThickness = 0.7, IsHitTestVisible = false }, left, top);
            if (current.TitleBlockOnly)
            {
                double cx = left + (current.ScopeBaseXmm - current.MinXmm) * pixelsPerMm;
                double cy = top + (current.MaxYmm - current.ScopeBaseYmm) * pixelsPerMm;
                Add(new Line { X1 = -10, X2 = 10, Stroke = Brushes.Blue, StrokeThickness = 2 }, cx, cy);
                Add(new Line { Y1 = -10, Y2 = 10, Stroke = Brushes.Blue, StrokeThickness = 2 }, cx, cy);
                PreviewStatus.Text = "Review khung tên và tâm vùng giấy (dấu cộng xanh). Chọn Floor Plan trong View List để canh và lưu bố trí. Khung tên hiển thị đường nét, không gồm chữ/logo.";
                return;
            }
            double x = left + (current.Layout.CenterXmm - current.MinXmm - current.ViewWidthMm / 2) * pixelsPerMm;
            double y = top + (current.MaxYmm - current.Layout.CenterYmm - current.ViewHeightMm / 2) * pixelsPerMm;
            bool outside = current.Layout.CenterXmm - current.ViewWidthMm / 2 < current.MinXmm + SheetLayoutMargins.LeftMm
                || current.Layout.CenterXmm + current.ViewWidthMm / 2 > current.MaxXmm - SheetLayoutMargins.RightMm
                || current.Layout.CenterYmm - current.ViewHeightMm / 2 < current.MinYmm
                || current.Layout.CenterYmm + current.ViewHeightMm / 2 > current.MaxYmm;
            viewportBorder = new Border { Width = Math.Max(2, current.ViewWidthMm * pixelsPerMm), Height = Math.Max(2, current.ViewHeightMm * pixelsPerMm),
                BorderBrush = outside ? Brushes.Firebrick : new SolidColorBrush(Color.FromRgb(98, 84, 60)), BorderThickness = new Thickness(2),
                Background = new SolidColorBrush(Color.FromArgb(40, 144, 128, 92)), Cursor = Cursors.SizeAll };
            if (!string.IsNullOrEmpty(current.ImagePath))
            {
                try
                {
                    ImageSource source;
                    if (!images.TryGetValue(current.ImagePath, out source))
                    {
                        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(current.ImagePath); bitmap.EndInit(); bitmap.Freeze();
                        images[current.ImagePath] = source = bitmap;
                    }
                    viewportBorder.Child = new Image { Source = source, Stretch = Stretch.Fill,
                        LayoutTransform = new RotateTransform(current.RotationDegrees) };
                }
                catch { viewportBorder.Child = new TextBlock { Text = current.Layout.ViewName, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8) }; }
            }
            else viewportBorder.Child = new TextBlock { Text = current.Layout.ViewName, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8) };
            Add(viewportBorder, x, y);
            if (current.Layout.ScopeCentered)
            {
                double scopeX = current.Layout.CenterXmm + (current.MinXmm + SheetLayoutMargins.LeftMm + current.MaxXmm - SheetLayoutMargins.RightMm) / 2 - current.ScopeBaseXmm;
                double scopeY = current.Layout.CenterYmm + (current.MinYmm + current.MaxYmm) / 2 - current.ScopeBaseYmm;
                double px = left + (scopeX - current.MinXmm) * pixelsPerMm;
                double py = top + (current.MaxYmm - scopeY) * pixelsPerMm;
                Add(new Line { X1 = -8, X2 = 8, Y1 = 0, Y2 = 0, Stroke = Brushes.Blue, StrokeThickness = 2, IsHitTestVisible = false }, px, py);
                Add(new Line { X1 = 0, X2 = 0, Y1 = -8, Y2 = 8, Stroke = Brushes.Blue, StrokeThickness = 2, IsHitTestVisible = false }, px, py);
            }
            Add(new TextBlock { Text = current.Layout.SheetNumber + "  |  " + width.ToString("0.#") + " × " + height.ToString("0.#") + " mm",
                Foreground = Brushes.DimGray }, left, top + height * pixelsPerMm + 8);
            PreviewStatus.Text = (outside ? "Khung viewport vượt vùng bản vẽ hoặc vào vùng khung tên. Chỉnh vị trí/crop/scale trước khi áp dụng. " : "Khung viewport nằm trong vùng bản vẽ. ")
                + (current.Layout.ScopeCentered ? " | Canh giữa tự động + độ dịch mm. " : " | Tọa độ tuyệt đối từ file cũ. ")
                + "X/Y là tâm viewport; khung đo không gồm nhãn viewport. " + current.ImageWarning;
        }
        private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e) { Draw(); }
        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (current == null || viewportBorder == null || !viewportBorder.IsMouseOver) return;
            dragging = true; mouseStart = e.GetPosition(PreviewCanvas); startX = current.Layout.CenterXmm; startY = current.Layout.CenterYmm;
            PreviewCanvas.CaptureMouse(); e.Handled = true;
        }
        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!dragging || current == null || pixelsPerMm <= 0) return;
            var mouse = e.GetPosition(PreviewCanvas);
            current.Layout.CenterXmm = startX + (mouse.X - mouseStart.X) / pixelsPerMm;
            current.Layout.CenterYmm = startY - (mouse.Y - mouseStart.Y) / pixelsPerMm;
            UpdateScopeOffsets();
            UpdateInputs(); Draw();
        }
        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e) { dragging = false; PreviewCanvas.ReleaseMouseCapture(); }
        private void Canvas_LostCapture(object sender, MouseEventArgs e) { dragging = false; }
        private bool ReadInputs()
        {
            if (current == null) return false;
            double x, y;
            if (!double.TryParse(XInput.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                || !double.TryParse(YInput.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                || double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y))
            { MessageBox.Show(this, "Nhập X/Y bằng số mm hợp lệ.", "Tọa độ"); return false; }
            current.Layout.CenterXmm = x; current.Layout.CenterYmm = y;
            UpdateScopeOffsets();
            return true;
        }
        private void UpdateScopeOffsets()
        {
            if (current != null && current.Layout.ScopeCentered)
            {
                current.Layout.OffsetXmm = current.Layout.CenterXmm - current.ScopeBaseXmm;
                current.Layout.OffsetYmm = current.Layout.CenterYmm - current.ScopeBaseYmm;
            }
        }
        private void Coordinates_Click(object sender, RoutedEventArgs e) { if (ReadInputs()) { UpdateInputs(); Draw(); } }
        private void Center_Click(object sender, RoutedEventArgs e)
        {
            if (current == null) return;
            current.Layout.ScopeCentered = true;
            current.Layout.CenterXmm = current.ScopeBaseXmm;
            current.Layout.CenterYmm = current.ScopeBaseYmm;
            current.Layout.OffsetXmm = current.Layout.OffsetYmm = 0;
            UpdateInputs(); Draw();
        }
        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            if (!ReadInputs()) return;
            var picker = new SaveFileDialog { Filter = "Sheet layout (*.xml)|*.xml", DefaultExt = ".xml", FileName = "SheetLayout.xml", OverwritePrompt = true };
            if (picker.ShowDialog(this) != true) return;
            try
            {
                var layouts = previews.Select(p => p.Layout).ToList();
                SheetLayoutFile.Write(picker.FileName, layouts); useLayouts(layouts);
                PreviewStatus.Text = "Đã lưu tọa độ mm: " + picker.FileName + ". Chưa thay đổi viewport trong Revit.";
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Lưu bố trí"); }
        }
        private void Use_Click(object sender, RoutedEventArgs e)
        {
            if (!ReadInputs()) return;
            try { useLayouts(previews.Select(p => p.Layout).ToList()); Close(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Dùng bố trí"); }
        }
        private void Close_Click(object sender, RoutedEventArgs e) { Close(); }
    }
}
