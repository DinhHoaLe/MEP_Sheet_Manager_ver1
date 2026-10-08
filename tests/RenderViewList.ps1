$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$taskProjectRoot=Split-Path $PSScriptRoot -Parent
[void][Reflection.Assembly]::LoadFrom((Join-Path $taskProjectRoot 'bin\Release\Revit2023\MEP_Sheet_Manager_Modeless.dll'))
[xml]$taskXaml=Get-Content -LiteralPath (Join-Path $taskProjectRoot 'src\UI\Wpf\SheetManagerWindow.xaml') -Raw -Encoding UTF8
foreach($taskElement in $taskXaml.SelectNodes('//*')) {
    foreach($taskAttribute in @($taskElement.Attributes)) {
        if($taskAttribute.LocalName -in @('Class','Click','SelectionChanged','CellEditEnding','TextChanged')) { [void]$taskElement.Attributes.Remove($taskAttribute) }
    }
}

$taskReader=New-Object System.Xml.XmlNodeReader $taskXaml
$taskWindow=[Windows.Markup.XamlReader]::Load($taskReader)
$taskWindow.FindName('LoadingPanel').Visibility='Collapsed';foreach($taskName in @('HeaderPanel','BodyGrid','FooterGrid')){$taskWindow.FindName($taskName).Visibility='Visible'}
$taskWindow.FindName('ViewTemplateFilter').ItemsSource=@('Template: All','MEP - Electrical');$taskWindow.FindName('ViewTemplateFilter').SelectedIndex=0
$taskWindow.FindName('ViewSetFilter').ItemsSource=@([pscustomobject]@{Name='V/S Sets: All'});$taskWindow.FindName('ViewSetFilter').SelectedIndex=0
$taskTabs=$taskWindow.FindName('MainTabs');$taskTabs.SelectedIndex=1
$taskWindow.FindName('SheetActions').Visibility='Collapsed'
$taskWindow.FindName('ActionsColumn').Width=0
$taskWindow.FindName('SpacerColumn').Width=0
$taskChoices=@([pscustomobject]@{Name='<Không chọn>'},[pscustomobject]@{Name='Floor Plan - Tầng 1'},[pscustomobject]@{Name='Floor Plan - Tầng 2'})
$taskRows=@(
 [pscustomobject]@{Number='A-101';Name='MẶT BẰNG TẦNG 1';FloorPlans=$taskChoices;SelectedFloorPlan=$taskChoices[1];CurrentFloorPlans='';Status='Đã chọn, chưa đặt';ProcessingStatus='Done';PositionLabel='Độ dịch X: 0; Y: 0 mm'},
 [pscustomobject]@{Number='A-102';Name='MẶT BẰNG TẦNG 2';FloorPlans=$taskChoices;SelectedFloorPlan=$taskChoices[0];CurrentFloorPlans='';Status='';ProcessingStatus='Done'}
)
$taskWindow.FindName('ViewGrid').ItemsSource=$taskRows
$taskWindow.FindName('ViewGrid').SelectedIndex=0
$taskWindow.FindName('ProjectLabel').Text='Xem trước bố cục — dữ liệu minh họa'
$taskWindow.FindName('ViewCountLabel').Text='Chọn Floor Plan tương ứng cho từng sheet, rồi bấm nút đặt.'
$taskWindow.FindName('PlacePlansButton').IsEnabled=$true
$taskContent=$taskWindow.Content
$taskContent.Background=$taskWindow.Background
$taskContent.Width=1180;$taskContent.Height=748
$taskContent.Measure((New-Object Windows.Size 1180,748))
$taskContent.Arrange((New-Object Windows.Rect 0,0,1180,748))
$taskContent.UpdateLayout()
if(@($taskWindow.FindName('ViewGrid').Columns | Where-Object {$_.Header -in @('Scope Box','Vị trí (mm)')}).Count -ne 0){throw 'Hidden View List columns still present'}
$taskBitmap=New-Object Windows.Media.Imaging.RenderTargetBitmap 1180,748,96,96,([Windows.Media.PixelFormats]::Pbgra32)
$taskBitmap.Render($taskContent)
$taskEncoder=New-Object Windows.Media.Imaging.PngBitmapEncoder
$taskEncoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($taskBitmap))
$taskOutput=Join-Path $taskProjectRoot 'tests\artifacts\ViewListPreview.png'
$taskStream=[IO.File]::Create($taskOutput)
try{$taskEncoder.Save($taskStream)}finally{$taskStream.Dispose()}
$taskWindow.Close()
Write-Output $taskOutput




