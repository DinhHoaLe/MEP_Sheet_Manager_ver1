$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskAssembly=[Reflection.Assembly]::LoadFrom((Join-Path $taskRoot 'bin\Release\Revit2023\MEP_Sheet_Manager_Modeless.dll'))
[xml]$taskXaml=Get-Content -LiteralPath (Join-Path $taskRoot 'src\UI\Wpf\SheetManagerWindow.xaml') -Raw -Encoding UTF8
foreach($taskElement in $taskXaml.SelectNodes('//*')) {
 foreach($taskAttribute in @($taskElement.Attributes)) {
  if($taskAttribute.LocalName -in @('Class','Click','SelectionChanged','CellEditEnding','TextChanged')){[void]$taskElement.Attributes.Remove($taskAttribute)}
 }
}
$taskReader=New-Object System.Xml.XmlNodeReader $taskXaml
$taskWindow=[Windows.Markup.XamlReader]::Load($taskReader)
$taskImage=$taskWindow.FindName('LoadingPanel').Children[0].Child
if($taskImage.Source.PixelWidth -lt 100){throw 'Embedded splash image failed to load'}
$taskGrid=$taskWindow.FindName('SheetGrid')
if($taskGrid.Columns[0].Header -ne 'Placeholder'){throw 'Placeholder must be first column'}
if(@($taskGrid.Columns | Where-Object {$_.Header -eq 'Trạng thái'}).Count -ne 0){throw 'Placement status must be shown only in Step 2'}
if($taskWindow.FindName('LoadingSheets') -ne $null){throw 'Opening tool must not scan placement states'}
if($taskWindow.FindName('ExportButton').ContextMenu.Items.Count -ne 2){throw 'Missing Excel/JSON export menu'}
if($taskWindow.FindName('NewSheetButton').ContextMenu.Items.Count -ne 2){throw 'Missing import/manual creation menu'}
$taskContent=$taskWindow.Content
$taskContent.Background=$taskWindow.Background
function Save-TaskUi([string]$taskName,[int]$taskWidth=1180,[int]$taskHeight=790) {
 $taskContent.Width=$taskWidth;$taskContent.Height=$taskHeight
 $taskContent.Measure((New-Object Windows.Size $taskWidth,$taskHeight))
 $taskContent.Arrange((New-Object Windows.Rect 0,0,$taskWidth,$taskHeight));$taskContent.UpdateLayout()
 [Windows.Threading.Dispatcher]::CurrentDispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::ApplicationIdle);$taskContent.UpdateLayout()
 $taskBitmap=New-Object Windows.Media.Imaging.RenderTargetBitmap $taskWidth,$taskHeight,96,96,([Windows.Media.PixelFormats]::Pbgra32)
 $taskBitmap.Render($taskContent)
 $taskEncoder=New-Object Windows.Media.Imaging.PngBitmapEncoder;$taskEncoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($taskBitmap))
 $taskStream=[IO.File]::Create((Join-Path $taskRoot ('tests\artifacts\'+$taskName+'.png')))
 try{$taskEncoder.Save($taskStream)}finally{$taskStream.Dispose()}
}
Save-TaskUi 'SheetLoadingPreview'
$taskWindow.FindName('LoadingPanel').Visibility='Collapsed'
foreach($taskName in @('HeaderPanel','BodyGrid','FooterGrid')){$taskWindow.FindName($taskName).Visibility='Visible'}
$taskWindow.FindName('ProjectLabel').Text='Project: MEP Sheet Studio — dữ liệu minh họa'
$taskWindow.FindName('StatusLabel').Text='Sheet hiện hữu hiển thị trạng thái. Chọn Nhập / Tạo sheet để thêm bản nháp.'
$taskRows=New-Object 'System.Collections.Generic.List[MEP_Sheet_Manager.SheetInfo]'
$taskRow=New-Object MEP_Sheet_Manager.SheetInfo -ArgumentList 'A101','MẶT BẰNG ĐIỆN TẦNG 1',$false;$taskRows.Add($taskRow)
$taskRow=New-Object MEP_Sheet_Manager.SheetInfo -ArgumentList 'P001','DANH SÁCH SHEET DỰ KIẾN',$true;$taskRows.Add($taskRow)
$taskGrid.ItemsSource=$taskRows
$taskWindow.FindName('CountLabel').Text='2 sheet. 1 placeholder.'
$taskSets=@([pscustomobject]@{Name='V/S Sets: All'},[pscustomobject]@{Name='MEP - Submission'})
foreach($taskComboName in @('SheetSetFilter','ViewSetFilter','RevisionSetFilter')) {$taskWindow.FindName($taskComboName).ItemsSource=$taskSets;$taskWindow.FindName($taskComboName).SelectedIndex=0}
Save-TaskUi 'SheetListPreview'
$taskWindow.FindName('SheetActions').Visibility='Visible';$taskWindow.FindName('ManualPanel').Visibility='Visible'
$taskWindow.FindName('TitleBlocks').ItemsSource=@([pscustomobject]@{Label='A1 Metric : A1 Standard'});$taskWindow.FindName('TitleBlocks').SelectedIndex=0
$taskWindow.FindName('ManualNumber').Text='A102';$taskWindow.FindName('ManualName').Text='MẶT BẰNG NƯỚC TẦNG 1'
$taskWindow.FindName('ListHeading').Text='SHEET TẠO THỦ CÔNG'
$taskWindow.FindName('RowColumn').Visibility='Visible';$taskWindow.FindName('StatusColumn').Visibility='Visible'
$taskWindow.FindName('CreateButton').IsEnabled=$true;$taskGrid.IsReadOnly=$false
foreach($taskRow in $taskRows){$taskRow.CanEdit=$true;$taskRow.Status='Sẵn sàng'}
$taskWindow.FindName('StatusLabel').Text='Xuất dữ liệu để lưu bản nháp Excel/JSON. Bấm Tạo sheet để ghi vào project.'
$taskGrid.Items.Refresh()
Save-TaskUi 'ManualSheetPreview'
Save-TaskUi 'ManualSheetNarrowPreview' 940 720
if($taskGrid.ActualHeight -lt 95){throw 'Manual layout must leave room for header and a visible sheet row'}
$taskCheck=$taskGrid.Columns[0].CellTemplate.LoadContent()
$taskCheck.DataContext=$taskRows[0]
[Windows.Threading.Dispatcher]::CurrentDispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::ApplicationIdle)
$taskCheck.GetBindingExpression([Windows.Controls.Primitives.ToggleButton]::IsCheckedProperty).UpdateTarget()
$taskCheck.SetCurrentValue([Windows.Controls.Primitives.ToggleButton]::IsCheckedProperty, $true)
$taskCheck.GetBindingExpression([Windows.Controls.Primitives.ToggleButton]::IsCheckedProperty).UpdateSource()
if(-not $taskRows[0].IsPlaceholder){throw 'Placeholder tick did not update draft data'}
$taskWindow.FindName('MainTabs').SelectedIndex=2
$taskRevisionRows=@([pscustomobject]@{Number='A101';Name='MẶT BẰNG ĐIỆN TẦNG 1';Summary='1 · Phát hành lần đầu';CurrentNumber='P01';CurrentDate='06/10/2026';CurrentDescription='Phát hành lần đầu'},[pscustomobject]@{Number='A102';Name='MẶT BẰNG NƯỚC TẦNG 1';Summary='<None> ▾';CurrentNumber='';CurrentDate='';CurrentDescription=''})
$taskWindow.FindName('RevisionGrid').ItemsSource=$taskRevisionRows
$taskWindow.FindName('RevisionCountLabel').Text='2 sheet · Chọn nhiều revision cho từng sheet, rồi bấm Apply.'
$taskWindow.FindName('RevisionSearch').Text=''
if(@($taskWindow.FindName('RevisionGrid').Columns | Where-Object {$_.Header -eq 'Current Revision'}).Count -ne 1){throw 'Missing sheet current revision column'}
Save-TaskUi 'RevisionListPreview'
if(@($taskWindow.FindName('RevisionGrid').Columns | Where-Object {$_.ActualWidth -lt 110}).Count -ne 0){throw 'Revision columns must remain readable'}
Save-TaskUi 'RevisionListNarrowPreview' 940 720
$taskWindow.Close()
Write-Output 'PASS: splash loads AI image embedded in DLL without a desktop window'
Write-Output 'PASS: Sheet List contains Excel/JSON export and import/manual creation menus'
Write-Output 'PASS: Placeholder is first column and checkbox updates draft data'
Write-Output 'PASS: loading, project and manual draft layouts render at default and minimum width'







