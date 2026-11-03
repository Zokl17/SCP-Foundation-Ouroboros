$ErrorActionPreference = 'Stop'
$stageFile = Join-Path $PSScriptRoot 'Build\Assemblies\ScpFoundation.dll'
if (!(Test-Path -LiteralPath $stageFile)) { throw 'No staged DLL. Run Build.ps1 -StageOnly first.' }
$installDir = Join-Path $PSScriptRoot '1.6\Assemblies'
[void][IO.Directory]::CreateDirectory($installDir)
try { Copy-Item -LiteralPath $stageFile -Destination (Join-Path $installDir 'ScpFoundation.dll') -Force }
catch { throw "Close RimWorld before installing the staged DLL. $($_.Exception.Message)" }
Write-Output 'Installed ScpFoundation.dll. Restart RimWorld to load the new code and XML.'
