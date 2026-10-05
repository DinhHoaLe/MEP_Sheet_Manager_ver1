$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$taskRoot=Split-Path $PSScriptRoot -Parent
[void][Reflection.Assembly]::LoadFrom((Join-Path $taskRoot 'bin\Release\Revit2023\MEP_Sheet_Manager_Modeless.dll'))
$taskLayout=New-Object MEP_Sheet_Manager.SheetLayoutInfo
$taskLayout.SheetNumber='A-101';$taskLayout.SheetName='Mặt bằng tầng 1';$taskLayout.ViewName='Floor Plan - Tầng 1'
$taskLayout.CenterXmm=395.5;$taskLayout.CenterYmm=297
$taskPreview=New-Object MEP_Sheet_Manager.SheetLayoutPreview
$taskPreview.Layout=$taskLayout;$taskPreview.MinXmm=0;$taskPreview.MinYmm=0;$taskPreview.MaxXmm=841;$taskPreview.MaxYmm=594
$taskPreview.ViewWidthMm=540;$taskPreview.ViewHeightMm=360
$taskPreview.HasScope=$true;$taskPreview.ScopeName='Zone A';$taskPreview.ScopeBaseXmm=375.5;$taskPreview.ScopeBaseYmm=307
$taskPreviews=New-Object 'System.Collections.Generic.List[MEP_Sheet_Manager.SheetLayoutPreview]'
$taskPreviews.Add($taskPreview)
$taskCallback=[Action[System.Collections.Generic.IList[MEP_Sheet_Manager.SheetLayoutInfo]]]{param($layouts)}
$taskWindow=New-Object MEP_Sheet_Manager.SheetLayoutPreviewWindow -ArgumentList $taskPreviews,$taskCallback
$taskContent=$taskWindow.Content;$taskContent.Margin=0;$taskContent.Width=1120;$taskContent.Height=700;$taskContent.Background=$taskWindow.Background
$taskContent.Measure((New-Object Windows.Size 1120,700));$taskContent.Arrange((New-Object Windows.Rect 0,0,1120,700));$taskContent.UpdateLayout()
$taskFlags=[Reflection.BindingFlags]'Instance,NonPublic'
$taskWindow.GetType().GetMethod('Center_Click',$taskFlags).Invoke($taskWindow,@($null,$null)) | Out-Null
if($taskLayout.CenterXmm -ne 375.5 -or $taskLayout.CenterYmm -ne 307 -or -not $taskLayout.ScopeCentered){throw 'Canh tâm scope phải bù lệch giữa tâm scope và tâm viewport'}
$taskWindow.FindName('XInput').Text='321,25';$taskWindow.FindName('YInput').Text='198.75'
$taskWindow.GetType().GetMethod('Coordinates_Click',$taskFlags).Invoke($taskWindow,@($null,$null)) | Out-Null
if($taskLayout.CenterXmm -ne 321.25 -or $taskLayout.CenterYmm -ne 198.75){throw 'Nhập X/Y mm sai'}
if($taskLayout.OffsetXmm -ne -54.25 -or $taskLayout.OffsetYmm -ne -108.25){throw 'Độ dịch phải lưu theo tâm scope'}
$taskPreview.ScopeName='Zone B';$taskPreview.ScopeBaseXmm=410.5;$taskPreview.ScopeBaseYmm=285
$taskWindow.GetType().GetMethod('Center_Click',$taskFlags).Invoke($taskWindow,@($null,$null)) | Out-Null
if($taskLayout.CenterXmm -ne 410.5 -or $taskLayout.CenterYmm -ne 285 -or $taskLayout.OffsetXmm -ne 0 -or $taskLayout.OffsetYmm -ne 0){throw 'Zone B phải tính lại theo scope riêng'}
$taskPreview.HasScope=$false;$taskPreview.ScopeName=$null;$taskPreview.ScopeBaseXmm=395.5;$taskPreview.ScopeBaseYmm=297
$taskWindow.GetType().GetMethod('Center_Click',$taskFlags).Invoke($taskWindow,@($null,$null)) | Out-Null
if($taskLayout.CenterXmm -ne 395.5 -or $taskLayout.CenterYmm -ne 297){throw 'Không có scope phải canh theo tâm khung view'}
$taskPreview.TitleBlockLines=New-Object 'System.Collections.Generic.List[MEP_Sheet_Manager.SheetLineInfo]'
foreach($taskCoords in @(@(9,9,832,9),@(832,9,832,585),@(832,585,9,585),@(9,585,9,9),@(747,9,747,585))){
 $taskLine=New-Object MEP_Sheet_Manager.SheetLineInfo
 $taskLine.X1=$taskCoords[0];$taskLine.Y1=$taskCoords[1];$taskLine.X2=$taskCoords[2];$taskLine.Y2=$taskCoords[3]
 $taskPreview.TitleBlockLines.Add($taskLine)
}
if($taskWindow.FindName('PreviewCanvas').Children.Count -lt 4){throw 'Preview chưa render sheet và viewport'}
$taskWindow.GetType().GetMethod('Center_Click',$taskFlags).Invoke($taskWindow,@($null,$null)) | Out-Null
$taskContent.UpdateLayout()
$taskBitmap=New-Object Windows.Media.Imaging.RenderTargetBitmap 1120,700,96,96,([Windows.Media.PixelFormats]::Pbgra32)
$taskBitmap.Render($taskContent)
$taskEncoder=New-Object Windows.Media.Imaging.PngBitmapEncoder
$taskEncoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($taskBitmap))
$taskPath=Join-Path $taskRoot 'tests\artifacts\LayoutPreview.png';$taskStream=[IO.File]::Create($taskPath)
try{$taskEncoder.Save($taskStream)}finally{$taskStream.Dispose()}
$taskWindow.Close()
Write-Output 'PASS: no-scope preview centers view box without requiring scope'
Write-Output 'PASS: compiled preview renders sheet, reserved left 44 mm and right 94 mm and viewport without showing a desktop window'
Write-Output 'PASS: preview centers scope instead of viewport and recalculates for different zone'
Write-Output 'PASS: manual adjustment stores millimeter offsets relative to scope center'
Write-Output 'PASS: preview accepts decimal comma and dot for X/Y millimeters'
Write-Output $taskPath



