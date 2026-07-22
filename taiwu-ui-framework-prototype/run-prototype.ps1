$ErrorActionPreference = 'Stop'

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$gameDir = 'D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu'
$targetDir = Join-Path $gameDir 'Mod\TaiwuUiFrameworkPrototype'
$pluginDir = Join-Path $targetDir 'Plugins'
$project = Join-Path $projectDir 'TaiwuUiFrameworkPrototype.Frontend.csproj'
$assembly = Join-Path $projectDir 'mod\Plugins\TaiwuUiFrameworkPrototype.Frontend.dll'

dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item -LiteralPath (Join-Path $projectDir 'Config.lua') -Destination (Join-Path $targetDir 'Config.lua') -Force
Copy-Item -LiteralPath $assembly -Destination $pluginDir -Force

Write-Host "Prototype installed to $targetDir"
Write-Host 'Enable it in the MOD manager, restart, then press F9 on the world map.'

