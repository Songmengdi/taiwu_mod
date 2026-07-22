$ErrorActionPreference = 'Stop'

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$gameDir = 'D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu'
$targetDir = Join-Path $gameDir 'Mod\太吾村本月最优排班'
$pluginDir = Join-Path $targetDir 'Plugins'

dotnet build (Join-Path $projectDir 'VillageWorkOptimizer.Backend.csproj') -c Release
dotnet build (Join-Path $projectDir 'VillageWorkOptimizer.Frontend.csproj') -c Release

New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item -LiteralPath (Join-Path $projectDir 'Config.lua') -Destination (Join-Path $targetDir 'Config.lua') -Force
Copy-Item -LiteralPath (Join-Path $projectDir 'mod\Plugins\VillageWorkOptimizer.Backend.dll') -Destination $pluginDir -Force
Copy-Item -LiteralPath (Join-Path $projectDir 'mod\Plugins\VillageWorkOptimizer.Frontend.dll') -Destination $pluginDir -Force

Write-Host "Installed to $targetDir"
