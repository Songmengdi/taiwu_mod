$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$gameModRoot = 'D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu\Mod'
$frameworkProject = Join-Path $root 'TaiwuUi.Core.csproj'
$sampleProject = Join-Path $root 'sample\TaiwuUi.Sample.csproj'

dotnet build $frameworkProject -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build $sampleProject -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$frameworkTarget = Join-Path $gameModRoot 'TaiwuUiFramework'
$sampleTarget = Join-Path $gameModRoot 'TaiwuUiFrameworkSample'
$frameworkPlugins = Join-Path $frameworkTarget 'Plugins'
$samplePlugins = Join-Path $sampleTarget 'Plugins'
New-Item -ItemType Directory -Force -Path $frameworkPlugins | Out-Null
New-Item -ItemType Directory -Force -Path $samplePlugins | Out-Null

Copy-Item -LiteralPath (Join-Path $root 'Config.lua') -Destination (Join-Path $frameworkTarget 'Config.lua') -Force
Copy-Item -LiteralPath (Join-Path $root 'mod\Plugins\TaiwuUi.Core.dll') -Destination $frameworkPlugins -Force
Copy-Item -LiteralPath (Join-Path $root 'sample\Config.lua') -Destination (Join-Path $sampleTarget 'Config.lua') -Force
Copy-Item -LiteralPath (Join-Path $root 'sample\mod\Plugins\TaiwuUi.Sample.dll') -Destination $samplePlugins -Force

$unexpectedFramework = Join-Path $samplePlugins 'TaiwuUi.Core.dll'
if (Test-Path -LiteralPath $unexpectedFramework) {
    Remove-Item -LiteralPath $unexpectedFramework -Force
}

Write-Host "TaiwuUi provider installed to $frameworkTarget"
Write-Host "Sample consumer installed to $sampleTarget"
Write-Host 'Enable both MODs, restart, then press F10.'
