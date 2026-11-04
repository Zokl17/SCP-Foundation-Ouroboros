param([string]$RimWorldDir = (Join-Path $PSScriptRoot '..\..'), [switch]$StageOnly, [string]$HarmonyAssembly)
$ErrorActionPreference = 'Stop'
$gameRoot = (Resolve-Path -LiteralPath $RimWorldDir).Path
if ((Test-Path -LiteralPath (Join-Path $gameRoot 'Assembly-CSharp.dll')) -and
        (Test-Path -LiteralPath (Join-Path $gameRoot 'UnityStubs.dll'))) {
    & (Join-Path $PSScriptRoot 'Tools\Build-RimWorld17.ps1') -RimWorldDir $gameRoot
    if (!$StageOnly) { & (Join-Path $PSScriptRoot 'Install.ps1') -GameVersion '1.7' }
    return
}
$buildTool = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'
if (!(Test-Path -LiteralPath $buildTool)) { throw 'The Windows .NET Framework 4.x MSBuild tool is required.' }
$versionFile = Join-Path $gameRoot 'Version.txt'
if (!(Test-Path -LiteralPath $versionFile)) { throw 'RimWorld Version.txt was not found.' }
$gameVersion = (Get-Content -LiteralPath $versionFile -Raw).Trim()
if ($gameVersion -notmatch '^1\.6\.') { throw "This prototype targets RimWorld 1.6; found: $gameVersion" }
Write-Output "Building for RimWorld $gameVersion"
if (!$HarmonyAssembly) {
    $HarmonyAssembly = Join-Path $gameRoot 'Mods\Harmony\1.6\Assemblies\0Harmony.dll'
    if (!(Test-Path -LiteralPath $HarmonyAssembly)) {
        $HarmonyAssembly = Join-Path $gameRoot 'Mods\Harmony\Current\Assemblies\0Harmony.dll'
    }
}
if (!(Test-Path -LiteralPath $HarmonyAssembly)) { throw 'Install the separate Harmony mod or specify -HarmonyAssembly.' }
$HarmonyAssembly = (Resolve-Path -LiteralPath $HarmonyAssembly).Path
$stageDir = Join-Path $PSScriptRoot 'Build\Assemblies'
& $buildTool (Join-Path $PSScriptRoot 'Source\ScpFoundation\ScpFoundation.csproj') /t:Build "/p:RimWorldDir=$gameRoot" "/p:HarmonyAssembly=$HarmonyAssembly" "/p:OutputPath=$stageDir" /verbosity:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }
$combatExtendedDir = Join-Path $gameRoot 'Mods\Combat'
$combatExtendedAssembly = Join-Path $combatExtendedDir 'Assemblies\CombatExtended.dll'
if (Test-Path -LiteralPath $combatExtendedAssembly) {
    $foundationAssembly = Join-Path $stageDir 'ScpFoundation.dll'
    & $buildTool (Join-Path $PSScriptRoot 'Source\ScpFoundation.CombatExtended\ScpFoundation.CombatExtended.csproj') /t:Build "/p:RimWorldDir=$gameRoot" "/p:HarmonyAssembly=$HarmonyAssembly" "/p:CombatExtendedDir=$combatExtendedDir" "/p:FoundationAssembly=$foundationAssembly" /verbosity:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw "Combat Extended adapter build failed with exit code $LASTEXITCODE" }
    Write-Output 'Built optional Combat Extended adapter.'
}
if (!$StageOnly) {
    & (Join-Path $PSScriptRoot 'Install.ps1')
}
