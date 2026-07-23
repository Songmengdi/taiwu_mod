[CmdletBinding()]
param(
    [string]$GameDir = 'D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu',
    [switch]$VerifyOnly,
    [switch]$SkipGameInstall
)

$ErrorActionPreference = 'Stop'

$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$gameModRoot = Join-Path $GameDir 'Mod'

function Get-ConfigVersion {
    param([Parameter(Mandatory)][string]$Path)

    $text = Get-Content -Raw -LiteralPath $Path
    $match = [regex]::Match($text, '(?m)^\s*Version\s*=\s*"(?<version>[^"]+)"')
    if (-not $match.Success) {
        throw "Version not found in $Path"
    }

    return $match.Groups['version'].Value
}

function Get-ConfigFileId {
    param([Parameter(Mandatory)][string]$Path)

    $text = Get-Content -Raw -LiteralPath $Path
    $match = [regex]::Match($text, '(?m)^\s*FileId\s*=\s*(?<fileId>\d+)')
    if (-not $match.Success) {
        throw "FileId not found in $Path"
    }

    return $match.Groups['fileId'].Value
}

function Find-ModRootByFileId {
    param(
        [Parameter(Mandatory)][string]$Parent,
        [Parameter(Mandatory)][string]$FileId
    )

    if (-not (Test-Path -LiteralPath $Parent)) {
        throw "Missing MOD parent directory: $Parent"
    }

    $matches = @(
        Get-ChildItem -LiteralPath $Parent -Directory | Where-Object {
            $config = Join-Path $_.FullName 'Config.lua'
            (Test-Path -LiteralPath $config) -and ((Get-ConfigFileId -Path $config) -eq $FileId)
        }
    )

    if ($matches.Count -ne 1) {
        throw "Expected exactly one MOD directory with FileId $FileId under $Parent; found $($matches.Count)"
    }

    return $matches[0].FullName
}

function Assert-HashMatch {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Destination
    )

    if (-not (Test-Path -LiteralPath $Destination)) {
        throw "Missing release file: $Destination"
    }

    $sourceHash = (Get-FileHash -LiteralPath $Source -Algorithm SHA256).Hash
    $destinationHash = (Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash
    if ($sourceHash -ne $destinationHash) {
        throw "Release file mismatch: $Destination"
    }
}

function Assert-ConfigVersionMatch {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Destination
    )

    if (-not (Test-Path -LiteralPath $Destination)) {
        throw "Missing installed manifest: $Destination"
    }

    $sourceVersion = Get-ConfigVersion -Path $Source
    $destinationVersion = Get-ConfigVersion -Path $Destination
    if ($sourceVersion -ne $destinationVersion) {
        throw "Installed manifest version mismatch: expected $sourceVersion, got $destinationVersion in $Destination"
    }
}

function Copy-ReleaseFile {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Destination
    )

    if (-not (Test-Path -LiteralPath $Source)) {
        throw "Missing build output: $Source"
    }

    $destinationDirectory = Split-Path -Parent $Destination
    if (-not (Test-Path -LiteralPath $destinationDirectory)) {
        throw "Missing destination directory: $destinationDirectory"
    }

    Copy-Item -LiteralPath $Source -Destination $Destination -Force
}

function Invoke-ReleaseBuild {
    param([Parameter(Mandatory)][string]$Project, [Parameter(Mandatory)][string]$WorkingDirectory)

    $projectPath = Join-Path $WorkingDirectory $Project
    & dotnet build $projectPath -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed: $Project"
    }
}

$framework = [pscustomobject]@{
    Name = 'Taiwu UI Framework'
    SourceRoot = Join-Path $workspace 'taiwu-ui-framework'
    ArtifactRoot = Join-Path $workspace 'taiwu-ui-framework\mod'
    PackageRoot = Join-Path $workspace 'taiwu-ui-framework\publish\TaiwuUiFramework'
    InstallRoot = Join-Path $gameModRoot 'TaiwuUiFramework'
    ConfigRelativePath = 'Config.lua'
    DllRelativePaths = @('Plugins\TaiwuUi.Core.dll', 'Plugins\TaiwuUi.Core.deps.json')
    VersionDllRelativePath = 'Plugins\TaiwuUi.Core.dll'
}

$finderSourceRoot = Join-Path $workspace 'map-skill-finder\mod'
$finderFileId = Get-ConfigFileId -Path (Join-Path $finderSourceRoot 'Config.lua')
$finder = [pscustomobject]@{
    Name = 'Map Skill Finder'
    SourceRoot = $finderSourceRoot
    ArtifactRoot = $finderSourceRoot
    PackageRoot = Find-ModRootByFileId -Parent (Join-Path $workspace 'map-skill-finder\publish') -FileId $finderFileId
    InstallRoot = Find-ModRootByFileId -Parent $gameModRoot -FileId $finderFileId
    ConfigRelativePath = 'Config.lua'
    DllRelativePaths = @(
        'Plugins\MapSkillFinder.Backend.dll',
        'Plugins\MapSkillFinder.Backend.deps.json',
        'Plugins\MapSkillFinder.Frontend.dll',
        'Plugins\MapSkillFinder.Frontend.deps.json'
    )
    VersionDllRelativePath = 'Plugins\MapSkillFinder.Frontend.dll'
}

if (-not $VerifyOnly) {
    Invoke-ReleaseBuild -Project '.\TaiwuUi.Core.csproj' -WorkingDirectory (Join-Path $workspace 'taiwu-ui-framework')
    Invoke-ReleaseBuild -Project '.\MapSkillFinder.Backend.csproj' -WorkingDirectory (Join-Path $workspace 'map-skill-finder')
    Invoke-ReleaseBuild -Project '.\MapSkillFinder.Frontend.csproj' -WorkingDirectory (Join-Path $workspace 'map-skill-finder')
}

foreach ($release in @($framework, $finder)) {
    $sourceConfig = Join-Path $release.SourceRoot $release.ConfigRelativePath
    $packageConfig = Join-Path $release.PackageRoot $release.ConfigRelativePath
    $installConfig = Join-Path $release.InstallRoot $release.ConfigRelativePath
    $cover = Join-Path $release.PackageRoot 'Cover.jpg'

    if (-not (Test-Path -LiteralPath $cover)) {
        throw "$($release.Name) upload package is missing Cover.jpg: $cover"
    }

    if (-not $VerifyOnly) {
        Copy-ReleaseFile -Source $sourceConfig -Destination $packageConfig
        if (-not $SkipGameInstall) {
            Copy-ReleaseFile -Source $sourceConfig -Destination $installConfig
        }

        foreach ($relativePath in $release.DllRelativePaths) {
            $source = Join-Path $release.ArtifactRoot $relativePath
            Copy-ReleaseFile -Source $source -Destination (Join-Path $release.PackageRoot $relativePath)
            if (-not $SkipGameInstall) {
                Copy-ReleaseFile -Source $source -Destination (Join-Path $release.InstallRoot $relativePath)
            }
        }
    }

    Assert-HashMatch -Source $sourceConfig -Destination $packageConfig
    if (-not $SkipGameInstall) {
        Assert-ConfigVersionMatch -Source $sourceConfig -Destination $installConfig
    }

    foreach ($relativePath in $release.DllRelativePaths) {
        $source = Join-Path $release.ArtifactRoot $relativePath
        Assert-HashMatch -Source $source -Destination (Join-Path $release.PackageRoot $relativePath)
        if (-not $SkipGameInstall) {
            Assert-HashMatch -Source $source -Destination (Join-Path $release.InstallRoot $relativePath)
        }
    }

    $version = Get-ConfigVersion -Path $sourceConfig
    $fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $release.ArtifactRoot $release.VersionDllRelativePath)).FileVersion
    if ($fileVersion -ne $version) {
        throw "$($release.Name) Config.lua version $version does not match DLL file version $fileVersion"
    }

    $packageExtras = Get-ChildItem -LiteralPath $release.PackageRoot -Recurse -File |
        Where-Object { $_.Extension -eq '.pdb' -or $_.Name -eq 'Settings.lua' }
    if ($packageExtras) {
        throw "$($release.Name) upload package contains forbidden files: $($packageExtras.FullName -join ', ')"
    }

    Write-Host "$($release.Name): $version verified"
}

Write-Host 'Steam release packages are ready.'
