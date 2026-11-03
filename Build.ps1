param([string]$RimWorldDir = (Join-Path $PSScriptRoot '..\..'), [switch]$StageOnly)
$ErrorActionPreference = 'Stop'
$gameRoot = (Resolve-Path -LiteralPath $RimWorldDir).Path
$buildTool = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'
if (!(Test-Path -LiteralPath $buildTool)) { throw 'The Windows .NET Framework 4.x MSBuild tool is required.' }
$versionFile = Join-Path $gameRoot 'Version.txt'
if (!(Test-Path -LiteralPath $versionFile)) { throw 'RimWorld Version.txt was not found.' }
$gameVersion = (Get-Content -LiteralPath $versionFile -Raw).Trim()
if ($gameVersion -notmatch '^1\.6\.') { throw "This prototype targets RimWorld 1.6; found: $gameVersion" }
Write-Output "Building for RimWorld $gameVersion"
$stageDir = Join-Path $PSScriptRoot 'Build\Assemblies'
& $buildTool (Join-Path $PSScriptRoot 'Source\ScpFoundation\ScpFoundation.csproj') /t:Build "/p:RimWorldDir=$gameRoot" "/p:OutputPath=$stageDir" /verbosity:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }
$combatExtendedDir = Join-Path $gameRoot 'Mods\Combat'
$combatExtendedAssembly = Join-Path $combatExtendedDir 'Assemblies\CombatExtended.dll'
if (Test-Path -LiteralPath $combatExtendedAssembly) {
    $foundationAssembly = Join-Path $stageDir 'ScpFoundation.dll'
    & $buildTool (Join-Path $PSScriptRoot 'Source\ScpFoundation.CombatExtended\ScpFoundation.CombatExtended.csproj') /t:Build "/p:RimWorldDir=$gameRoot" "/p:CombatExtendedDir=$combatExtendedDir" "/p:FoundationAssembly=$foundationAssembly" /verbosity:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw "Combat Extended adapter build failed with exit code $LASTEXITCODE" }
    Write-Output 'Built optional Combat Extended adapter.'
}
if (!$StageOnly) {
    & (Join-Path $PSScriptRoot 'Install.ps1')
}
