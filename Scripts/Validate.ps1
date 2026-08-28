[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure([string]$Message) {
    $failures.Add($Message)
}

function Load-Xml([string]$Path) {
    try {
        return [xml](Get-Content -LiteralPath $Path -Raw)
    }
    catch {
        Add-Failure "Invalid XML: $Path ($($_.Exception.Message))"
        return $null
    }
}

Get-ChildItem -LiteralPath $repoRoot -Recurse -File -Filter '*.xml' |
    Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' } |
    ForEach-Object { [void](Load-Xml $_.FullName) }

$aboutPath = Join-Path $repoRoot 'About/About.xml'
$about = Load-Xml $aboutPath
if ($about) {
    $metadata = $about.ModMetaData
    if ([string]$metadata.packageId -cne 'KyleMHB.CargoContainerExpanded') {
        Add-Failure 'About.xml packageId changed unexpectedly.'
    }

    $versions = @($metadata.supportedVersions.li | ForEach-Object { [string]$_ })
    if ($versions.Count -ne 1 -or $versions[0] -ne '1.6') {
        Add-Failure 'About.xml must support exactly RimWorld 1.6.'
    }

    $dependencies = @($metadata.modDependencies.li.packageId | ForEach-Object { [string]$_ })
    if ($dependencies.Count -ne 1 -or $dependencies[0] -cne 'brrainz.harmony') {
        Add-Failure 'Harmony must remain the only required dependency.'
    }

    $incompatible = @($metadata.incompatibleWith.li | ForEach-Object { [string]$_ })
    if ($incompatible -cnotcontains 'Aoba.CargoContainer') {
        Add-Failure 'About.xml is missing incompatibility with Aoba.CargoContainer.'
    }
}

$structurePath = Join-Path $repoRoot '1.6/Defs/FT_Structure.xml'
$structure = Load-Xml $structurePath
if ($structure) {
    $thingDefs = @($structure.Defs.ThingDef)
    $namedDefs = @{}
    foreach ($thingDef in $thingDefs) {
        $name = [string]$thingDef.Name
        if ($name) { $namedDefs[$name] = $thingDef }
    }

    function Resolve-Ticker($ThingDef) {
        $visited = [System.Collections.Generic.HashSet[string]]::new()
        $current = $ThingDef
        while ($current) {
            $ticker = [string]$current.tickerType
            if ($ticker) { return $ticker }
            $parentName = [string]$current.ParentName
            if (-not $parentName -or -not $visited.Add($parentName) -or -not $namedDefs.ContainsKey($parentName)) {
                break
            }
            $current = $namedDefs[$parentName]
        }
        return $null
    }

    foreach ($defName in @('FT_ContainerBeer', 'FT_ContainerNeutroamine', 'FT_ContainerWort')) {
        $thingDef = $thingDefs | Where-Object { [string]$_.defName -ceq $defName } | Select-Object -First 1
        if (-not $thingDef -or (Resolve-Ticker $thingDef) -cne 'Never') {
            Add-Failure "$defName must resolve to tickerType Never."
        }
    }

    $chemfuel = $thingDefs | Where-Object { [string]$_.defName -ceq 'FT_ContainerChemfuel' } | Select-Object -First 1
    if (-not $chemfuel -or (Resolve-Ticker $chemfuel) -cne 'Normal') {
        Add-Failure 'FT_ContainerChemfuel must resolve to tickerType Normal.'
    }

    foreach ($defName in @('FT_RefrigeratedContainer', 'FT_RefrigeratedContainerHalf')) {
        $thingDef = $thingDefs | Where-Object { [string]$_.defName -ceq $defName } | Select-Object -First 1
        if (-not $thingDef -or (Resolve-Ticker $thingDef) -cne 'Rare') {
            Add-Failure "$defName must resolve to tickerType Rare."
        }
        if ([string]$thingDef.defaultStuff -cne 'RawRice') {
            Add-Failure "$defName must retain RawRice as its XML default stuff."
        }
    }

    foreach ($graphicData in @($structure.SelectNodes('//graphicData[texPath]'))) {
        $textureStem = Join-Path (Join-Path $repoRoot 'Textures') ([string]$graphicData.texPath -replace '/', [IO.Path]::DirectorySeparatorChar)
        foreach ($suffix in @('_east.png', '_south.png')) {
            if (-not (Test-Path -LiteralPath ($textureStem + $suffix) -PathType Leaf)) {
                Add-Failure "Missing referenced texture: $($textureStem + $suffix)"
            }
        }
        if ([string]$graphicData.shaderType -ceq 'CutoutComplex') {
            foreach ($suffix in @('_eastm.png', '_southm.png')) {
                if (-not (Test-Path -LiteralPath ($textureStem + $suffix) -PathType Leaf)) {
                    Add-Failure "Missing CutoutComplex mask: $($textureStem + $suffix)"
                }
            }
        }
    }
}

$allDefNames = @()
Get-ChildItem -LiteralPath (Join-Path $repoRoot '1.6/Defs') -File -Filter '*.xml' | ForEach-Object {
    $document = Load-Xml $_.FullName
    if ($document) {
        $allDefNames += @($document.SelectNodes('/Defs/*/defName') | ForEach-Object { [string]$_.InnerText })
    }
}
$duplicates = @($allDefNames | Group-Object | Where-Object Count -gt 1)
foreach ($duplicate in $duplicates) {
    Add-Failure "Duplicate Def name: $($duplicate.Name)"
}

$requiredEnglishKeys = @(
    'CCE_CargoPayload',
    'CCE_PoweredRefrigerationActive',
    'CCE_CurrentlyFrozen',
    'CCE_CurrentlyRefrigerated',
    'CCE_NotRefrigerated',
    'CCE_RotRate',
    'CCE_ExtractRecipeLabel',
    'CCE_ExtractRecipeDescription'
)
$englishKeyedPath = Join-Path $repoRoot 'Languages/English/Keyed/CargoContainersExpanded.xml'
$englishKeyed = Load-Xml $englishKeyedPath
if ($englishKeyed) {
    foreach ($key in $requiredEnglishKeys) {
        if (-not $englishKeyed.LanguageData.SelectSingleNode($key)) {
            Add-Failure "Missing required English keyed translation: $key"
        }
    }
}

foreach ($language in @('English', 'ChineseSimplified', 'ChineseTraditional')) {
    Get-ChildItem -LiteralPath (Join-Path $repoRoot "Languages/$language") -Recurse -File -Filter '*.xml' | ForEach-Object {
        $document = Load-Xml $_.FullName
        if ($document -and $document.DocumentElement.Name -cne 'LanguageData') {
            Add-Failure "Localization file must use a LanguageData root: $($_.FullName)"
        }
        if ($document) {
            $generatedDefKeys = @($document.LanguageData.ChildNodes | Where-Object { $_.NodeType -eq 'Element' -and $_.Name -match '_(Blueprint|Frame)\.' })
            foreach ($generatedDefKey in $generatedDefKeys) {
                Add-Failure "Obsolete generated DefInjected key: $($generatedDefKey.Name) in $($_.FullName)"
            }
        }
    }
}

foreach ($language in @('ChineseSimplified', 'ChineseTraditional')) {
    $placeholderPath = Join-Path $repoRoot "Languages/$language/Keyed/CargoContainersExpanded.xml"
    if (Test-Path -LiteralPath $placeholderPath) {
        Add-Failure "English placeholder keyed localization must be removed: $placeholderPath"
    }
    $keyedFolder = Join-Path $repoRoot "Languages/$language/Keyed"
    if (Test-Path -LiteralPath $keyedFolder) {
        Get-ChildItem -LiteralPath $keyedFolder -Recurse -File | ForEach-Object {
            if ((Get-Content -LiteralPath $_.FullName -Raw) -match '(?i)TODO') {
                Add-Failure "TODO placeholder remains in Chinese keyed localization: $($_.FullName)"
            }
        }
    }
}

$assembliesPath = Join-Path $repoRoot 'Assemblies'
$requiredRuntimeFolders = @('1.6', 'About', 'Assemblies', 'Languages', 'Textures')
foreach ($folder in $requiredRuntimeFolders) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $folder) -PathType Container)) {
        Add-Failure "Required runtime folder is missing: $folder"
    }
}
Get-ChildItem -LiteralPath $repoRoot -Directory | Where-Object { $_.Name -match '^\d+\.\d+$' -and $_.Name -cne '1.6' } | ForEach-Object {
    Add-Failure "Unsupported versioned runtime folder is present: $($_.Name)"
}

$assemblyFiles = @(Get-ChildItem -LiteralPath $assembliesPath -File)
foreach ($file in $assemblyFiles) {
    if ($file.Name -cne 'CargoContainersExpanded.dll') {
        Add-Failure "Unexpected packaged assembly output: $($file.Name)"
    }
}
if (-not (Test-Path -LiteralPath (Join-Path $assembliesPath 'CargoContainersExpanded.dll') -PathType Leaf)) {
    Add-Failure 'CargoContainersExpanded.dll is missing from Assemblies.'
}

$forbiddenBinaryPatterns = @('0Harmony.dll', 'Assembly-CSharp.dll', 'UnityEngine*.dll', '*.pdb')
foreach ($pattern in $forbiddenBinaryPatterns) {
    Get-ChildItem -LiteralPath $repoRoot -Recurse -File -Filter $pattern |
        Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' } |
        ForEach-Object { Add-Failure "Forbidden runtime/dependency output: $($_.FullName)" }
}

if ($failures.Count -gt 0) {
    Write-Host "Validation failed with $($failures.Count) issue(s):" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    exit 1
}

Write-Host 'Cargo Containers Expanded validation passed.' -ForegroundColor Green
