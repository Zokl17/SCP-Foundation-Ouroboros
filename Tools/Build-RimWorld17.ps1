param(
    [Parameter(Mandatory = $true)][string]$RimWorldDir,
    [switch]$DevelopmentTools
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$gameRoot = (Resolve-Path -LiteralPath $RimWorldDir).Path
if ([Environment]::Version.Major -lt 10) { throw 'Build with PowerShell 7.6+ on .NET 10.' }
$gameAssembly = Join-Path $gameRoot 'Assembly-CSharp.dll'
$harmonyAssembly = Join-Path $gameRoot 'Mods\Harmony\1.7\Assemblies\0Harmony.dll'
foreach ($required in @($gameAssembly, $harmonyAssembly, (Join-Path $gameRoot 'UnityStubs.dll'))) {
    if (!(Test-Path -LiteralPath $required)) { throw "Missing 1.7 build reference: $required" }
}
$outputRoot = Join-Path $projectRoot ('Build\1.7' + $(if ($DevelopmentTools) { '-Development' } else { '' }))
[void][IO.Directory]::CreateDirectory($outputRoot)
[void][Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll'))
$parseOptions = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default
if ($DevelopmentTools) { $parseOptions = $parseOptions.WithPreprocessorSymbols([string[]]@('SCP_DEVELOPMENT_TOOLS')) }
$trees = [Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Source\ScpFoundation') -Recurse -Filter '*.cs' -File) {
    $trees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
        [IO.File]::ReadAllText($file.FullName), $parseOptions, $file.FullName))
}
$references = [Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new()
# Reference assemblies from this .NET 10 host; engine and mod references stay local.
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' -File) {
    $references.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($file.FullName))
}
foreach ($file in Get-ChildItem -LiteralPath $gameRoot -Filter '*.dll' -File) {
    if ($file.Name -match '^(System\.|Microsoft\.|mscorlib\.|netstandard\.)') { continue }
    try { [void][Reflection.AssemblyName]::GetAssemblyName($file.FullName) }
    catch [BadImageFormatException] { continue }
    $references.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($file.FullName))
}
$references.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($harmonyAssembly))
$options = [Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new(
    [Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary).WithOptimizationLevel(
        [Microsoft.CodeAnalysis.OptimizationLevel]::Release)
$compilation = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('ScpFoundation', $trees, $references, $options)
$outputFile = Join-Path $outputRoot 'ScpFoundation.dll'
$stream = [IO.File]::Create($outputFile)
try { $result = $compilation.Emit($stream) } finally { $stream.Dispose() }
$diagnostics = @($result.Diagnostics | ForEach-Object ToString)
[IO.File]::WriteAllLines((Join-Path $outputRoot 'Build.log'), [string[]]$diagnostics)
if (!$result.Success) {
    $result.Diagnostics | Where-Object Severity -EQ Error | Select-Object -First 35 | ForEach-Object ToString
    throw "RimWorld 1.7 compilation failed. Full diagnostics: $outputRoot\Build.log"
}
Write-Output "Built RimWorld 1.7 DLL: $outputFile (development tools: $DevelopmentTools)"
