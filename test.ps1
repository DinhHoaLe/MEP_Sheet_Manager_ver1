param()
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -RevitVersion 2023
$taskMsBuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path -LiteralPath $taskMsBuild)) {
    $taskMsBuild = (Get-Command MSBuild.exe -ErrorAction Stop).Source
}
foreach ($project in @('tests\WorkbookTests.csproj', 'tests\RevitSheetTests.csproj')) {
    & $taskMsBuild (Join-Path $PSScriptRoot $project) /t:Build /nologo /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { throw "Build test thất bại: $project" }
}
& (Join-Path $PSScriptRoot 'tests\bin\WorkbookTests.exe') (Join-Path $PSScriptRoot 'tests\artifacts')
if ($LASTEXITCODE -ne 0) { throw 'Kiểm thử Excel thất bại.' }
$taskPowerShell64 = Join-Path $env:WINDIR 'Sysnative\WindowsPowerShell\v1.0\powershell.exe'
if (-not (Test-Path -LiteralPath $taskPowerShell64)) {
    $taskPowerShell64 = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
}
& $taskPowerShell64 -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'tests\RenderLayoutPreview.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Kiểm thử bố cục preview thất bại.' }
& $taskPowerShell64 -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'tests\RenderSheetManager.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Kiểm thử Sheet List/loading UI thất bại.' }
& $taskPowerShell64 -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'tests\RenderViewList.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Kiểm thử View List thất bại.' }
Write-Host 'Đã chạy test Excel. Test Revit chỉ được build; chạy RevitSheetTests trong Add-in Manager khi cần.'
