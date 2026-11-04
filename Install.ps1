param([ValidateSet('1.6','1.7')][string]$GameVersion = '1.6')
$ErrorActionPreference = 'Stop'
$stageFile = Join-Path $PSScriptRoot $(if ($GameVersion -eq '1.7') { 'Build\1.7\ScpFoundation.dll' } else { 'Build\Assemblies\ScpFoundation.dll' })
if (!(Test-Path -LiteralPath $stageFile)) { throw 'No staged DLL. Run Build.ps1 -StageOnly first.' }
$installDir = Join-Path $PSScriptRoot ($GameVersion + '\Assemblies')
[void][IO.Directory]::CreateDirectory($installDir)
try { Copy-Item -LiteralPath $stageFile -Destination (Join-Path $installDir 'ScpFoundation.dll') -Force }
catch { throw "Close RimWorld before installing the staged DLL. $($_.Exception.Message)" }
Write-Output 'Installed ScpFoundation.dll. Restart RimWorld to load the new code and XML.'
