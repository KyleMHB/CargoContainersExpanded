$ErrorActionPreference = "Stop"

$toolPath = Join-Path $PSScriptRoot "..\_Shared\RimWorldModTools.ps1"
. $toolPath

Invoke-RimWorldModDeploy `
    -ModName "Cargo Containers Expanded" `
    -SourceRoot $PSScriptRoot `
    -BuildPath (Join-Path $PSScriptRoot "Source\cargo-containers-expanded.csproj") `
    -Configuration "Release" `
    -Folders @("About", "Assemblies", "1.6", "Languages", "Textures") `
    -RemoveFilePatterns @("*.pdb")
