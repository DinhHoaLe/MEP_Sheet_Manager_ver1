param(
    [ValidateSet('2023', '2024')][string]$RevitVersion = '2023'
)
$ErrorActionPreference = 'Stop'
$taskMsBuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path -LiteralPath $taskMsBuild)) {
    $taskMsBuild = (Get-Command MSBuild.exe -ErrorAction Stop).Source
}
& $taskMsBuild (Join-Path $PSScriptRoot 'MEP_Sheet_Manager.csproj') /t:Build /p:Configuration=Release "/p:RevitVersion=$RevitVersion" /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw "Build thất bại: $LASTEXITCODE" }
