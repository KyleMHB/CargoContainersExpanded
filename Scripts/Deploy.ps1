$ErrorActionPreference = "Stop"

$toolPath = Join-Path $PSScriptRoot "..\..\_Shared\RimWorldModTools.ps1"
. $toolPath

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "Source\cargo-containers-expanded.csproj"
$targetRoot = Get-RimWorldModTargetPath -ModName "Cargo Containers Expanded"

Invoke-RimWorldDotNetBuild -ProjectPath $projectPath -Configuration "Release"
Sync-RimWorldModFolders -SourceRoot $repoRoot -TargetRoot $targetRoot -Folders @("About", "1.6", "Languages", "Textures")

$stalePdb = Join-Path $targetRoot "Assemblies\CargoContainersExpanded.pdb"
if (Test-Path -LiteralPath $stalePdb) {
    Remove-Item -LiteralPath $stalePdb -Force
}
